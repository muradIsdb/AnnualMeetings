using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    public SettingsController(IAppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
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
