using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

/// <summary>
/// Manages platform-level settings: Hotels, Pickup Days, and Pickup Hours.
/// All write operations are restricted to Administrator role.
/// Read operations (for the public departure form) are open.
/// </summary>
public class SettingsController : ApiControllerBase
{
    private readonly IAppDbContext _db;

    public SettingsController(IAppDbContext db)
    {
        _db = db;
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
            .Select(h => new HotelOptionDto { Id = h.Id, Name = h.Name, IsActive = h.IsActive, DisplayOrder = h.DisplayOrder })
            .ToListAsync();
        return Ok(hotels);
    }

    [HttpGet("hotels/all")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<List<HotelOptionDto>>> GetAllHotels()
    {
        var hotels = await _db.HotelOptions
            .OrderBy(h => h.DisplayOrder)
            .ThenBy(h => h.Name)
            .Select(h => new HotelOptionDto { Id = h.Id, Name = h.Name, IsActive = h.IsActive, DisplayOrder = h.DisplayOrder })
            .ToListAsync();
        return Ok(hotels);
    }

    [HttpPost("hotels")]
    [Authorize(Roles = "Administrator")]
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
        return CreatedAtAction(nameof(GetHotels), new HotelOptionDto { Id = hotel.Id, Name = hotel.Name, IsActive = hotel.IsActive, DisplayOrder = hotel.DisplayOrder });
    }

    [HttpPut("hotels/{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<HotelOptionDto>> UpdateHotel(Guid id, [FromBody] CreateHotelRequest req)
    {
        var hotel = await _db.HotelOptions.FindAsync(id);
        if (hotel == null) return NotFound();
        hotel.Name = req.Name.Trim();
        hotel.IsActive = req.IsActive;
        hotel.DisplayOrder = req.DisplayOrder;
        await _db.SaveChangesAsync();
        return Ok(new HotelOptionDto { Id = hotel.Id, Name = hotel.Name, IsActive = hotel.IsActive, DisplayOrder = hotel.DisplayOrder });
    }

    [HttpDelete("hotels/{id:guid}")]
    [Authorize(Roles = "Administrator")]
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
    [Authorize(Roles = "Administrator")]
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
    [Authorize(Roles = "Administrator")]
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
    [Authorize(Roles = "Administrator")]
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
    [Authorize(Roles = "Administrator")]
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
    [Authorize(Roles = "Administrator")]
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
    [Authorize(Roles = "Administrator")]
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
    [Authorize(Roles = "Administrator")]
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
    [Authorize(Roles = "Administrator")]
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
