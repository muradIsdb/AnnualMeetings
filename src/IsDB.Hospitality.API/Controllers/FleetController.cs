using IsDB.Hospitality.API.Services;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[Authorize(Roles = "Admin,Transport,Airport")]
public class FleetController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly NotificationTemplateService _templates;

    public FleetController(AppDbContext db, NotificationTemplateService templates)
    {
        _db = db;
        _templates = templates;
    }

    // GET /api/fleet/assignments?includeHistory=false
    [HttpGet("assignments")]
    public async Task<ActionResult<List<object>>> GetAssignments([FromQuery] bool includeHistory = false)
    {
        var query = _db.VehicleAssignments
            .Include(a => a.Vehicle)
            .Include(a => a.Driver)
            .Include(a => a.Guest)
            .Include(a => a.AssignedByStaff)
            .Include(a => a.UnassignedByStaff)
            .AsQueryable();

        if (!includeHistory)
            query = query.Where(a => a.IsActive);

        var assignments = await query
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();

        return Ok(assignments.Select(a => (object)new
        {
            a.Id,
            VehicleId    = a.VehicleId,
            VehiclePlate = a.Vehicle?.LicensePlate,
            VehicleMake  = a.Vehicle?.Make,
            VehicleModel = a.Vehicle?.Model,
            CarNumber    = a.Vehicle?.CarNumber,
            DriverId     = a.DriverId,
            DriverName   = a.Driver?.FullName,
            DriverPhone  = a.Driver?.Phone,
            GuestId      = a.GuestId,
            GuestName    = a.Guest?.FullName,
            AssignmentType = a.AssignmentType.ToString(),
            a.AssignedAt,
            a.UnassignedAt,
            a.IsActive,
            a.Notes,
            AssignedByName   = a.AssignedByStaff?.FullName,
            AssignedByRole   = a.AssignedByStaff?.Role.ToString(),
            UnassignedByName = a.UnassignedByStaff?.FullName,
            UnassignedByRole = a.UnassignedByStaff?.Role.ToString(),
        }).ToList());
    }

    // POST /api/fleet/assign — assign vehicle to guest (or log a drop-off trip)
    [HttpPost("assign")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<ActionResult> AssignToGuest([FromBody] FleetAssignRequest req)
    {
        var vehicle = await _db.Vehicles.Include(v => v.Driver).FirstOrDefaultAsync(v => v.Id == req.VehicleId);
        if (vehicle == null) return NotFound(new { message = "Vehicle not found." });

        var guest = await _db.Guests.FindAsync(req.GuestId);
        if (guest == null) return NotFound(new { message = "Guest not found." });

        if (!Enum.TryParse<AssignmentType>(req.AssignmentType, true, out var assignmentType))
            return BadRequest(new { message = "Invalid assignment type. Use 'DropOff' or 'Dedicated'." });

        // ── DROP-OFF PATH ─────────────────────────────────────────────────────────
        // Vehicle is NOT linked to the guest; we only log the trip.
        if (assignmentType == AssignmentType.DropOff)
        {
            if (vehicle.Status == VehicleStatus.Assigned)
                return BadRequest(new { message = "Vehicle is already assigned to a guest." });

            return await LogDropOffTripInternal(vehicle, guest, req.Destination, req.Notes);
        }

        // ── DEDICATED PATH ────────────────────────────────────────────────────────
        if (vehicle.Status == VehicleStatus.Assigned)
            return BadRequest(new { message = "Vehicle is already assigned to a guest." });

        // Close any existing active assignment for this guest
        var existing = await _db.VehicleAssignments.Where(a => a.GuestId == req.GuestId && a.IsActive).ToListAsync();
        foreach (var old in existing)
        {
            old.IsActive = false;
            old.UnassignedAt = DateTime.UtcNow;
            old.UnassignedByStaffId = CurrentUserId;
            var oldVehicle = await _db.Vehicles.FindAsync(old.VehicleId);
            if (oldVehicle != null)
            {
                oldVehicle.Status = VehicleStatus.Available;
                oldVehicle.CurrentGuestId = null;
                oldVehicle.CurrentAssignmentType = null;
            }
            if (oldVehicle?.DriverId.HasValue == true)
            {
                var oldDriver = await _db.Drivers.FindAsync(oldVehicle.DriverId);
                if (oldDriver != null) oldDriver.Status = DriverStatus.Available;
            }
        }

        // Create new assignment
        var assignment = new VehicleAssignment
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicle.Id,
            GuestId = guest.Id,
            DriverId = vehicle.DriverId,
            AssignmentType = assignmentType,
            AssignedAt = DateTime.UtcNow,
            AssignedByStaffId = CurrentUserId,
            IsActive = true,
            Notes = req.Notes,
        };
        _db.VehicleAssignments.Add(assignment);

        // Update vehicle and driver status
        vehicle.Status = VehicleStatus.Assigned;
        vehicle.CurrentGuestId = guest.Id;
        vehicle.CurrentAssignmentType = assignmentType;
        if (vehicle.DriverId.HasValue)
        {
            var driver = await _db.Drivers.FindAsync(vehicle.DriverId);
            if (driver != null) driver.Status = DriverStatus.Assigned;
        }

        // Fire notification from template (first assignment vs. vehicle change)
        var isReassignment = existing.Count > 0;
        var eventKey        = isReassignment ? "inbound.vehicle_changed" : "inbound.vehicle_assigned";
        var eventKeyAdmin   = isReassignment ? "inbound.vehicle_changed.admin_copy" : "inbound.vehicle_assigned.admin_copy";

        var notifs = await _templates.BuildNotificationsAsync(
            new[] { eventKey, eventKeyAdmin },
            guest.FullName,
            CurrentUserId,
            vehicle.LicensePlate, vehicle.Make, vehicle.Model);
        _db.Notifications.AddRange(notifs);

        // Auto-set VehicleAssigned inbound status if guest has already arrived
        if (guest.InboundStatus >= InboundStatus.Arrived && guest.InboundStatus < InboundStatus.AtHotel)
            guest.InboundStatus = InboundStatus.VehicleAssigned;

        // Write vehicle activity history entry
        var staffUser = await _db.StaffUsers.FindAsync(CurrentUserId);
        var vehicleLabel = isReassignment
            ? $"Vehicle Reassigned — {vehicle.Make} {vehicle.Model} ({vehicle.LicensePlate})"
            : $"Vehicle Assigned — {vehicle.Make} {vehicle.Model} ({vehicle.LicensePlate})";
        _db.GuestStatusHistories.Add(new GuestStatusHistory
        {
            Id = Guid.NewGuid(),
            GuestId = guest.Id,
            Track = StatusTrack.Vehicle,
            StatusValue = 0,
            StatusLabel = vehicleLabel,
            ChangedByStaffId = CurrentUserId,
            ChangedByName = staffUser?.FullName,
            ChangedByRole = staffUser?.Role,
            IsSystemGenerated = false,
            Notes = req.Notes
        });

        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAssignments), new { }, new { assignment.Id });
    }

    // POST /api/fleet/force-assign — assign vehicle to guest even if already assigned (displaces existing assignment)
    [HttpPost("force-assign")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<ActionResult> ForceAssignToGuest([FromBody] FleetAssignRequest req)
    {
        var vehicle = await _db.Vehicles.Include(v => v.Driver).FirstOrDefaultAsync(v => v.Id == req.VehicleId);
        if (vehicle == null) return NotFound(new { message = "Vehicle not found." });

        var guest = await _db.Guests.FindAsync(req.GuestId);
        if (guest == null) return NotFound(new { message = "Guest not found." });

        if (!Enum.TryParse<AssignmentType>(req.AssignmentType, true, out var assignmentType))
            return BadRequest(new { message = "Invalid assignment type. Use 'DropOff' or 'Dedicated'." });

        // ── DROP-OFF PATH ─────────────────────────────────────────────────────────
        if (assignmentType == AssignmentType.DropOff)
            return await LogDropOffTripInternal(vehicle, guest, req.Destination, req.Notes);

        // ── DEDICATED PATH ────────────────────────────────────────────────────────
        // Displace any existing active assignment on this vehicle (different guest)
        string? displacedGuestName = null;
        var displacedAssignment = await _db.VehicleAssignments
            .Include(a => a.Guest)
            .FirstOrDefaultAsync(a => a.VehicleId == req.VehicleId && a.IsActive && a.GuestId != req.GuestId);
        if (displacedAssignment != null)
        {
            displacedGuestName = displacedAssignment.Guest?.FullName;
            displacedAssignment.IsActive = false;
            displacedAssignment.UnassignedAt = DateTime.UtcNow;
            displacedAssignment.UnassignedByStaffId = CurrentUserId;
        }

        // Close any existing active assignment for this guest (on a different vehicle)
        var existingGuestAssignments = await _db.VehicleAssignments
            .Where(a => a.GuestId == req.GuestId && a.IsActive)
            .ToListAsync();
        foreach (var old in existingGuestAssignments)
        {
            old.IsActive = false;
            old.UnassignedAt = DateTime.UtcNow;
            old.UnassignedByStaffId = CurrentUserId;
            if (old.VehicleId != req.VehicleId)
            {
                var oldVehicle = await _db.Vehicles.FindAsync(old.VehicleId);
                if (oldVehicle != null)
                {
                    oldVehicle.Status = VehicleStatus.Available;
                    oldVehicle.CurrentGuestId = null;
                    oldVehicle.CurrentAssignmentType = null;
                }
                if (oldVehicle?.DriverId.HasValue == true)
                {
                    var oldDriver = await _db.Drivers.FindAsync(oldVehicle.DriverId);
                    if (oldDriver != null) oldDriver.Status = DriverStatus.Available;
                }
            }
        }

        // Create new assignment
        var assignment = new VehicleAssignment
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicle.Id,
            GuestId = guest.Id,
            DriverId = vehicle.DriverId,
            AssignmentType = assignmentType,
            AssignedAt = DateTime.UtcNow,
            AssignedByStaffId = CurrentUserId,
            IsActive = true,
            Notes = req.Notes,
        };
        _db.VehicleAssignments.Add(assignment);

        // Update vehicle and driver status
        vehicle.Status = VehicleStatus.Assigned;
        vehicle.CurrentGuestId = guest.Id;
        vehicle.CurrentAssignmentType = assignmentType;
        if (vehicle.DriverId.HasValue)
        {
            var driver = await _db.Drivers.FindAsync(vehicle.DriverId);
            if (driver != null) driver.Status = DriverStatus.Assigned;
        }

        // Fire notification from template
        var isReassignment = existingGuestAssignments.Count > 0 || displacedAssignment != null;
        var eventKey      = isReassignment ? "inbound.vehicle_changed" : "inbound.vehicle_assigned";
        var eventKeyAdmin = isReassignment ? "inbound.vehicle_changed.admin_copy" : "inbound.vehicle_assigned.admin_copy";
        var notifs = await _templates.BuildNotificationsAsync(
            new[] { eventKey, eventKeyAdmin },
            guest.FullName,
            CurrentUserId,
            vehicle.LicensePlate, vehicle.Make, vehicle.Model);
        _db.Notifications.AddRange(notifs);

        // Auto-set VehicleAssigned inbound status if guest has already arrived
        if (guest.InboundStatus >= InboundStatus.Arrived && guest.InboundStatus < InboundStatus.AtHotel)
            guest.InboundStatus = InboundStatus.VehicleAssigned;

        // Write vehicle activity history entry
        var staffUserForce = await _db.StaffUsers.FindAsync(CurrentUserId);
        var forceLabel = isReassignment
            ? $"Vehicle Force-Reassigned — {vehicle.Make} {vehicle.Model} ({vehicle.LicensePlate})"
            : $"Vehicle Force-Assigned — {vehicle.Make} {vehicle.Model} ({vehicle.LicensePlate})";
        if (displacedGuestName != null)
            forceLabel += $" (displaced from {displacedGuestName})";
        _db.GuestStatusHistories.Add(new GuestStatusHistory
        {
            Id = Guid.NewGuid(),
            GuestId = guest.Id,
            Track = StatusTrack.Vehicle,
            StatusValue = 0,
            StatusLabel = forceLabel,
            ChangedByStaffId = CurrentUserId,
            ChangedByName = staffUserForce?.FullName,
            ChangedByRole = staffUserForce?.Role,
            IsSystemGenerated = false,
            Notes = req.Notes
        });

        await _db.SaveChangesAsync();
        return Ok(new { displacedGuestName });
    }

    // POST /api/fleet/assignments/{id}/unassign
    [HttpPost("assignments/{id:guid}/unassign")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<IActionResult> Unassign(Guid id)
    {
        var assignment = await _db.VehicleAssignments
            .Include(a => a.Guest)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (assignment == null) return NotFound();
        if (!assignment.IsActive) return BadRequest(new { message = "Assignment is already completed." });

        assignment.IsActive = false;
        assignment.UnassignedAt = DateTime.UtcNow;
        assignment.UnassignedByStaffId = CurrentUserId;

        // Free vehicle
        var vehicle = await _db.Vehicles.Include(v => v.Driver).FirstOrDefaultAsync(v => v.Id == assignment.VehicleId);
        if (vehicle != null)
        {
            vehicle.Status = VehicleStatus.Available;
            vehicle.CurrentGuestId = null;
            vehicle.CurrentAssignmentType = null;
            if (vehicle.Driver != null) vehicle.Driver.Status = DriverStatus.Available;
        }

        // Notify Hotel + Admin from template
        if (assignment.Guest != null)
        {
            var notifs = await _templates.BuildNotificationsAsync(
                new[] { "inbound.vehicle_cancelled", "inbound.vehicle_cancelled.admin_copy" },
                assignment.Guest.FullName,
                CurrentUserId,
                vehicle?.LicensePlate, vehicle?.Make, vehicle?.Model);
            _db.Notifications.AddRange(notifs);
        }

        // Roll back InboundStatus from VehicleAssigned → Arrived when vehicle is unassigned
        if (assignment.Guest != null
            && assignment.Guest.InboundStatus == InboundStatus.VehicleAssigned)
        {
            assignment.Guest.InboundStatus = InboundStatus.Arrived;
        }

        // Write vehicle unassignment history entry
        if (assignment.Guest != null)
        {
            var staffUserUnassign = await _db.StaffUsers.FindAsync(CurrentUserId);
            var unassignLabel = $"Vehicle Unassigned — {vehicle?.Make} {vehicle?.Model} ({vehicle?.LicensePlate ?? "unknown"})";
            _db.GuestStatusHistories.Add(new GuestStatusHistory
            {
                Id = Guid.NewGuid(),
                GuestId = assignment.Guest.Id,
                Track = StatusTrack.Vehicle,
                StatusValue = 0,
                StatusLabel = unassignLabel,
                ChangedByStaffId = CurrentUserId,
                ChangedByName = staffUserUnassign?.FullName,
                ChangedByRole = staffUserUnassign?.Role,
                IsSystemGenerated = false
            });
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── DROP-OFF TRIP ENDPOINTS ──────────────────────────────────────────────────

    // GET /api/fleet/dropoff-trips?status=all|inprogress|completed&page=1&pageSize=20
    [HttpGet("dropoff-trips")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<ActionResult<object>> GetDropOffTrips(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.DropOffTrips
            .Include(d => d.Guest)
            .Include(d => d.Vehicle)
            .Include(d => d.Driver)
            .Include(d => d.LoggedByStaff)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status.ToLower() != "all")
        {
            if (Enum.TryParse<DropOffTripStatus>(status, true, out var parsedStatus))
                query = query.Where(d => d.Status == parsedStatus);
        }

        var total = await query.CountAsync();
        var trips = await query
            .OrderByDescending(d => d.LoggedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
            items = trips.Select(d => new
            {
                d.Id,
                GuestId      = d.GuestId,
                GuestName    = d.Guest?.FullName,
                GuestCountry = d.Guest?.Country,
                VehicleId    = d.VehicleId,
                CarNumber    = d.CarNumber ?? d.Vehicle?.CarNumber,
                VehicleMake  = d.Vehicle?.Make,
                VehicleModel = d.Vehicle?.Model,
                LicensePlate = d.Vehicle?.LicensePlate,
                DriverId     = d.DriverId,
                DriverName   = d.DriverName ?? d.Driver?.FullName,
                DriverPhone  = d.DriverPhone ?? d.Driver?.Phone,
                d.Destination,
                d.Notes,
                d.LoggedAt,
                d.CompletedAt,
                Status       = d.Status.ToString(),
                LoggedBy     = d.LoggedByStaff?.FullName,
                LoggedByRole = d.LoggedByStaff?.Role.ToString(),
            })
        });
    }

    // POST /api/fleet/dropoff-trips/{id}/complete — mark a drop-off trip as completed
    [HttpPost("dropoff-trips/{id:guid}/complete")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<IActionResult> CompleteDropOffTrip(Guid id)
    {
        var trip = await _db.DropOffTrips.FindAsync(id);
        if (trip == null) return NotFound(new { message = "Drop-off trip not found." });
        if (trip.Status == DropOffTripStatus.Completed)
            return BadRequest(new { message = "Trip is already completed." });

        trip.Status = DropOffTripStatus.Completed;
        trip.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { trip.Id, trip.CompletedAt });
    }

    // POST /api/fleet/dropoff-trips/complete-all — mark all in-progress trips as completed
    [HttpPost("dropoff-trips/complete-all")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<IActionResult> CompleteAllDropOffTrips()
    {
        var trips = await _db.DropOffTrips
            .Where(t => t.Status == DropOffTripStatus.InProgress)
            .ToListAsync();
        if (!trips.Any())
            return Ok(new { updated = 0, message = "No in-progress trips to complete." });
        var now = DateTime.UtcNow;
        foreach (var trip in trips)
        {
            trip.Status = DropOffTripStatus.Completed;
            trip.CompletedAt = now;
        }
        await _db.SaveChangesAsync();
        return Ok(new { updated = trips.Count });
    }

    // POST /api/fleet/dropoff-trips — log a drop-off trip directly (used by the assign modal)
    [HttpPost("dropoff-trips")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<IActionResult> CreateDropOffTrip([FromBody] DropOffTripRequest req)
    {
        var vehicle = await _db.Vehicles.Include(v => v.Driver).FirstOrDefaultAsync(v => v.Id == req.VehicleId);
        if (vehicle == null) return NotFound(new { message = "Vehicle not found." });
        var guest = await _db.Guests.FindAsync(req.GuestId);
        if (guest == null) return NotFound(new { message = "Guest not found." });
        return await LogDropOffTripInternal(vehicle, guest, req.Destination, req.Notes);
    }

    // ── PRIVATE HELPERS ──────────────────────────────────────────────────────────

    /// <summary>
    /// Logs a drop-off trip. The vehicle is NOT linked to the guest and its status is NOT changed.
    /// </summary>
    private async Task<ActionResult> LogDropOffTripInternal(Vehicle vehicle, Guest guest, string? destination, string? notes)
    {
        if (string.IsNullOrWhiteSpace(destination))
            return BadRequest(new { message = "Destination is required for drop-off trips." });

        var staffUser = await _db.StaffUsers.FindAsync(CurrentUserId);

        var trip = new DropOffTrip
        {
            Id              = Guid.NewGuid(),
            GuestId         = guest.Id,
            VehicleId       = vehicle.Id,
            DriverId        = vehicle.DriverId,
            DriverName      = vehicle.Driver?.FullName,
            DriverPhone     = vehicle.Driver?.Phone,
            CarNumber       = vehicle.CarNumber,
            Destination     = destination,
            Notes           = notes,
            LoggedByStaffId = CurrentUserId,
            LoggedAt        = DateTime.UtcNow,
            Status          = DropOffTripStatus.InProgress,
        };
        _db.DropOffTrips.Add(trip);

        // Write a history entry on the guest timeline so the team can see the drop-off was logged
        _db.GuestStatusHistories.Add(new GuestStatusHistory
        {
            Id               = Guid.NewGuid(),
            GuestId          = guest.Id,
            Track            = StatusTrack.Vehicle,
            StatusValue      = 0,
            StatusLabel      = $"Drop-off Trip Logged — {vehicle.Make} {vehicle.Model} ({vehicle.CarNumber ?? vehicle.LicensePlate}) → {destination}",
            ChangedByStaffId = CurrentUserId,
            ChangedByName    = staffUser?.FullName,
            ChangedByRole    = staffUser?.Role,
            IsSystemGenerated = false,
            Notes            = notes
        });

        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetDropOffTrips), new { }, new { trip.Id });
    }
}

public record FleetAssignRequest(Guid VehicleId, Guid GuestId, string AssignmentType, string? Destination, string? Notes);
public record DropOffTripRequest(Guid VehicleId, Guid GuestId, string? Destination, string? Notes);
