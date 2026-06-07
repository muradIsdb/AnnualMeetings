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

    private async Task<string?> GetActiveEventCodeAsync() =>
        (await _db.EventsAirConfigs.FirstOrDefaultAsync())?.EventCode;

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
        var activeEventCode = await GetActiveEventCodeAsync();
        var vehicles = await _db.Vehicles
            .Where(v => v.IsActive && (v.EventCode == null || v.EventCode == activeEventCode))
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
    public async Task<IActionResult> GetAll(
        [FromQuery] string? view,
        [FromQuery] string? type,
        [FromQuery] bool? resolved,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // ── Sync Alerts: summary ──────────────────────────────────────────────────────────────────────────────────────
        if (view == "discrepancies-summary")
        {
            var counts = await _db.SyncAlerts
                .GroupBy(a => new { a.AlertType, a.IsResolved })
                .Select(g => new { g.Key.AlertType, g.Key.IsResolved, Count = g.Count() })
                .ToListAsync(ct);
            return Ok(new
            {
                guestRemoved     = counts.Where(c => c.AlertType == SyncAlertType.GuestRemoved     && !c.IsResolved).Sum(c => c.Count),
                carClassMismatch = counts.Where(c => c.AlertType == SyncAlertType.CarClassMismatch && !c.IsResolved).Sum(c => c.Count),
                regTypeChanged   = counts.Where(c => c.AlertType == SyncAlertType.RegTypeChanged   && !c.IsResolved).Sum(c => c.Count),
                totalResolved    = counts.Where(c => c.IsResolved).Sum(c => c.Count),
                totalOpen        = counts.Where(c => !c.IsResolved).Sum(c => c.Count)
            });
        }

        // ── Sync Alerts: paginated list ───────────────────────────────────────────────────────────────────────────────
        if (view == "discrepancies")
        {
            var q = _db.SyncAlerts.AsQueryable();

            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<SyncAlertType>(type, true, out var at))
                q = q.Where(a => a.AlertType == at);

            if (resolved.HasValue)
                q = q.Where(a => a.IsResolved == resolved.Value);
            else
                q = q.Where(a => !a.IsResolved);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                q = q.Where(a =>
                    a.GuestName.ToLower().Contains(s) ||
                    (a.VehiclePlate != null && a.VehiclePlate.ToLower().Contains(s)) ||
                    (a.CarClassName != null && a.CarClassName.ToLower().Contains(s)) ||
                    (a.EventsAirContactId != null && a.EventsAirContactId.ToLower().Contains(s)));
            }

            var total = await q.CountAsync(ct);
            var items = await q
                .OrderByDescending(a => a.DetectedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    alertType = a.AlertType.ToString(),
                    a.GuestId,
                    a.GuestName,
                    a.EventsAirContactId,
                    a.VehicleId,
                    a.VehiclePlate,
                    a.CarClassName,
                    a.OldValue,
                    a.NewValue,
                    syncSource = a.SyncSource.ToString(),
                    a.DetectedAt,
                    a.IsResolved,
                    a.ResolvedAt,
                    a.ResolvedByUserName,
                    a.Notes
                })
                .ToListAsync(ct);

            return Ok(new
            {
                items,
                totalCount = total,
                totalPages = (int)Math.Ceiling(total / (double)pageSize),
                page,
                pageSize
            });
        }

        // ── Normal vehicle list ───────────────────────────────────────────────────────────────────────────────────────
        var activeEventCode2 = await GetActiveEventCodeAsync();
        var vehicles = await _db.Vehicles
            .Where(v => v.IsActive && (v.EventCode == null || v.EventCode == activeEventCode2))
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

    // ── Sync Alerts: resolve single & resolve-all (routed via ?view=) ─────────────────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<IActionResult> PostWithView(
        [FromQuery] string? view,
        [FromQuery] Guid? id,
        [FromBody] System.Text.Json.JsonElement? body,
        CancellationToken ct = default)
    {
        if (view == "discrepancies-resolve" && id.HasValue)
        {
            var alert = await _db.SyncAlerts.FindAsync(new object[] { id.Value }, ct);
            if (alert == null) return NotFound();
            var userName = User.FindFirst("name")?.Value ?? User.FindFirst("sub")?.Value ?? "Unknown";
            alert.IsResolved = true;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.ResolvedByUserName = userName;
            if (body.HasValue && body.Value.TryGetProperty("notes", out var notesEl) && notesEl.ValueKind == System.Text.Json.JsonValueKind.String)
                alert.Notes = notesEl.GetString();
            await _db.SaveChangesAsync(ct);
            return Ok(new { success = true });
        }

        if (view == "discrepancies-resolve-all")
        {
            var userName = User.FindFirst("name")?.Value ?? User.FindFirst("sub")?.Value ?? "Unknown";
            var alerts = await _db.SyncAlerts.Where(a => !a.IsResolved).ToListAsync(ct);
            var now = DateTime.UtcNow;
            foreach (var a in alerts)
            {
                a.IsResolved = true;
                a.ResolvedAt = now;
                a.ResolvedByUserName = userName;
            }
            await _db.SaveChangesAsync(ct);
            return Ok(new { resolved = alerts.Count });
        }

        if (view == "discrepancies-delete-all")
        {
            var role = User.FindFirst("role")?.Value ?? "";
            if (role != "Admin") return Forbid();
            var all = await _db.SyncAlerts.ToListAsync(ct);
            _db.SyncAlerts.RemoveRange(all);
            await _db.SaveChangesAsync(ct);
            return Ok(new { deleted = all.Count });
        }

        // Fall through to normal vehicle creation
        if (!body.HasValue || body.Value.ValueKind == System.Text.Json.JsonValueKind.Null)
            return BadRequest(new { message = "Request body is required for vehicle creation." });

        var req = System.Text.Json.JsonSerializer.Deserialize<CreateVehicleRequest>(
            body.Value.GetRawText(),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (req == null) return BadRequest(new { message = "Invalid request body." });
        return await CreateInternal(req);
    }

    private async Task<ActionResult> CreateInternal(CreateVehicleRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Make) || string.IsNullOrWhiteSpace(req.Model) || string.IsNullOrWhiteSpace(req.LicensePlate))
            return BadRequest(new { message = "Make, model, and license plate are required." });

        var staffUser = await _db.StaffUsers.FindAsync(CurrentUserId);

        var activeEventCode3 = await GetActiveEventCodeAsync();
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Make = req.Make.Trim(),
            Model = req.Model.Trim(),
            LicensePlate = req.LicensePlate.Trim().ToUpper(),
            Color = req.Color?.Trim(),
            // Default: NotProvided — set to Available when the vehicle physically arrives on site
            Status = VehicleStatus.NotProvided,
            IsActive = true,
            CarClassId = req.CarClassId,
            EventCode = activeEventCode3,
        };
        _db.Vehicles.Add(vehicle);

        // Write initial status history entry
        _db.VehicleStatusHistories.Add(new VehicleStatusHistory
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicle.Id,
            OldStatus = VehicleStatus.NotProvided,
            NewStatus = VehicleStatus.NotProvided,
            ChangedByStaffId = CurrentUserId,
            ChangedByName = staffUser?.FullName,
            ChangedByRole = staffUser?.Role,
            Notes = "Vehicle registered — awaiting physical delivery",
        });

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
        var staffUser = await _db.StaffUsers.FindAsync(CurrentUserId);

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

        if (iMake < 0 || iModel < 0)
            return BadRequest(new { message = "CSV must have columns: Make, Model, LicensePlate (optional), CarNumber (optional), Color (optional), CarClass (optional)." });

        // Pre-load all car classes for name lookup (case-insensitive)
        var allCarClasses = await _db.CarClasses.ToListAsync();

        int lineNum = 1;
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length <= Math.Max(iMake, iModel))
            {
                errors.Add($"Line {lineNum}: not enough columns.");
                skipped++;
                continue;
            }

            var make = parts[iMake].Trim();
            var model = parts[iModel].Trim();
            var plate = iPlate >= 0 && parts.Length > iPlate ? parts[iPlate].Trim().ToUpper() : null;
            var color = iColor >= 0 && parts.Length > iColor ? parts[iColor].Trim() : null;
            var carClassName = iCarClass >= 0 && parts.Length > iCarClass ? parts[iCarClass].Trim() : null;
            var carNumber = iCarNumber >= 0 && parts.Length > iCarNumber ? parts[iCarNumber].Trim() : null;
            // Skip comment lines (lines starting with #)
            if (make.StartsWith("#") || model.StartsWith("#")) { skipped++; continue; }
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

            if (string.IsNullOrEmpty(make) || string.IsNullOrEmpty(model))
            {
                errors.Add($"Line {lineNum}: Make and Model are required.");
                skipped++;
                continue;
            }

            // Skip duplicates by plate (only when a plate is provided)
            if (!string.IsNullOrEmpty(plate) && await _db.Vehicles.AnyAsync(v => v.LicensePlate == plate))
            {
                errors.Add($"Line {lineNum}: Plate '{plate}' already exists — skipped.");
                skipped++;
                continue;
            }

            var newVehicle = new Vehicle
            {
                Id = Guid.NewGuid(),
                Make = make,
                Model = model,
                LicensePlate = string.IsNullOrEmpty(plate) ? null : plate,
                Color = string.IsNullOrEmpty(color) ? null : color,
                CarNumber = string.IsNullOrEmpty(carNumber) ? null : carNumber,
                // CarNumber is already optional — null when not provided
                // Default: NotProvided — set to Available when the vehicle physically arrives on site
                Status = VehicleStatus.NotProvided,
                IsActive = true,
                CarClassId = carClassId,
            };
            _db.Vehicles.Add(newVehicle);

            // Write initial status history entry
            _db.VehicleStatusHistories.Add(new VehicleStatusHistory
            {
                Id = Guid.NewGuid(),
                VehicleId = newVehicle.Id,
                OldStatus = VehicleStatus.NotProvided,
                NewStatus = VehicleStatus.NotProvided,
                ChangedByStaffId = CurrentUserId,
                ChangedByName = staffUser?.FullName,
                ChangedByRole = staffUser?.Role,
                Notes = "Vehicle registered via CSV import — awaiting physical delivery",
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

    /// <summary>
    /// Update the status of a single vehicle.
    /// Cannot change status of an Assigned vehicle (must unassign first).
    /// Writes a history entry with optional notes.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateVehicleStatusRequest req)
    {
        if (!Enum.TryParse<VehicleStatus>(req.Status, ignoreCase: true, out var newStatus))
            return BadRequest(new { message = $"Invalid status '{req.Status}'. Valid values: Available, OutOfService, NotProvided." });

        var vehicle = await _db.Vehicles.FindAsync(id);
        if (vehicle == null) return NotFound();

        // Cannot manually change status of an Assigned vehicle
        if (vehicle.Status == VehicleStatus.Assigned)
            return BadRequest(new { message = "Cannot change status of an assigned vehicle. Unassign the vehicle from its guest first." });

        // Cannot set to Assigned via this endpoint
        if (newStatus == VehicleStatus.Assigned)
            return BadRequest(new { message = "Cannot set status to Assigned via this endpoint. Use the assignment workflow." });

        var oldStatus = vehicle.Status;
        if (oldStatus == newStatus)
            return Ok(new { message = "Status unchanged." });

        vehicle.Status = newStatus;

        var staffUser = await _db.StaffUsers.FindAsync(CurrentUserId);
        _db.VehicleStatusHistories.Add(new VehicleStatusHistory
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicle.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedByStaffId = CurrentUserId,
            ChangedByName = staffUser?.FullName,
            ChangedByRole = staffUser?.Role,
            Notes = req.Notes,
        });

        await _db.SaveChangesAsync();
        return Ok(new { id = vehicle.Id, oldStatus = oldStatus.ToString(), newStatus = newStatus.ToString() });
    }

    /// <summary>
    /// Bulk-update the status of multiple vehicles in one request.
    /// Skips Assigned vehicles (cannot change their status) and returns a summary.
    /// </summary>
    [HttpPatch("bulk-status")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<IActionResult> BulkUpdateStatus([FromBody] BulkUpdateVehicleStatusRequest req)
    {
        if (req.VehicleIds == null || req.VehicleIds.Count == 0)
            return BadRequest(new { message = "No vehicle IDs provided." });

        if (!Enum.TryParse<VehicleStatus>(req.Status, ignoreCase: true, out var newStatus))
            return BadRequest(new { message = $"Invalid status '{req.Status}'. Valid values: Available, OutOfService, NotProvided." });

        if (newStatus == VehicleStatus.Assigned)
            return BadRequest(new { message = "Cannot set status to Assigned via this endpoint. Use the assignment workflow." });

        var vehicles = await _db.Vehicles
            .Where(v => req.VehicleIds.Contains(v.Id) && v.IsActive)
            .ToListAsync();

        var staffUser = await _db.StaffUsers.FindAsync(CurrentUserId);
        var updated = new List<string>();
        var skipped = new List<string>();

        foreach (var vehicle in vehicles)
        {
            if (vehicle.Status == VehicleStatus.Assigned)
            {
                skipped.Add($"{vehicle.Make} {vehicle.Model} ({vehicle.LicensePlate}) — currently assigned to a guest");
                continue;
            }

            if (vehicle.Status == newStatus)
            {
                skipped.Add($"{vehicle.Make} {vehicle.Model} ({vehicle.LicensePlate}) — already {newStatus}");
                continue;
            }

            var oldStatus = vehicle.Status;
            vehicle.Status = newStatus;

            _db.VehicleStatusHistories.Add(new VehicleStatusHistory
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicle.Id,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangedByStaffId = CurrentUserId,
                ChangedByName = staffUser?.FullName,
                ChangedByRole = staffUser?.Role,
                Notes = req.Notes,
            });

            updated.Add($"{vehicle.Make} {vehicle.Model} ({vehicle.LicensePlate})");
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            updatedCount = updated.Count,
            skippedCount = skipped.Count,
            updated,
            skipped,
        });
    }

    /// <summary>
    /// Get the full status change history for a single vehicle.
    /// </summary>
    [HttpGet("{id:guid}/status-history")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<IActionResult> GetStatusHistory(Guid id)
    {
        var vehicle = await _db.Vehicles.FindAsync(id);
        if (vehicle == null) return NotFound();

        var history = await _db.VehicleStatusHistories
            .Where(h => h.VehicleId == id)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new
            {
                h.Id,
                OldStatus = h.OldStatus.ToString(),
                NewStatus = h.NewStatus.ToString(),
                h.ChangedByName,
                ChangedByRole = h.ChangedByRole.HasValue ? h.ChangedByRole.ToString() : null,
                h.Notes,
                ChangedAt = h.CreatedAt,
            })
            .ToListAsync();

        return Ok(history);
    }

    // ═══════════════════════════════════════════════════════════════
    // SYNC ALERTS — exposed under /api/vehicles/sync-log/... (vehicles controller
    //               is already whitelisted by Railway WAF)
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("sync-log")]
    public async Task<IActionResult> GetSyncAlerts(
        [FromQuery] string? tab,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var role = User.FindFirst("role")?.Value ?? "";
        if (role != "Admin" && role != "Transport") return Forbid();
        var q = _db.SyncAlerts.AsQueryable();
        if (tab == "open")          q = q.Where(a => !a.IsResolved);
        else if (tab == "resolved") q = q.Where(a => a.IsResolved);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(a =>
                (a.GuestName != null && a.GuestName.ToLower().Contains(s)) ||
                (a.VehiclePlate != null && a.VehiclePlate.ToLower().Contains(s)) ||
                (a.EventsAirContactId != null && a.EventsAirContactId.ToLower().Contains(s)));
        }
        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.DetectedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                alertType = a.AlertType.ToString(),
                a.GuestId,
                a.GuestName,
                a.EventsAirContactId,
                a.VehicleId,
                a.VehiclePlate,
                a.CarClassName,
                a.OldValue,
                a.NewValue,
                syncSource = a.SyncSource.ToString(),
                a.DetectedAt,
                a.IsResolved,
                a.ResolvedAt,
                a.ResolvedByUserName,
                a.Notes
            })
            .ToListAsync(ct);
        return Ok(new { items, totalCount = total, totalPages = (int)Math.Ceiling(total / (double)pageSize), page, pageSize });
    }

    [HttpGet("sync-log/summary")]
    public async Task<IActionResult> GetSyncAlertsSummary(CancellationToken ct = default)
    {
        var role = User.FindFirst("role")?.Value ?? "";
        if (role != "Admin" && role != "Transport") return Forbid();
        var counts = await _db.SyncAlerts
            .GroupBy(a => new { a.AlertType, a.IsResolved })
            .Select(g => new { g.Key.AlertType, g.Key.IsResolved, Count = g.Count() })
            .ToListAsync(ct);
        return Ok(new
        {
            guestRemoved     = counts.Where(c => c.AlertType == SyncAlertType.GuestRemoved     && !c.IsResolved).Sum(c => c.Count),
            carClassMismatch = counts.Where(c => c.AlertType == SyncAlertType.CarClassMismatch && !c.IsResolved).Sum(c => c.Count),
            regTypeChanged   = counts.Where(c => c.AlertType == SyncAlertType.RegTypeChanged   && !c.IsResolved).Sum(c => c.Count),
            resolved         = counts.Where(c => c.IsResolved).Sum(c => c.Count),
            totalOpen        = counts.Where(c => !c.IsResolved).Sum(c => c.Count)
        });
    }

    [HttpPost("sync-log/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveSyncAlert(Guid id, [FromBody] ResolveSyncAlertRequest? req, CancellationToken ct = default)
    {
        var role = User.FindFirst("role")?.Value ?? "";
        if (role != "Admin" && role != "Transport") return Forbid();
        var alert = await _db.SyncAlerts.FindAsync(new object[] { id }, ct);
        if (alert == null) return NotFound();
        var userName = User.FindFirst("name")?.Value ?? User.FindFirst("sub")?.Value ?? "Unknown";
        alert.IsResolved = true;
        alert.ResolvedAt = DateTime.UtcNow;
        alert.ResolvedByUserName = userName;
        if (!string.IsNullOrWhiteSpace(req?.Notes)) alert.Notes = req.Notes;
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("sync-log/resolve-all")]
    public async Task<IActionResult> ResolveAllSyncAlerts([FromQuery] string? alertType, CancellationToken ct = default)
    {
        var role = User.FindFirst("role")?.Value ?? "";
        if (role != "Admin" && role != "Transport") return Forbid();
        var userName = User.FindFirst("name")?.Value ?? User.FindFirst("sub")?.Value ?? "Unknown";
        var q = _db.SyncAlerts.Where(a => !a.IsResolved);
        if (!string.IsNullOrWhiteSpace(alertType) && Enum.TryParse<SyncAlertType>(alertType, true, out var at))
            q = q.Where(a => a.AlertType == at);
        var alerts = await q.ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var a in alerts) { a.IsResolved = true; a.ResolvedAt = now; a.ResolvedByUserName = userName; }
        await _db.SaveChangesAsync(ct);
        return Ok(new { resolved = alerts.Count });
    }

    [HttpDelete("sync-log/{id:guid}")]
    public async Task<IActionResult> DeleteSyncAlert(Guid id, CancellationToken ct = default)
    {
        var role = User.FindFirst("role")?.Value ?? "";
        if (role != "Admin") return Forbid();
        var alert = await _db.SyncAlerts.FindAsync(new object[] { id }, ct);
        if (alert == null) return NotFound();
        _db.SyncAlerts.Remove(alert);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record ResolveSyncAlertRequest(string? Notes);
public record ResolveAlertRequest(string? Notes);
public record CreateVehicleRequest(string Make, string Model, string LicensePlate, string? Color, Guid? DriverId = null, Guid? CarClassId = null);
public record AssignDriverToVehicleRequest(Guid? DriverId);
public record SetCarNumberRequest(string? CarNumber);
public record UpdateVehicleStatusRequest(string Status, string? Notes = null);
public record BulkUpdateVehicleStatusRequest(List<Guid> VehicleIds, string Status, string? Notes = null);
