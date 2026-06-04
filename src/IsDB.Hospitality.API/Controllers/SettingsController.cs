using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.Common.Models;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Infrastructure.BackgroundServices;
using IsDB.Hospitality.Infrastructure.ExternalClients.FlightTracker;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace IsDB.Hospitality.API.Controllers;

/// <summary>
/// Manages platform-level settings: Hotels, Pickup Days, Pickup Hours, and App Config.
/// All write operations are restricted to Administrator role.
/// Read operations (for the public departure form) are open.
/// </summary>
public class SettingsController : ApiControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly FlightTrackerSyncService? _flightSync;

    public SettingsController(IAppDbContext db, IWebHostEnvironment env, IEnumerable<IHostedService> hostedServices)
    {
        _db = db;
        _env = env;
        _flightSync = hostedServices.OfType<FlightTrackerSyncService>().FirstOrDefault();
    }

    // ─── APP CONFIG ───────────────────────────────────────────────────────────

    [HttpGet("config")]
    [AllowAnonymous]
    public async Task<ActionResult<AppConfigDto>> GetConfig()
    {
        var config = await _db.AppConfigs.FindAsync(1);
        if (config == null)
            return Ok(new AppConfigDto
            {
                EventTitle = "IsDB Annual Meetings 2025",
                MinimumLeadTimeHours = 2,
                EventTimezone = "Asia/Riyadh",
                PlaCardTheme = "Light",
                EventLogoUrl = null
            });

        return Ok(new AppConfigDto
        {
            EventTitle = config.EventTitle,
            MinimumLeadTimeHours = config.MinimumLeadTimeHours,
            EventTimezone = config.EventTimezone,
            PlaCardTheme = config.PlaCardTheme,
            EventLogoUrl = !string.IsNullOrEmpty(config.EventLogoBase64)
                ? $"data:{config.EventLogoMimeType ?? "image/png"};base64,{config.EventLogoBase64}"
                : config.EventLogoUrl
        });
    }

    [HttpPut("config")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AppConfigDto>> UpdateConfig([FromBody] AppConfigDto req)
    {
        var config = await _db.AppConfigs.FindAsync(1);
        if (config == null)
        {
            config = new IsDB.Hospitality.Domain.Entities.AppConfig { Id = 1 };
            _db.AppConfigs.Add(config);
        }
        config.EventTitle = req.EventTitle.Trim();
        config.MinimumLeadTimeHours = req.MinimumLeadTimeHours;
        config.EventTimezone = string.IsNullOrWhiteSpace(req.EventTimezone) ? "Asia/Riyadh" : req.EventTimezone.Trim();
        config.PlaCardTheme = req.PlaCardTheme is "Light" or "DarkNavy" ? req.PlaCardTheme : "Light";
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new AppConfigDto
        {
            EventTitle = config.EventTitle,
            MinimumLeadTimeHours = config.MinimumLeadTimeHours,
            EventTimezone = config.EventTimezone,
            PlaCardTheme = config.PlaCardTheme,
            EventLogoUrl = !string.IsNullOrEmpty(config.EventLogoBase64)
                ? $"data:{config.EventLogoMimeType ?? "image/png"};base64,{config.EventLogoBase64}"
                : config.EventLogoUrl
        });
    }

    // ─── EVENT LOGO ───────────────────────────────────────────────────────────

    [HttpPost("logo")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AppConfigDto>> UploadEventLogo(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        // Validate file type
        var allowedTypes = new[] { "image/png", "image/jpeg", "image/jpg", "image/svg+xml", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest("Only PNG, JPG, SVG, or WebP files are allowed.");

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest("File size must be under 5 MB.");

        // Read file into memory and convert to base64 for persistent DB storage
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        var mimeType = file.ContentType.ToLower();

        // Update config
        var config = await _db.AppConfigs.FindAsync(1);
        if (config == null)
        {
            config = new IsDB.Hospitality.Domain.Entities.AppConfig { Id = 1 };
            _db.AppConfigs.Add(config);
        }
        config.EventLogoBase64 = base64;
        config.EventLogoMimeType = mimeType;
        config.EventLogoUrl = null; // clear old file-path URL
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new AppConfigDto
        {
            EventTitle = config.EventTitle,
            MinimumLeadTimeHours = config.MinimumLeadTimeHours,
            EventTimezone = config.EventTimezone,
            PlaCardTheme = config.PlaCardTheme,
            EventLogoUrl = $"data:{mimeType};base64,{base64}"
        });
    }

    [HttpDelete("logo")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AppConfigDto>> DeleteEventLogo()
    {
        var config = await _db.AppConfigs.FindAsync(1);
        if (config == null)
            return NotFound();

        // Clear both base64 and legacy file-path URL
        config.EventLogoBase64 = null;
        config.EventLogoMimeType = null;
        config.EventLogoUrl = null;
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new AppConfigDto
        {
            EventTitle = config.EventTitle,
            MinimumLeadTimeHours = config.MinimumLeadTimeHours,
            EventTimezone = config.EventTimezone,
            PlaCardTheme = config.PlaCardTheme,
            EventLogoUrl = null
        });
    }

    // ─── HOTELS ──────────────────────────────────────────────────────────────

    [HttpGet("hotels")]
    [AllowAnonymous]
    public async Task<ActionResult<List<HotelOptionDto>>> GetHotels()
    {
        var hotels = await _db.HotelOptions
            .Where(h => h.IsActive)
            .OrderBy(h => h.DisplayOrder)
            .ThenBy(h => h.Name)
            .Select(h => new HotelOptionDto { Id = h.Id, Name = h.Name, IsActive = h.IsActive, DisplayOrder = h.DisplayOrder, ContractedRoomsIsDB = h.ContractedRoomsIsDB, ContractedRoomsGuest = h.ContractedRoomsGuest, ActualOccupiedIsDB = h.ActualOccupiedIsDB, ActualOccupiedGuest = h.ActualOccupiedGuest })
            .ToListAsync();
        return Ok(hotels);
    }

    [HttpGet("hotels/all")]
    [Authorize(Roles = "Admin,Hotel")]
    public async Task<ActionResult<List<HotelOptionDto>>> GetAllHotels()
    {
        var hotels = await _db.HotelOptions
            .OrderBy(h => h.DisplayOrder)
            .ThenBy(h => h.Name)
            .Select(h => new HotelOptionDto { Id = h.Id, Name = h.Name, IsActive = h.IsActive, DisplayOrder = h.DisplayOrder, ContractedRoomsIsDB = h.ContractedRoomsIsDB, ContractedRoomsGuest = h.ContractedRoomsGuest, ActualOccupiedIsDB = h.ActualOccupiedIsDB, ActualOccupiedGuest = h.ActualOccupiedGuest })
            .ToListAsync();
        return Ok(hotels);
    }

    [HttpPost("hotels")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<HotelOptionDto>> CreateHotel([FromBody] CreateHotelRequest req)
    {
        var hotel = new HotelOption
        {
            Name = req.Name.Trim(),
            IsActive = req.IsActive,
            DisplayOrder = req.DisplayOrder
        };
        _db.HotelOptions.Add(hotel);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetHotels), new HotelOptionDto { Id = hotel.Id, Name = hotel.Name, IsActive = hotel.IsActive, DisplayOrder = hotel.DisplayOrder, ContractedRoomsIsDB = hotel.ContractedRoomsIsDB, ContractedRoomsGuest = hotel.ContractedRoomsGuest, ActualOccupiedIsDB = hotel.ActualOccupiedIsDB, ActualOccupiedGuest = hotel.ActualOccupiedGuest });
    }

    [HttpPut("hotels/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<HotelOptionDto>> UpdateHotel(Guid id, [FromBody] CreateHotelRequest req)
    {
        var hotel = await _db.HotelOptions.FindAsync(id);
        if (hotel == null) return NotFound();
        hotel.Name = req.Name.Trim();
        hotel.IsActive = req.IsActive;
        hotel.DisplayOrder = req.DisplayOrder;
        await _db.SaveChangesAsync();
        return Ok(new HotelOptionDto { Id = hotel.Id, Name = hotel.Name, IsActive = hotel.IsActive, DisplayOrder = hotel.DisplayOrder, ContractedRoomsIsDB = hotel.ContractedRoomsIsDB, ContractedRoomsGuest = hotel.ContractedRoomsGuest, ActualOccupiedIsDB = hotel.ActualOccupiedIsDB, ActualOccupiedGuest = hotel.ActualOccupiedGuest });
    }

    /// <summary>Hotel team: update contracted rooms and actual occupancy only.</summary>
    [HttpPatch("hotels/{id:guid}/rooms")]
    [Authorize(Roles = "Admin,Hotel")]
    public async Task<ActionResult<HotelOptionDto>> UpdateHotelRooms(Guid id, [FromBody] UpdateHotelRoomsRequest req)
    {
        var hotel = await _db.HotelOptions.FindAsync(id);
        if (hotel == null) return NotFound();
        hotel.ContractedRoomsIsDB = req.ContractedRoomsIsDB;
        hotel.ContractedRoomsGuest = req.ContractedRoomsGuest;
        hotel.ActualOccupiedIsDB = req.ActualOccupiedIsDB;
        hotel.ActualOccupiedGuest = req.ActualOccupiedGuest;
        hotel.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new HotelOptionDto { Id = hotel.Id, Name = hotel.Name, IsActive = hotel.IsActive, DisplayOrder = hotel.DisplayOrder, ContractedRoomsIsDB = hotel.ContractedRoomsIsDB, ContractedRoomsGuest = hotel.ContractedRoomsGuest, ActualOccupiedIsDB = hotel.ActualOccupiedIsDB, ActualOccupiedGuest = hotel.ActualOccupiedGuest });
    }

    [HttpDelete("hotels/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteHotel(Guid id)
    {
        var hotel = await _db.HotelOptions.FindAsync(id);
        if (hotel == null) return NotFound();
        _db.HotelOptions.Remove(hotel);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── PICKUP DAYS ─────────────────────────────────────────────────────────

    [HttpGet("pickup-days")]
    [AllowAnonymous]
    public async Task<ActionResult<List<PickupDayDto>>> GetPickupDays()
    {
        var days = await _db.PickupDayOptions
            .Where(d => d.IsActive)
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.Value)
            .Select(d => new PickupDayDto { Id = d.Id, Label = d.Label, Value = d.Value, IsActive = d.IsActive, DisplayOrder = d.DisplayOrder })
            .ToListAsync();
        return Ok(days);
    }

    [HttpGet("pickup-days/all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<PickupDayDto>>> GetAllPickupDays()
    {
        var days = await _db.PickupDayOptions
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.Value)
            .Select(d => new PickupDayDto { Id = d.Id, Label = d.Label, Value = d.Value, IsActive = d.IsActive, DisplayOrder = d.DisplayOrder })
            .ToListAsync();
        return Ok(days);
    }

    [HttpPost("pickup-days")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PickupDayDto>> CreatePickupDay([FromBody] CreatePickupDayRequest req)
    {
        var day = new PickupDayOption
        {
            Label = req.Label.Trim(),
            Value = req.Value.Trim(),
            IsActive = req.IsActive,
            DisplayOrder = req.DisplayOrder
        };
        _db.PickupDayOptions.Add(day);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPickupDays), new PickupDayDto { Id = day.Id, Label = day.Label, Value = day.Value, IsActive = day.IsActive, DisplayOrder = day.DisplayOrder });
    }

    [HttpPut("pickup-days/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PickupDayDto>> UpdatePickupDay(Guid id, [FromBody] CreatePickupDayRequest req)
    {
        var day = await _db.PickupDayOptions.FindAsync(id);
        if (day == null) return NotFound();
        day.Label = req.Label.Trim();
        day.Value = req.Value.Trim();
        day.IsActive = req.IsActive;
        day.DisplayOrder = req.DisplayOrder;
        await _db.SaveChangesAsync();
        return Ok(new PickupDayDto { Id = day.Id, Label = day.Label, Value = day.Value, IsActive = day.IsActive, DisplayOrder = day.DisplayOrder });
    }

    [HttpDelete("pickup-days/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePickupDay(Guid id)
    {
        var day = await _db.PickupDayOptions.FindAsync(id);
        if (day == null) return NotFound();
        _db.PickupDayOptions.Remove(day);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── PICKUP HOURS ─────────────────────────────────────────────────────────

    [HttpGet("pickup-hours")]
    [AllowAnonymous]
    public async Task<ActionResult<List<PickupHourDto>>> GetPickupHours()
    {
        var hours = await _db.PickupHourOptions
            .Where(h => h.IsActive)
            .OrderBy(h => h.DisplayOrder)
            .ThenBy(h => h.Value)
            .Select(h => new PickupHourDto { Id = h.Id, Label = h.Label, Value = h.Value, IsActive = h.IsActive, DisplayOrder = h.DisplayOrder })
            .ToListAsync();
        return Ok(hours);
    }

    [HttpGet("pickup-hours/all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<PickupHourDto>>> GetAllPickupHours()
    {
        var hours = await _db.PickupHourOptions
            .OrderBy(h => h.DisplayOrder)
            .ThenBy(h => h.Value)
            .Select(h => new PickupHourDto { Id = h.Id, Label = h.Label, Value = h.Value, IsActive = h.IsActive, DisplayOrder = h.DisplayOrder })
            .ToListAsync();
        return Ok(hours);
    }

    [HttpPost("pickup-hours")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PickupHourDto>> CreatePickupHour([FromBody] CreatePickupHourRequest req)
    {
        var hour = new PickupHourOption
        {
            Label = req.Label.Trim(),
            Value = req.Value.Trim(),
            IsActive = req.IsActive,
            DisplayOrder = req.DisplayOrder
        };
        _db.PickupHourOptions.Add(hour);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPickupHours), new PickupHourDto { Id = hour.Id, Label = hour.Label, Value = hour.Value, IsActive = hour.IsActive, DisplayOrder = hour.DisplayOrder });
    }

    [HttpPut("pickup-hours/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PickupHourDto>> UpdatePickupHour(Guid id, [FromBody] CreatePickupHourRequest req)
    {
        var hour = await _db.PickupHourOptions.FindAsync(id);
        if (hour == null) return NotFound();
        hour.Label = req.Label.Trim();
        hour.Value = req.Value.Trim();
        hour.IsActive = req.IsActive;
        hour.DisplayOrder = req.DisplayOrder;
        await _db.SaveChangesAsync();
        return Ok(new PickupHourDto { Id = hour.Id, Label = hour.Label, Value = hour.Value, IsActive = hour.IsActive, DisplayOrder = hour.DisplayOrder });
    }

    [HttpDelete("pickup-hours/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePickupHour(Guid id)
    {
        var hour = await _db.PickupHourOptions.FindAsync(id);
        if (hour == null) return NotFound();
        _db.PickupHourOptions.Remove(hour);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── FLIGHT TRACKING (AVIATIONSTACK) ─────────────────────────────────────

    [HttpGet("flight-tracking")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FlightTrackingConfigDto>> GetFlightTrackingConfig(
        [FromServices] IOptions<AviationstackOptions> optionsAccessor)
    {
        var config = await _db.AppConfigs.FindAsync(1);
        var opts = optionsAccessor.Value;

        // Determine effective API key and its source
        string? dbKey = config?.AviationstackApiKey;
        bool hasDbKey = !string.IsNullOrWhiteSpace(dbKey);
        bool hasEnvKey = !string.IsNullOrWhiteSpace(opts.ApiKey)
                         && opts.ApiKey != "REPLACE_WITH_AVIATIONSTACK_API_KEY";

        string? effectiveKey = hasDbKey ? dbKey : (hasEnvKey ? opts.ApiKey : null);
        string configSource = hasDbKey ? "database" : (hasEnvKey ? "environment" : "none");

        // Mask: show only last 4 characters
        string? masked = null;
        if (!string.IsNullOrWhiteSpace(effectiveKey) && effectiveKey.Length >= 4)
            masked = new string('•', effectiveKey.Length - 4) + effectiveKey[^4..];
        else if (!string.IsNullOrWhiteSpace(effectiveKey))
            masked = new string('•', effectiveKey.Length);

        return Ok(new FlightTrackingConfigDto
        {
            ApiKeyMasked = masked,
            IsConfigured = !string.IsNullOrWhiteSpace(effectiveKey),
            SyncIntervalMinutes = config?.AviationstackSyncIntervalMinutes > 0
                ? config.AviationstackSyncIntervalMinutes
                : opts.SyncIntervalMinutes,
            TrackingWindowHours = config?.AviationstackTrackingWindowHours > 0
                ? config.AviationstackTrackingWindowHours
                : opts.TrackingWindowHours,
            DateGuardDays = config?.AviationstackDateGuardDays != null
                ? config.AviationstackDateGuardDays  // 0 is valid: exact date match only
                : opts.DateGuardDays,
            ConfigSource = configSource
        });
    }

    [HttpPut("flight-tracking")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FlightTrackingConfigDto>> UpdateFlightTrackingConfig(
        [FromBody] UpdateFlightTrackingConfigRequest req,
        [FromServices] IOptions<AviationstackOptions> optionsAccessor)
    {
        var config = await _db.AppConfigs.FindAsync(1);
        if (config == null)
        {
            config = new IsDB.Hospitality.Domain.Entities.AppConfig { Id = 1 };
            _db.AppConfigs.Add(config);
        }

        // Only update API key if a non-empty value was provided
        if (!string.IsNullOrWhiteSpace(req.ApiKey))
            config.AviationstackApiKey = req.ApiKey.Trim();

        if (req.SyncIntervalMinutes > 0)
            config.AviationstackSyncIntervalMinutes = req.SyncIntervalMinutes;

        if (req.TrackingWindowHours > 0)
            config.AviationstackTrackingWindowHours = req.TrackingWindowHours;

        if (req.DateGuardDays >= 0)  // 0 is valid: means exact date match, no tolerance
            config.AviationstackDateGuardDays = req.DateGuardDays;

        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Return updated config (re-use GET logic)
        return await GetFlightTrackingConfig(HttpContext.RequestServices.GetRequiredService<IOptions<AviationstackOptions>>());
    }

    [HttpPost("flight-tracking/test")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TestFlightTrackingResult>> TestFlightTrackingConnection(
        [FromBody] TestFlightTrackingRequest req,
        [FromServices] IOptions<AviationstackOptions> optionsAccessor)
    {
        // Determine which key to test
        string? keyToTest = req.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(keyToTest))
        {
            var config = await _db.AppConfigs.FindAsync(1);
            keyToTest = config?.AviationstackApiKey?.Trim();
        }
        if (string.IsNullOrWhiteSpace(keyToTest))
            keyToTest = optionsAccessor.Value.ApiKey?.Trim();

        if (string.IsNullOrWhiteSpace(keyToTest) || keyToTest == "REPLACE_WITH_AVIATIONSTACK_API_KEY")
            return Ok(new TestFlightTrackingResult
            {
                Success = false,
                Message = "No API key provided. Enter a key and try again."
            });

        // Call AviationStack API to validate the key
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var baseUrl = optionsAccessor.Value.BaseUrl?.TrimEnd('/') ?? "http://api.aviationstack.com/v1";
            var url = $"{baseUrl}/flights?access_key={Uri.EscapeDataString(keyToTest)}&limit=1";
            var response = await httpClient.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return Ok(new TestFlightTrackingResult
                {
                    Success = false,
                    Message = $"AviationStack returned HTTP {(int)response.StatusCode}. Check your API key."
                });

            // Try to extract plan info from the response
            string? plan = null;
            int? quota = null;
            int? used = null;
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("pagination", out var pagination))
                {
                    if (pagination.TryGetProperty("total", out var total))
                        used = total.GetInt32();
                }
                // AviationStack free plan returns data even on first call
                if (json.RootElement.TryGetProperty("data", out _))
                    plan = "Connected";
            }
            catch { /* ignore JSON parse errors */ }

            return Ok(new TestFlightTrackingResult
            {
                Success = true,
                Message = "Connection successful. AviationStack API key is valid.",
                Plan = plan,
                MonthlyQuota = quota,
                QuotaUsed = used
            });
        }
        catch (Exception ex)
        {
            return Ok(new TestFlightTrackingResult
            {
                Success = false,
                Message = $"Connection failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Triggers an immediate AviationStack sync cycle outside the normal timer.
    /// Returns a summary of flights polled and updated.
    /// </summary>
     [HttpPost("flight-tracking/sync-now")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SyncNowResult>> SyncFlightsNow()
    {
        if (_flightSync == null)
            return StatusCode(503, new SyncNowResult { Success = false, Message = "Flight sync service is not running." });
        try
        {
            var staffName = User.Identity?.Name ?? "Admin";
            var result = await _flightSync.TriggerSyncNowAsync(HttpContext.RequestAborted, staffName);
            return Ok(new SyncNowResult
            {
                Success = true,
                FlightsTracked = result.FlightsTracked,
                FlightsUpdated = result.FlightsUpdated,
                Message = result.Message
            });
        }
        catch (Exception ex)
        {
            return Ok(new SyncNowResult { Success = false, Message = $"Sync failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Returns paginated AviationStack sync history log entries.
    /// </summary>
    [HttpGet("flight-tracking/sync-logs")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetFlightSyncLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.FlightSyncLogs.AsQueryable();
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(l => l.SyncedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new FlightSyncLogDto
            {
                Id = l.Id,
                SyncedAt = l.SyncedAt,
                TriggerSource = l.TriggerSource,
                Status = l.Status,
                FlightsInWindow = l.FlightsInWindow,
                FlightsQueried = l.FlightsQueried,
                FlightsUpdated = l.FlightsUpdated,
                DurationMs = l.DurationMs,
                Message = l.Message,
                InitiatedByStaffName = l.InitiatedByStaffName
            })
            .ToListAsync();

        return Ok(new { items, totalCount, totalPages, page, pageSize });
    }

    /// <summary>
    /// Debug: sync a single flight by flight number and return detailed before/after comparison.
    /// </summary>
    [HttpPost("flight-tracking/debug-sync/{flightNumber}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DebugSyncFlight(
        string flightNumber,
        [FromServices] IsDB.Hospitality.Infrastructure.Persistence.AppDbContext db,
        [FromServices] IFlightTrackerClient flightTracker,
        CancellationToken ct)
    {
        var flight = await db.Flights
            .Include(f => f.TravelBookings).ThenInclude(tb => tb.Guest)
            .FirstOrDefaultAsync(f => f.FlightNumber == flightNumber, ct);

        if (flight == null)
            return NotFound(new { message = $"Flight '{flightNumber}' not found in DB" });

        var before = new
        {
            flight.FlightNumber,
            flight.ScheduledArrival,
            Status = flight.Status.ToString(),
            flight.ActualTerminal,
            flight.ActualGate,
            flight.LiveDelayMinutes,
            flight.LastTrackedAt,
            GuestCount = flight.TravelBookings.Count,
            Guests = flight.TravelBookings.Select(tb => new { tb.Guest.FirstName, tb.Guest.LastName, GuestStatus = tb.Guest.Status.ToString() }).ToList()
        };

        // Read the effective API key from DB (same logic as the background sync service)
        var dbConfig = await db.AppConfigs.FindAsync(new object[] { 1 }, ct);
        var effectiveApiKey = !string.IsNullOrWhiteSpace(dbConfig?.AviationstackApiKey)
            ? dbConfig.AviationstackApiKey
            : null;

        FlightStatusDto? status = null;
        string? apiError = null;
        var flightDateOnly = DateOnly.FromDateTime(flight.ScheduledArrival.Date);
        try { status = await flightTracker.GetFlightStatusAsync(flightNumber, flightDateOnly, ct, effectiveApiKey); }
        catch (Exception ex) { apiError = ex.Message; }

        if (status == null)
            return Ok(new { before, apiResult = (object?)null, apiError = apiError ?? "GetFlightStatusAsync returned null", updated = false });

        string? dateGuardReason = null;
        if (status.ScheduledArrival.HasValue)
        {
            var dayDiff = Math.Abs((status.ScheduledArrival.Value.Date - flight.ScheduledArrival.Date).TotalDays);
            if (dayDiff > 1)
                dateGuardReason = $"Date guard: AviationStack={status.ScheduledArrival.Value.Date:yyyy-MM-dd}, DB={flight.ScheduledArrival.Date:yyyy-MM-dd}, diff={dayDiff}d";
        }

        var apiResult = new
        {
            status.FlightNumber,
            status.Status,
            status.ScheduledArrival,
            status.ActualArrival,
            status.Terminal,
            status.Gate,
            status.DelayMinutes
        };

        if (dateGuardReason != null)
            return Ok(new { before, apiResult, dateGuardReason, updated = false });

        bool changed = false;
        var newStatus = ParseFlightStatusDebug(status.Status);
        if (flight.Status != newStatus) { flight.Status = newStatus; changed = true; }
        if (status.ActualArrival.HasValue && flight.ActualArrival != status.ActualArrival) { flight.ActualArrival = status.ActualArrival; changed = true; }
        if (status.ActualDeparture.HasValue && flight.ActualDeparture != status.ActualDeparture) { flight.ActualDeparture = status.ActualDeparture; changed = true; }
        if (status.Terminal != null && flight.ActualTerminal != status.Terminal) { flight.ActualTerminal = status.Terminal; changed = true; }
        if (status.Gate != null && flight.ActualGate != status.Gate) { flight.ActualGate = status.Gate; changed = true; }
        if (status.DelayMinutes.HasValue && flight.LiveDelayMinutes != status.DelayMinutes) { flight.LiveDelayMinutes = status.DelayMinutes; changed = true; }
        flight.LastTrackedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var after = new
        {
            flight.FlightNumber,
            flight.ScheduledArrival,
            Status = flight.Status.ToString(),
            flight.ActualTerminal,
            flight.ActualGate,
            flight.LiveDelayMinutes,
            flight.LastTrackedAt
        };
        return Ok(new { before, apiResult, dateGuardReason = (string?)null, changed, updated = changed, after });
    }

    private static IsDB.Hospitality.Domain.Enums.FlightStatus ParseFlightStatusDebug(string? s) => s?.ToLower() switch
    {
        "active" => IsDB.Hospitality.Domain.Enums.FlightStatus.Active,
        "landed" => IsDB.Hospitality.Domain.Enums.FlightStatus.Landed,
        "cancelled" => IsDB.Hospitality.Domain.Enums.FlightStatus.Cancelled,
        "diverted" => IsDB.Hospitality.Domain.Enums.FlightStatus.Diverted,
        "scheduled" => IsDB.Hospitality.Domain.Enums.FlightStatus.Scheduled,
        _ => IsDB.Hospitality.Domain.Enums.FlightStatus.Unknown
    };
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record HotelOptionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int DisplayOrder { get; init; }
    public int ContractedRoomsIsDB { get; init; }
    public int ContractedRoomsGuest { get; init; }
    public int ActualOccupiedIsDB { get; init; }
    public int ActualOccupiedGuest { get; init; }
}

public record UpdateHotelRoomsRequest
{
    public int ContractedRoomsIsDB { get; init; }
    public int ContractedRoomsGuest { get; init; }
    public int ActualOccupiedIsDB { get; init; }
    public int ActualOccupiedGuest { get; init; }
}

public record CreateHotelRequest
{
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public int DisplayOrder { get; init; } = 0;
}

public record PickupDayDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int DisplayOrder { get; init; }
}

public record CreatePickupDayRequest
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public int DisplayOrder { get; init; } = 0;
}

public record PickupHourDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int DisplayOrder { get; init; }
}

public record CreatePickupHourRequest
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public int DisplayOrder { get; init; } = 0;
}

public record AppConfigDto
{
    public string EventTitle { get; init; } = "IsDB Annual Meetings 2025";
    public int MinimumLeadTimeHours { get; init; } = 2;
    /// <summary>IANA timezone identifier for the event location (e.g. "Asia/Riyadh").</summary>
    public string EventTimezone { get; init; } = "Asia/Riyadh";
    /// <summary>Placard theme: "Light" or "DarkNavy".</summary>
    public string PlaCardTheme { get; init; } = "Light";
    /// <summary>URL of the uploaded event logo. Null means use default IsDB logo.</summary>
    public string? EventLogoUrl { get; init; }
}

// ─── FLIGHT TRACKING DTOs ─────────────────────────────────────────────────────

public record FlightTrackingConfigDto
{
    /// <summary>Masked API key — last 4 chars visible, rest replaced with bullets.</summary>
    public string? ApiKeyMasked { get; init; }
    /// <summary>Whether an API key is currently configured (in DB or env var).</summary>
    public bool IsConfigured { get; init; }
    public int SyncIntervalMinutes { get; init; } = 5;
    public int TrackingWindowHours { get; init; } = 12;
    /// <summary>Maximum day difference tolerated between AviationStack result and DB flight date.</summary>
    public int DateGuardDays { get; init; } = 1;
    /// <summary>Source of the current config: "database", "environment", or "none".</summary>
    public string ConfigSource { get; init; } = "none";
}

public record UpdateFlightTrackingConfigRequest
{
    /// <summary>New API key. Null or empty means "keep existing key".</summary>
    public string? ApiKey { get; init; }
    public int SyncIntervalMinutes { get; init; } = 5;
    public int TrackingWindowHours { get; init; } = 12;
    /// <summary>Maximum day difference tolerated between AviationStack result and DB flight date. Default 1.</summary>
    public int DateGuardDays { get; init; } = 1;
}

public record TestFlightTrackingRequest
{
    /// <summary>API key to test. If null, uses the currently saved key.</summary>
    public string? ApiKey { get; init; }
}

public record TestFlightTrackingResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Plan { get; init; }
    public int? MonthlyQuota { get; init; }
    public int? QuotaUsed { get; init; }
}
public record SyncNowResult
{
    public bool Success { get; init; }
    public int FlightsTracked { get; init; }
    public int FlightsUpdated { get; init; }
    public string Message { get; init; } = string.Empty;
}

public record FlightSyncLogDto
{
    public Guid Id { get; init; }
    public DateTime SyncedAt { get; init; }
    public string TriggerSource { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int FlightsInWindow { get; init; }
    public int FlightsQueried { get; init; }
    public int FlightsUpdated { get; init; }
    public int DurationMs { get; init; }
    public string? Message { get; init; }
    public string? InitiatedByStaffName { get; init; }
}
