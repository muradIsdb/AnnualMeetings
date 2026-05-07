using System.Text;
using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[Route("api/departure-requests")]
public class DepartureRequestsController : ApiControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;

    public DepartureRequestsController(IAppDbContext db, IEmailService email, IConfiguration config)
    {
        _db = db;
        _email = email;
        _config = config;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult> Submit([FromBody] SubmitDepartureRequest req, CancellationToken ct)
    {
        if (!await ValidateTurnstileAsync(req.TurnstileToken, ct))
            return BadRequest(new { message = "CAPTCHA validation failed. Please try again." });

        var hotel = await _db.HotelOptions.FindAsync(new object[] { req.HotelOptionId }, ct);
        if (hotel == null || !hotel.IsActive) return BadRequest(new { message = "Invalid hotel selection." });

        var day = await _db.PickupDayOptions.FindAsync(new object[] { req.PickupDayOptionId }, ct);
        if (day == null || !day.IsActive) return BadRequest(new { message = "Invalid pickup day selection." });

        var hour = await _db.PickupHourOptions.FindAsync(new object[] { req.PickupHourOptionId }, ct);
        if (hour == null || !hour.IsActive) return BadRequest(new { message = "Invalid pickup hour selection." });

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var existing = await _db.DepartureRequests
            .FirstOrDefaultAsync(r => r.Email.ToLower() == req.Email.ToLower(), ct);

        if (existing != null)
        {
            existing.FullName = req.FullName.Trim();
            existing.RoomNumber = req.RoomNumber.Trim();
            existing.HotelOptionId = req.HotelOptionId;
            existing.PickupDayOptionId = req.PickupDayOptionId;
            existing.PickupHourOptionId = req.PickupHourOptionId;
            existing.DisclaimerAccepted = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            var manageUrl = $"{baseUrl}/departure/manage/{existing.ManageToken}";
            _ = Task.Run(() => _email.SendDepartureUpdateAsync(existing.Email, existing.FullName, hotel.Name, existing.RoomNumber, day.Label, hour.Label, manageUrl), ct);

            return Ok(new DepartureSubmitResponse { ManageToken = existing.ManageToken, IsUpdate = true, Message = "Your registration has been updated. A confirmation email has been sent." });
        }
        else
        {
            var record = new DepartureRequest
            {
                FullName = req.FullName.Trim(),
                Email = req.Email.Trim().ToLower(),
                RoomNumber = req.RoomNumber.Trim(),
                HotelOptionId = req.HotelOptionId,
                PickupDayOptionId = req.PickupDayOptionId,
                PickupHourOptionId = req.PickupHourOptionId,
                DisclaimerAccepted = true,
                ManageToken = Guid.NewGuid(),
                SubmittedAt = DateTime.UtcNow,
            };
            _db.DepartureRequests.Add(record);
            await _db.SaveChangesAsync(ct);

            var manageUrl = $"{baseUrl}/departure/manage/{record.ManageToken}";
            _ = Task.Run(() => _email.SendDepartureConfirmationAsync(record.Email, record.FullName, hotel.Name, record.RoomNumber, day.Label, hour.Label, manageUrl), ct);

            return CreatedAtAction(nameof(GetByToken), new { token = record.ManageToken },
                new DepartureSubmitResponse { ManageToken = record.ManageToken, IsUpdate = false, Message = "Registration successful! A confirmation email has been sent." });
        }
    }

    [HttpGet("manage/{token:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<DepartureRequestManageDto>> GetByToken(Guid token, CancellationToken ct)
    {
        var r = await _db.DepartureRequests
            .Include(x => x.HotelOption)
            .Include(x => x.PickupDayOption)
            .Include(x => x.PickupHourOption)
            .FirstOrDefaultAsync(x => x.ManageToken == token, ct);

        if (r == null) return NotFound(new { message = "Registration not found or link has expired." });

        return Ok(new DepartureRequestManageDto
        {
            Id = r.Id, FullName = r.FullName, Email = r.Email, RoomNumber = r.RoomNumber,
            HotelOptionId = r.HotelOptionId, HotelName = r.HotelOption.Name,
            PickupDayOptionId = r.PickupDayOptionId, PickupDayLabel = r.PickupDayOption.Label,
            PickupHourOptionId = r.PickupHourOptionId, PickupHourLabel = r.PickupHourOption.Label,
            SubmittedAt = r.SubmittedAt, UpdatedAt = r.UpdatedAt,
        });
    }

    [HttpPut("manage/{token:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> UpdateByToken(Guid token, [FromBody] UpdateDepartureRequest req, CancellationToken ct)
    {
        var r = await _db.DepartureRequests.FirstOrDefaultAsync(x => x.ManageToken == token, ct);
        if (r == null) return NotFound(new { message = "Registration not found." });

        var hotel = await _db.HotelOptions.FindAsync(new object[] { req.HotelOptionId }, ct);
        if (hotel == null || !hotel.IsActive) return BadRequest(new { message = "Invalid hotel selection." });
        var day = await _db.PickupDayOptions.FindAsync(new object[] { req.PickupDayOptionId }, ct);
        if (day == null || !day.IsActive) return BadRequest(new { message = "Invalid pickup day selection." });
        var hour = await _db.PickupHourOptions.FindAsync(new object[] { req.PickupHourOptionId }, ct);
        if (hour == null || !hour.IsActive) return BadRequest(new { message = "Invalid pickup hour selection." });

        r.FullName = req.FullName.Trim();
        r.RoomNumber = req.RoomNumber.Trim();
        r.HotelOptionId = req.HotelOptionId;
        r.PickupDayOptionId = req.PickupDayOptionId;
        r.PickupHourOptionId = req.PickupHourOptionId;
        r.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var manageUrl = $"{baseUrl}/departure/manage/{r.ManageToken}";
        _ = Task.Run(() => _email.SendDepartureUpdateAsync(r.Email, r.FullName, hotel.Name, r.RoomNumber, day.Label, hour.Label, manageUrl), ct);

        return Ok(new { message = "Registration updated successfully." });
    }

    [HttpDelete("manage/{token:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult> CancelByToken(Guid token, CancellationToken ct)
    {
        var r = await _db.DepartureRequests.FirstOrDefaultAsync(x => x.ManageToken == token, ct);
        if (r == null) return NotFound(new { message = "Registration not found." });

        var name = r.FullName; var email = r.Email;
        _db.DepartureRequests.Remove(r);
        await _db.SaveChangesAsync(ct);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        _ = Task.Run(() => _email.SendDepartureCancellationAsync(email, name, $"{baseUrl}/departure"), ct);

        return Ok(new { message = "Registration cancelled." });
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Transport,ControlRoom")]
    public async Task<ActionResult<PagedResult<DepartureRequestAdminDto>>> GetAll(
        [FromQuery] Guid? hotelId, [FromQuery] Guid? dayId, [FromQuery] Guid? hourId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(page, 1);

        var query = _db.DepartureRequests
            .Include(r => r.HotelOption).Include(r => r.PickupDayOption).Include(r => r.PickupHourOption)
            .AsQueryable();

        if (hotelId.HasValue) query = query.Where(r => r.HotelOptionId == hotelId);
        if (dayId.HasValue) query = query.Where(r => r.PickupDayOptionId == dayId);
        if (hourId.HasValue) query = query.Where(r => r.PickupHourOptionId == hourId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.ToLower();
            query = query.Where(r => r.FullName.ToLower().Contains(q) || r.Email.ToLower().Contains(q) || r.RoomNumber.ToLower().Contains(q));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(r => r.PickupDayOption.DisplayOrder).ThenBy(r => r.PickupHourOption.DisplayOrder)
            .ThenBy(r => r.HotelOption.DisplayOrder).ThenBy(r => r.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new DepartureRequestAdminDto
            {
                Id = r.Id, FullName = r.FullName, Email = r.Email, RoomNumber = r.RoomNumber,
                HotelOptionId = r.HotelOptionId, HotelName = r.HotelOption.Name,
                PickupDayOptionId = r.PickupDayOptionId, PickupDayLabel = r.PickupDayOption.Label,
                PickupHourOptionId = r.PickupHourOptionId, PickupHourLabel = r.PickupHourOption.Label,
                SubmittedAt = r.SubmittedAt, UpdatedAt = r.UpdatedAt,
            }).ToListAsync(ct);

        return Ok(new PagedResult<DepartureRequestAdminDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("export/csv")]
    [Authorize(Roles = "Admin,Transport,ControlRoom")]
    public async Task<IActionResult> ExportCsv(CancellationToken ct)
    {
        var list = await _db.DepartureRequests
            .Include(r => r.HotelOption).Include(r => r.PickupDayOption).Include(r => r.PickupHourOption)
            .OrderBy(r => r.PickupDayOption.DisplayOrder).ThenBy(r => r.PickupHourOption.DisplayOrder)
            .ThenBy(r => r.HotelOption.DisplayOrder).ThenBy(r => r.FullName)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Full Name,Email,Room Number,Hotel,Pickup Day,Pickup Hour,Submitted At,Updated At");
        foreach (var r in list)
            sb.AppendLine($"\"{r.FullName}\",\"{r.Email}\",\"{r.RoomNumber}\",\"{r.HotelOption.Name}\",\"{r.PickupDayOption.Label}\",\"{r.PickupHourOption.Label}\",\"{r.SubmittedAt:yyyy-MM-dd HH:mm}\",\"{r.UpdatedAt:yyyy-MM-dd HH:mm}\"");

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"departure-registrations-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("stats")]
    [Authorize(Roles = "Admin,Transport,ControlRoom")]
    public async Task<ActionResult<DepartureStatsDto>> GetStats(CancellationToken ct)
    {
        var all = await _db.DepartureRequests
            .Include(r => r.HotelOption).Include(r => r.PickupDayOption).Include(r => r.PickupHourOption)
            .ToListAsync(ct);

        var byHotel = all.GroupBy(r => new { r.HotelOptionId, r.HotelOption.Name })
            .Select(g => new HotelStat { HotelId = g.Key.HotelOptionId, HotelName = g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count).ToList();

        var byDay = all.GroupBy(r => new { r.PickupDayOptionId, r.PickupDayOption.Label, r.PickupDayOption.DisplayOrder })
            .Select(g => new DayStat
            {
                DayId = g.Key.PickupDayOptionId, DayLabel = g.Key.Label, DisplayOrder = g.Key.DisplayOrder, Count = g.Count(),
                ByHour = g.GroupBy(r => new { r.PickupHourOptionId, r.PickupHourOption.Label, r.PickupHourOption.DisplayOrder })
                    .Select(hg => new HourStat
                    {
                        HourId = hg.Key.PickupHourOptionId, HourLabel = hg.Key.Label, DisplayOrder = hg.Key.DisplayOrder, Count = hg.Count(),
                        ByHotel = hg.GroupBy(r => new { r.HotelOptionId, r.HotelOption.Name })
                            .Select(hhg => new HotelStat { HotelId = hhg.Key.HotelOptionId, HotelName = hhg.Key.Name, Count = hhg.Count() })
                            .OrderByDescending(x => x.Count).ToList()
                    }).OrderBy(x => x.DisplayOrder).ToList()
            }).OrderBy(x => x.DisplayOrder).ToList();

        return Ok(new DepartureStatsDto { TotalRegistrations = all.Count, ByHotel = byHotel, ByDay = byDay });
    }

    [HttpGet("server-time")]
    [AllowAnonymous]
    public async Task<ActionResult> GetServerTime()
    {
        var utcNow = DateTime.UtcNow;
        var config = await _db.AppConfigs.FindAsync(1);
        var tzId = config?.EventTimezone ?? "Asia/Riyadh";
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { tz = TimeZoneInfo.Utc; tzId = "UTC"; }
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
        return Ok(new
        {
            utcNow,
            isoNow = utcNow.ToString("o"),
            localNow = localNow.ToString("o"),
            localDate = localNow.ToString("yyyy-MM-dd"),
            timezone = tzId
        });
    }

    private async Task<bool> ValidateTurnstileAsync(string? token, CancellationToken ct)
    {
        var secret = _config["Turnstile:SecretKey"];
        if (string.IsNullOrEmpty(secret) || secret == "TURNSTILE_DISABLED") return true;
        if (string.IsNullOrEmpty(token)) return false;

        using var http = new HttpClient();
        var resp = await http.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["secret"] = secret, ["response"] = token }), ct);
        if (!resp.IsSuccessStatusCode) return false;
        var json = await resp.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken: ct);
        return json?.Success == true;
    }

    private record TurnstileResponse(bool Success);
}

public record SubmitDepartureRequest
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string RoomNumber { get; init; } = string.Empty;
    public Guid HotelOptionId { get; init; }
    public Guid PickupDayOptionId { get; init; }
    public Guid PickupHourOptionId { get; init; }
    public bool DisclaimerAccepted { get; init; }
    public string? TurnstileToken { get; init; }
}

public record UpdateDepartureRequest
{
    public string FullName { get; init; } = string.Empty;
    public string RoomNumber { get; init; } = string.Empty;
    public Guid HotelOptionId { get; init; }
    public Guid PickupDayOptionId { get; init; }
    public Guid PickupHourOptionId { get; init; }
}

public record DepartureSubmitResponse
{
    public Guid ManageToken { get; init; }
    public bool IsUpdate { get; init; }
    public string Message { get; init; } = string.Empty;
}

public record DepartureRequestManageDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string RoomNumber { get; init; } = string.Empty;
    public Guid HotelOptionId { get; init; }
    public string HotelName { get; init; } = string.Empty;
    public Guid PickupDayOptionId { get; init; }
    public string PickupDayLabel { get; init; } = string.Empty;
    public Guid PickupHourOptionId { get; init; }
    public string PickupHourLabel { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record DepartureRequestAdminDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string RoomNumber { get; init; } = string.Empty;
    public Guid HotelOptionId { get; init; }
    public string HotelName { get; init; } = string.Empty;
    public Guid PickupDayOptionId { get; init; }
    public string PickupDayLabel { get; init; } = string.Empty;
    public Guid PickupHourOptionId { get; init; }
    public string PickupHourLabel { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record DepartureStatsDto
{
    public int TotalRegistrations { get; init; }
    public List<HotelStat> ByHotel { get; init; } = new();
    public List<DayStat> ByDay { get; init; } = new();
}

public record HotelStat { public Guid HotelId { get; init; } public string HotelName { get; init; } = string.Empty; public int Count { get; init; } }
public record DayStat { public Guid DayId { get; init; } public string DayLabel { get; init; } = string.Empty; public int DisplayOrder { get; init; } public int Count { get; init; } public List<HourStat> ByHour { get; init; } = new(); }
public record HourStat { public Guid HourId { get; init; } public string HourLabel { get; init; } = string.Empty; public int DisplayOrder { get; init; } public int Count { get; init; } public List<HotelStat> ByHotel { get; init; } = new(); }

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
