using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[Authorize]
public class CarClassesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    public CarClassesController(AppDbContext db) { _db = db; }

    private async Task<string?> GetActiveEventCodeAsync() =>
        (await _db.EventsAirConfigs.FirstOrDefaultAsync())?.EventCode;

    // GET /api/car-classes
    [HttpGet]
    public async Task<ActionResult<List<object>>> GetAll()
    {
        var activeEventCode = await GetActiveEventCodeAsync();
        var classes = await _db.CarClasses
            .Where(c => c.EventCode == null || c.EventCode == activeEventCode)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync();

        // Use separate count queries per class to avoid GroupBy on nullable Guid (PostgreSQL compatibility)
        var result = new List<object>();
        foreach (var c in classes)
        {
            var vehicleCount = await _db.Vehicles.CountAsync(v => v.IsActive && v.CarClassId == c.Id);
            var guestCount   = await _db.Guests.CountAsync(g => g.IsActive && g.DeservedCarClassId == c.Id);
            result.Add(new
            {
                c.Id,
                c.Name,
                c.Description,
                c.Color,
                c.SortOrder,
                VehicleCount = vehicleCount,
                GuestCount   = guestCount,
            });
        }

        return Ok(result);
    }

    // GET /api/car-classes/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> GetById(Guid id)
    {
        var c = await _db.CarClasses.FindAsync(id);
        if (c == null) return NotFound();

        var vehicleCount = await _db.Vehicles.CountAsync(v => v.IsActive && v.CarClassId == id);
        var guestCount   = await _db.Guests.CountAsync(g => g.IsActive && g.DeservedCarClassId == id);

        return Ok(new
        {
            c.Id,
            c.Name,
            c.Description,
            c.Color,
            c.SortOrder,
            VehicleCount = vehicleCount,
            GuestCount   = guestCount,
        });
    }

    // POST /api/car-classes
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Create([FromBody] CarClassRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Name is required." });

        var activeEventCode = await GetActiveEventCodeAsync();
        if (await _db.CarClasses.AnyAsync(c =>
            (c.EventCode == null || c.EventCode == activeEventCode) &&
            c.Name.ToLower() == req.Name.Trim().ToLower()))
            return BadRequest(new { message = "A car class with this name already exists." });
        var carClass = new CarClass
        {
            Id          = Guid.NewGuid(),
            Name        = req.Name.Trim(),
            Description = req.Description?.Trim(),
            Color       = req.Color?.Trim(),
            SortOrder   = req.SortOrder ?? 0,
            EventCode   = activeEventCode,
        };

        _db.CarClasses.Add(carClass);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = carClass.Id }, new { carClass.Id });
    }

    // PUT /api/car-classes/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CarClassRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Name is required." });

        var carClass = await _db.CarClasses.FindAsync(id);
        if (carClass == null) return NotFound();

        // Check name uniqueness (excluding self, scoped to active event)
        var activeEventCode2 = await GetActiveEventCodeAsync();
        if (await _db.CarClasses.AnyAsync(c =>
            c.Id != id &&
            (c.EventCode == null || c.EventCode == activeEventCode2) &&
            c.Name.ToLower() == req.Name.Trim().ToLower()))
            return BadRequest(new { message = "A car class with this name already exists." });

        carClass.Name        = req.Name.Trim();
        carClass.Description = req.Description?.Trim();
        carClass.Color       = req.Color?.Trim();
        carClass.SortOrder   = req.SortOrder ?? carClass.SortOrder;

        await _db.SaveChangesAsync();
        return Ok(new { carClass.Id });
    }

    // DELETE /api/car-classes/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var carClass = await _db.CarClasses.FindAsync(id);
        if (carClass == null) return NotFound();

        // Check if any vehicles or guests are still using this class
        var vehicleCount = await _db.Vehicles.CountAsync(v => v.CarClassId == id && v.IsActive);
        var guestCount   = await _db.Guests.CountAsync(g => g.DeservedCarClassId == id && g.IsActive);

        if (vehicleCount > 0 || guestCount > 0)
            return BadRequest(new
            {
                message = $"Cannot delete: {vehicleCount} vehicle(s) and {guestCount} guest(s) are still assigned to this class. Reassign them first."
            });

        _db.CarClasses.Remove(carClass);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/car-classes/{id}/history — provision history for all vehicles in a class
    [HttpGet("{id:guid}/history")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<IActionResult> GetHistory(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null)
    {
        var carClass = await _db.CarClasses.FindAsync(id);
        if (carClass == null) return NotFound();

        var query = _db.VehicleStatusHistories
            .Include(h => h.Vehicle)
            .Where(h => h.Vehicle.CarClassId == id);

        if (!string.IsNullOrWhiteSpace(fromDate) && DateTime.TryParse(fromDate, out var from))
            query = query.Where(h => h.CreatedAt >= from.ToUniversalTime());

        if (!string.IsNullOrWhiteSpace(toDate) && DateTime.TryParse(toDate, out var to))
            query = query.Where(h => h.CreatedAt <= to.ToUniversalTime().AddDays(1));

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(h => h.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new
            {
                h.Id,
                VehicleId       = h.VehicleId,
                VehiclePlate    = h.Vehicle.LicensePlate,
                VehicleMake     = h.Vehicle.Make,
                VehicleModel    = h.Vehicle.Model,
                OldStatus       = h.OldStatus.ToString(),
                NewStatus       = h.NewStatus.ToString(),
                h.ChangedByName,
                ChangedByRole   = h.ChangedByRole.HasValue ? h.ChangedByRole.ToString() : null,
                h.Notes,
                ChangedAt       = h.CreatedAt,
            })
            .ToListAsync();

        return Ok(new
        {
            CarClassId   = id,
            CarClassName = carClass.Name,
            Total        = total,
            Page         = page,
            PageSize     = pageSize,
            Items        = items,
        });
    }

    // PATCH /api/car-classes/reorder  — update sort orders in bulk
    [HttpPatch("reorder")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reorder([FromBody] List<ReorderItem> items)
    {
        foreach (var item in items)
        {
            var c = await _db.CarClasses.FindAsync(item.Id);
            if (c != null) c.SortOrder = item.SortOrder;
        }
        await _db.SaveChangesAsync();
        return Ok();
    }
}

public record CarClassRequest(string Name, string? Description, string? Color, int? SortOrder);
public record ReorderItem(Guid Id, int SortOrder);
