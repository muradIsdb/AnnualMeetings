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

    // POST /api/fleet/assign — assign vehicle to guest
    [HttpPost("assign")]
    [Authorize(Roles = "Admin,Transport")]
    public async Task<ActionResult> AssignToGuest([FromBody] FleetAssignRequest req)
    {
        var vehicle = await _db.Vehicles.Include(v => v.Driver).FirstOrDefaultAsync(v => v.Id == req.VehicleId);
        if (vehicle == null) return NotFound(new { message = "Vehicle not found." });
        if (vehicle.Status == VehicleStatus.Assigned)
            return BadRequest(new { message = "Vehicle is already assigned to a guest." });

        var guest = await _db.Guests.FindAsync(req.GuestId);
        if (guest == null) return NotFound(new { message = "Guest not found." });

        if (!Enum.TryParse<AssignmentType>(req.AssignmentType, true, out var assignmentType))
            return BadRequest(new { message = "Invalid assignment type. Use 'DropOff' or 'Dedicated'." });

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
}

public record FleetAssignRequest(Guid VehicleId, Guid GuestId, string AssignmentType, string? Notes);
