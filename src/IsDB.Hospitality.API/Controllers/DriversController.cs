using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[Authorize(Roles = "Admin,Transport")]
public class DriversController : ApiControllerBase
{
    private readonly AppDbContext _db;
    public DriversController(AppDbContext db) { _db = db; }

    // GET /api/drivers
    [HttpGet]
    public async Task<ActionResult<List<object>>> GetAll()
    {
        var drivers = await _db.Drivers
            .Where(d => d.IsActive)
            .Include(d => d.Vehicle)
            .OrderBy(d => d.FullName)
            .ToListAsync();

        return Ok(drivers.Select(d => (object)new
        {
            d.Id, d.FullName, d.Phone,
            Status = d.Status.ToString(),
            d.IsActive,
            VehicleId = d.VehicleId,
            VehiclePlate = d.Vehicle?.LicensePlate,
            VehicleMake = d.Vehicle?.Make,
            VehicleModel = d.Vehicle?.Model,
        }).ToList());
    }

    // GET /api/drivers/search?q=
    [HttpGet("search")]
    public async Task<ActionResult<List<object>>> Search([FromQuery] string q = "")
    {
        var query = q.Trim().ToLower();
        var drivers = await _db.Drivers
            .Where(d => d.IsActive && (
                string.IsNullOrEmpty(query) ||
                d.FullName.ToLower().Contains(query) ||
                d.Phone.Contains(query)
            ))
            .Include(d => d.Vehicle)
            .OrderBy(d => d.FullName)
            .Take(10)
            .ToListAsync();

        return Ok(drivers.Select(d => (object)new
        {
            d.Id, d.FullName, d.Phone,
            Status = d.Status.ToString(),
            VehicleId = d.VehicleId,
            VehiclePlate = d.Vehicle?.LicensePlate,
        }).ToList());
    }

    // GET /api/drivers/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> GetById(Guid id)
    {
        var d = await _db.Drivers.Include(x => x.Vehicle).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return NotFound();
        return Ok(new
        {
            d.Id, d.FullName, d.Phone, Status = d.Status.ToString(), d.IsActive,
            VehicleId = d.VehicleId, VehiclePlate = d.Vehicle?.LicensePlate,
        });
    }

    // POST /api/drivers
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateDriverRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Phone))
            return BadRequest(new { message = "Full name and phone are required." });

        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            FullName = req.FullName.Trim(),
            Phone = req.Phone.Trim(),
            Status = DriverStatus.Available,
            IsActive = true,
        };
        _db.Drivers.Add(driver);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = driver.Id }, new { driver.Id });
    }

    // PUT /api/drivers/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateDriverRequest req)
    {
        var d = await _db.Drivers.FindAsync(id);
        if (d == null) return NotFound();
        d.FullName = req.FullName.Trim();
        d.Phone = req.Phone.Trim();
        await _db.SaveChangesAsync();
        return Ok(new { d.Id });
    }

    // DELETE /api/drivers/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var d = await _db.Drivers.FindAsync(id);
        if (d == null) return NotFound();

        // Block deletion if driver is currently in an active guest assignment
        if (d.Status == DriverStatus.Assigned)
            return BadRequest(new { message = "Cannot delete a driver who is currently assigned to a guest. Unassign the vehicle first." });

        // Clear the vehicle link so the vehicle no longer shows this driver
        if (d.VehicleId.HasValue)
        {
            var vehicle = await _db.Vehicles.FindAsync(d.VehicleId.Value);
            if (vehicle != null) vehicle.DriverId = null;
            d.VehicleId = null;
        }

        d.IsActive = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/drivers/import
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportCsv(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        using var reader = new System.IO.StreamReader(file.OpenReadStream());
        var header = await reader.ReadLineAsync();
        if (header == null)
            return BadRequest(new { message = "File is empty." });

        var cols = header.Split(',').Select(c => c.Trim().ToLower()).ToArray();
        int iName = Array.IndexOf(cols, "fullname") >= 0 ? Array.IndexOf(cols, "fullname") : Array.IndexOf(cols, "name");
        int iPhone = Array.IndexOf(cols, "phone");

        if (iName < 0 || iPhone < 0)
            return BadRequest(new { message = "CSV must have columns: FullName (or Name) and Phone." });

        int lineNum = 1;
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length <= Math.Max(iName, iPhone))
            {
                errors.Add($"Line {lineNum}: not enough columns.");
                skipped++;
                continue;
            }

            var fullName = parts[iName].Trim();
            var phone = parts[iPhone].Trim();
            // Skip comment lines (lines starting with #)
            if (fullName.StartsWith("#") || phone.StartsWith("#")) { skipped++; continue; }
            // Normalize scientific notation phone numbers (e.g. 9.66501E+11 → 966501000000)
            if (System.Text.RegularExpressions.Regex.IsMatch(phone, @"^[0-9.]+[eE][+\-]?[0-9]+$"))
            {
                if (double.TryParse(phone, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var numPhone))
                    phone = ((long)Math.Round(numPhone)).ToString();
            }

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(phone))
            {
                errors.Add($"Line {lineNum}: FullName and Phone are required.");
                skipped++;
                continue;
            }

            // Skip duplicates by phone number
            if (await _db.Drivers.AnyAsync(d => d.Phone == phone && d.IsActive))
            {
                errors.Add($"Line {lineNum}: Phone '{phone}' already exists — skipped.");
                skipped++;
                continue;
            }

            _db.Drivers.Add(new Driver
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Phone = phone,
                Status = DriverStatus.Available,
                IsActive = true,
            });
            imported++;
        }

        await _db.SaveChangesAsync();
        return Ok(new { imported, skipped, errors });
    }

    // PATCH /api/drivers/{id}/status
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateDriverStatusRequest req)
    {
        var d = await _db.Drivers.FindAsync(id);
        if (d == null) return NotFound();
        if (!Enum.TryParse<DriverStatus>(req.Status, true, out var status))
            return BadRequest(new { message = "Invalid status value." });
        d.Status = status;
        await _db.SaveChangesAsync();
        return Ok();
    }
}

public record CreateDriverRequest(string FullName, string Phone);
public record UpdateDriverStatusRequest(string Status);
