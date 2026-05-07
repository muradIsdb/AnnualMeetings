using IsDB.Hospitality.Application.DTOs.Vehicles;
using IsDB.Hospitality.Application.Features.Vehicles.Commands;
using IsDB.Hospitality.Application.Features.Vehicles.Queries;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[Authorize]
public class VehiclesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    public VehiclesController(AppDbContext db) { _db = db; }

    // ─── Legacy endpoints ─────────────────────────────────────────────────────
    [HttpGet("available")]
    public async Task<ActionResult<List<VehicleDto>>> GetAvailable()
        => Ok(await Mediator.Send(new GetAvailableVehiclesQuery()));

    [HttpPost("assign")]
    public async Task<IActionResult> AssignVehicle([FromBody] AssignVehicleDto request)
    {
        var ok = await Mediator.Send(new AssignVehicleCommand(request.GuestId, request.VehicleId, CurrentUserId, request.Notes, request.EstimatedArrivalTime));
        return ok ? NoContent() : BadRequest(new { message = "Vehicle is not available or guest not found." });
    }

    [HttpPost("assign-by-barcode")]
    public async Task<IActionResult> AssignByBarcode([FromBody] AssignVehicleByBarcodeDto request)
    {
        var ok = await Mediator.Send(new AssignVehicleByBarcodeCommand(request.GuestId, request.BarcodeValue, CurrentUserId, request.Notes));
        return ok ? NoContent() : BadRequest(new { message = "Barcode not found or vehicle unavailable." });
    }

    /// <summary>
    /// Force-assign a vehicle to a guest, displacing any existing assignment on that vehicle.
    /// Returns the displaced guest name so the frontend can show a confirmation result.
    /// </summary>
    [HttpPost("assign-force")]
    public async Task<IActionResult> ForceAssignVehicle([FromBody] AssignVehicleDto request)
    {
        var result = await Mediator.Send(new ForceAssignVehicleCommand(request.GuestId, request.VehicleId, CurrentUserId, request.Notes, request.EstimatedArrivalTime));
        if (!result.Success)
            return BadRequest(new { message = "Vehicle not found." });
        return Ok(new { displacedGuestName = result.DisplacedGuestName });
    }

    /// <summary>
    /// Returns all active vehicles with their current guest name (for the "Show all" picker).
    /// </summary>
    [HttpGet("all-with-status")]
    public async Task<IActionResult> GetAllWithStatus()
    {
        var vehicles = await _db.Vehicles
            .Where(v => v.IsActive)
            .Include(v => v.Driver)
            .Include(v => v.CarClass)
            .OrderBy(v => v.Status).ThenBy(v => v.Make).ThenBy(v => v.Model)
            .ToListAsync();

        var guestIds = vehicles.Where(v => v.CurrentGuestId.HasValue).Select(v => v.CurrentGuestId!.Value).Distinct().ToList();
        var guestNames = await _db.Guests.Where(g => guestIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.FullName);

        return Ok(vehicles.Select(v => new
        {
            v.Id,
            v.LicensePlate,
            v.Make,
            v.Model,
            v.Color,
            Status = v.Status.ToString(),
            v.BarcodeValue,
            DriverName = (v.Driver != null && v.Driver.IsActive) ? v.Driver.FullName : null,
            DriverPhone = (v.Driver != null && v.Driver.IsActive) ? v.Driver.Phone : null,
            CurrentGuestId = v.CurrentGuestId,
            CurrentGuestName = v.CurrentGuestId.HasValue && guestNames.TryGetValue(v.CurrentGuestId.Value, out var n) ? n : null,
            CarClassId = v.CarClassId,
            CarClassName = v.CarClass?.Name,
            CarClassColor = v.CarClass?.Color,
            v.CarNumber,
            DriverId = v.DriverId,
        }).ToList());
    }

    // ─── Fleet CRUD ───────────────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<ActionResult<List<object>>> GetAll()
    {
        var vehicles = await _db.Vehicles
            .Where(v => v.IsActive)
            .Include(v => v.Driver)
            .Include(v => v.CarClass)
            .OrderBy(v => v.Make).ThenBy(v => v.Model)
            .ToListAsync();

        var guestIds = vehicles.Where(v => v.CurrentGuestId.HasValue).Select(v => v.CurrentGuestId!.Value).Distinct().ToList();
        var guestNames = await _db.Guests.Where(g => guestIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.FullName);

        return Ok(vehicles.Select(v => (object)new
        {
            v.Id, v.LicensePlate, v.Make, v.Model, v.Color,
            Status = v.Status.ToString(), v.BarcodeValue, v.IsActive,
            v.DriverId, v.CarNumber,
            // Only return driver info if the driver is still active
            DriverName = (v.Driver != null && v.Driver.IsActive) ? v.Driver.FullName : null,
            DriverPhone = (v.Driver != null && v.Driver.IsActive) ? v.Driver.Phone : null,
            CurrentGuestId = v.CurrentGuestId,
            CurrentGuestName = v.CurrentGuestId.HasValue && guestNames.TryGetValue(v.CurrentGuestId.Value, out var n) ? n : null,
            CurrentAssignmentType = v.CurrentAssignmentType.HasValue ? v.CurrentAssignmentType.ToString() : null,
            CarClassId = v.CarClassId,
            CarClassName = v.CarClass?.Name,
            CarClassColor = v.CarClass?.Color,
        }).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<ActionResult<object>> GetById(Guid id)
    {
        var v = await _db.Vehicles.Include(x => x.Driver).FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();
        return Ok(new
        {
            v.Id, v.LicensePlate, v.Make, v.Model, v.Color,
            Status = v.Status.ToString(), v.BarcodeValue, v.IsActive,
            v.DriverId, v.CarNumber,
            DriverName = (v.Driver != null && v.Driver.IsActive) ? v.Driver.FullName : null,
            DriverPhone = (v.Driver != null && v.Driver.IsActive) ? v.Driver.Phone : null,
            CurrentGuestId = v.CurrentGuestId,
            CurrentAssignmentType = v.CurrentAssignmentType.HasValue ? v.CurrentAssignmentType.ToString() : null,
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<ActionResult> Create([FromBody] CreateVehicleRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Make) || string.IsNullOrWhiteSpace(req.Model) || string.IsNullOrWhiteSpace(req.LicensePlate))
            return BadRequest(new { message = "Make, model, and license plate are required." });

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Make = req.Make.Trim(),
            Model = req.Model.Trim(),
            LicensePlate = req.LicensePlate.Trim().ToUpper(),
            Color = req.Color?.Trim(),
            Status = VehicleStatus.Available,
            IsActive = true,
            CarClassId = req.CarClassId,
        };
        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync();

        // Assign driver if provided
        if (req.DriverId.HasValue)
        {
            var driver = await _db.Drivers.FindAsync(req.DriverId.Value);
            if (driver != null && driver.IsActive)
            {
                // Unlink driver from any previous vehicle
                if (driver.VehicleId.HasValue)
                {
                    var prevVehicle = await _db.Vehicles.FindAsync(driver.VehicleId.Value);
                    if (prevVehicle != null) prevVehicle.DriverId = null;
                }
                driver.VehicleId = vehicle.Id;
                vehicle.DriverId = driver.Id;
                await _db.SaveChangesAsync();
            }
        }

        return CreatedAtAction(nameof(GetById), new { id = vehicle.Id }, new { vehicle.Id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateVehicleRequest req)
    {
        var v = await _db.Vehicles.FindAsync(id);
        if (v == null) return NotFound();
        v.Make = req.Make.Trim();
        v.Model = req.Model.Trim();
        v.LicensePlate = req.LicensePlate.Trim().ToUpper();
        v.Color = req.Color?.Trim();
        v.CarClassId = req.CarClassId;
        await _db.SaveChangesAsync();
        return Ok(new { v.Id });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var vehicle = await _db.Vehicles
            .Include(v => v.Driver)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (vehicle == null) return NotFound();

        // Close any active assignments for this vehicle
        var activeAssignments = await _db.VehicleAssignments
            .Where(a => a.VehicleId == id && a.IsActive)
            .ToListAsync();
        foreach (var a in activeAssignments)
        {
            a.IsActive = false;
            a.UnassignedAt = DateTime.UtcNow;
            a.UnassignedByStaffId = CurrentUserId;
        }

        // Free the driver
        if (vehicle.Driver != null)
        {
            vehicle.Driver.VehicleId = null;
            vehicle.Driver.Status = DriverStatus.Available;
        }
        vehicle.DriverId = null;

        vehicle.IsActive = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("import")]
    [Authorize(Roles = "Admin,Transport")]
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

        // Parse header to find column indices (case-insensitive)
        var cols = header.Split(',').Select(c => c.Trim().ToLower()).ToArray();
        int iMake = Array.IndexOf(cols, "make");
        int iModel = Array.IndexOf(cols, "model");
        int iPlate = Array.IndexOf(cols, "licenseplate") >= 0 ? Array.IndexOf(cols, "licenseplate") : Array.IndexOf(cols, "plate");
        int iColor = Array.IndexOf(cols, "color");
        int iCarClass = Array.IndexOf(cols, "carclass") >= 0 ? Array.IndexOf(cols, "carclass") : Array.IndexOf(cols, "class");
        int iCarNumber = Array.IndexOf(cols, "carnumber") >= 0 ? Array.IndexOf(cols, "carnumber") : Array.IndexOf(cols, "car#");

        if (iMake < 0 || iModel < 0 || iPlate < 0)
            return BadRequest(new { message = "CSV must have columns: Make, Model, LicensePlate (or Plate), Color (optional), CarClass (optional)." });

        // Pre-load all car classes for name lookup (case-insensitive)
        var allCarClasses = await _db.CarClasses.ToListAsync();

        int lineNum = 1;
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length <= Math.Max(iMake, Math.Max(iModel, iPlate)))
            {
                errors.Add($"Line {lineNum}: not enough columns.");
                skipped++;
                continue;
            }

            var make = parts[iMake].Trim();
            var model = parts[iModel].Trim();
            var plate = parts[iPlate].Trim().ToUpper();
            var color = iColor >= 0 && parts.Length > iColor ? parts[iColor].Trim() : null;
            var carClassName = iCarClass >= 0 && parts.Length > iCarClass ? parts[iCarClass].Trim() : null;
            var carNumber = iCarNumber >= 0 && parts.Length > iCarNumber ? parts[iCarNumber].Trim() : null;
            // Skip comment lines (lines starting with #)
            if (make.StartsWith("#") || model.StartsWith("#") || plate.StartsWith("#")) { skipped++; continue; }
            // Look up car class by name (case-insensitive)
            Guid? carClassId = null;
            if (!string.IsNullOrEmpty(carClassName))
            {
                var matchedClass = allCarClasses.FirstOrDefault(c =>
                    string.Equals(c.Name, carClassName, StringComparison.OrdinalIgnoreCase));
                if (matchedClass != null)
                    carClassId = matchedClass.Id;
                else
                    errors.Add($"Line {lineNum}: CarClass '{carClassName}' not found — vehicle imported without class.");
            }

            if (string.IsNullOrEmpty(make) || string.IsNullOrEmpty(model) || string.IsNullOrEmpty(plate))
            {
                errors.Add($"Line {lineNum}: Make, Model, and Plate are required.");
                skipped++;
                continue;
            }

            // Skip duplicates by plate
            if (await _db.Vehicles.AnyAsync(v => v.LicensePlate == plate))
            {
                errors.Add($"Line {lineNum}: Plate '{plate}' already exists — skipped.");
                skipped++;
                continue;
            }

            _db.Vehicles.Add(new Vehicle
            {
                Id = Guid.NewGuid(),
                Make = make,
                Model = model,
                LicensePlate = plate,
                Color = string.IsNullOrEmpty(color) ? null : color,
                CarNumber = string.IsNullOrEmpty(carNumber) ? null : carNumber,
                Status = VehicleStatus.Available,
                IsActive = true,
                CarClassId = carClassId,
            });
            imported++;
        }

        await _db.SaveChangesAsync();
        return Ok(new { imported, skipped, errors });
    }

    [HttpPatch("{id:guid}/driver")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<IActionResult> AssignDriver(Guid id, [FromBody] AssignDriverToVehicleRequest req)
    {
        var vehicle = await _db.Vehicles.FindAsync(id);
        if (vehicle == null) return NotFound();

        // Unlink old driver
        if (vehicle.DriverId.HasValue)
        {
            var old = await _db.Drivers.FindAsync(vehicle.DriverId);
            if (old != null) old.VehicleId = null;
        }

        if (req.DriverId.HasValue)
        {
            var driver = await _db.Drivers.FindAsync(req.DriverId.Value);
            if (driver == null) return BadRequest(new { message = "Driver not found." });
            if (!driver.IsActive) return BadRequest(new { message = "Driver is inactive." });

            // Unlink driver from any previous vehicle
            if (driver.VehicleId.HasValue && driver.VehicleId != id)
            {
                var prevVehicle = await _db.Vehicles.FindAsync(driver.VehicleId.Value);
                if (prevVehicle != null) prevVehicle.DriverId = null;
            }

            driver.VehicleId = vehicle.Id;
            vehicle.DriverId = driver.Id;
        }
        else
        {
            vehicle.DriverId = null;
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPatch("{id:guid}/car-number")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<IActionResult> SetCarNumber(Guid id, [FromBody] SetCarNumberRequest req)
    {
        var vehicle = await _db.Vehicles.FindAsync(id);
        if (vehicle == null) return NotFound();

        var trimmed = string.IsNullOrWhiteSpace(req.CarNumber) ? null : req.CarNumber.Trim();

        // Uniqueness check — only when setting a non-null value
        if (trimmed != null)
        {
            var conflict = await _db.Vehicles
                .Where(v => v.CarNumber == trimmed && v.Id != id && v.IsActive)
                .Select(v => new { v.Id, v.LicensePlate, v.Make, v.Model })
                .FirstOrDefaultAsync();

            if (conflict != null)
                return Conflict(new
                {
                    error = "Car number already in use",
                    conflictingVehicle = new
                    {
                        id = conflict.Id,
                        plate = conflict.LicensePlate,
                        make = conflict.Make,
                        model = conflict.Model,
                    }
                });
        }

        vehicle.CarNumber = trimmed;
        await _db.SaveChangesAsync();
        return Ok();
    }
}

public record CreateVehicleRequest(string Make, string Model, string LicensePlate, string? Color, Guid? DriverId = null, Guid? CarClassId = null);
public record AssignDriverToVehicleRequest(Guid? DriverId);
public record SetCarNumberRequest(string? CarNumber);
