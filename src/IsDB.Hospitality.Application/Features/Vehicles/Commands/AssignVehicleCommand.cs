using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Vehicles.Commands;

// ─── Standard assign (available vehicles only) ────────────────────────────────

public record AssignVehicleCommand(
    Guid GuestId,
    Guid VehicleId,
    Guid StaffUserId,
    string? Notes = null,
    string? EstimatedArrivalTime = null) : IRequest<bool>;

public class AssignVehicleCommandHandler : IRequestHandler<AssignVehicleCommand, bool>
{
    private readonly IAppDbContext _context;

    public AssignVehicleCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(AssignVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles.FindAsync(new object[] { request.VehicleId }, cancellationToken);
        if (vehicle == null || vehicle.Status != VehicleStatus.Available) return false;

        await AssignVehicleHelper.ExecuteAssignment(_context, vehicle, request.GuestId, request.StaffUserId, request.Notes, request.EstimatedArrivalTime, cancellationToken);
        return true;
    }
}

// ─── Force-assign (can displace an already-assigned vehicle) ─────────────────

/// <summary>Result of a force-assign operation.</summary>
public record ForceAssignResult(bool Success, string? DisplacedGuestName = null);

public record ForceAssignVehicleCommand(
    Guid GuestId,
    Guid VehicleId,
    Guid StaffUserId,
    string? Notes = null,
    string? EstimatedArrivalTime = null) : IRequest<ForceAssignResult>;

public class ForceAssignVehicleCommandHandler : IRequestHandler<ForceAssignVehicleCommand, ForceAssignResult>
{
    private readonly IAppDbContext _context;

    public ForceAssignVehicleCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ForceAssignResult> Handle(ForceAssignVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles.FindAsync(new object[] { request.VehicleId }, cancellationToken);
        if (vehicle == null) return new ForceAssignResult(false);

        string? displacedGuestName = null;

        // If the vehicle is currently assigned to another guest, displace them
        if (vehicle.Status == VehicleStatus.Assigned && vehicle.CurrentGuestId.HasValue && vehicle.CurrentGuestId != request.GuestId)
        {
            var displacedGuest = await _context.Guests.FindAsync(new object[] { vehicle.CurrentGuestId.Value }, cancellationToken);
            displacedGuestName = displacedGuest?.FullName;

            // Deactivate the vehicle's current active assignment
            var vehicleAssignments = await _context.VehicleAssignments
                .Where(va => va.VehicleId == request.VehicleId && va.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var va in vehicleAssignments)
            {
                va.IsActive = false;
                va.UnassignedAt = DateTime.UtcNow;
                va.UnassignedByStaffId = request.StaffUserId;
            }
        }

        await AssignVehicleHelper.ExecuteAssignment(_context, vehicle, request.GuestId, request.StaffUserId, request.Notes, request.EstimatedArrivalTime, cancellationToken);
        return new ForceAssignResult(true, displacedGuestName);
    }
}

// ─── Shared assignment logic ──────────────────────────────────────────────────

internal static class AssignVehicleHelper
{
    internal static async Task ExecuteAssignment(
        IAppDbContext context,
        Vehicle vehicle,
        Guid guestId,
        Guid staffUserId,
        string? notes,
        string? estimatedArrivalTime,
        CancellationToken cancellationToken)
    {
        // Deactivate any existing active assignment for this guest AND free the old vehicle
        var existingGuestAssignments = await context.VehicleAssignments
            .Where(va => va.GuestId == guestId && va.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingGuestAssignments)
        {
            existing.IsActive = false;
            existing.UnassignedAt = DateTime.UtcNow;

            // Free the old vehicle so it becomes available again
            if (existing.VehicleId != vehicle.Id)
            {
                var oldVehicle = await context.Vehicles.FindAsync(new object[] { existing.VehicleId }, cancellationToken);
                if (oldVehicle != null)
                {
                    oldVehicle.Status = VehicleStatus.Available;
                    oldVehicle.CurrentGuestId = null;
                    oldVehicle.CurrentAssignmentType = null;
                }
            }
        }

        // Create new assignment
        var assignment = new VehicleAssignment
        {
            GuestId = guestId,
            VehicleId = vehicle.Id,
            AssignedByStaffId = staffUserId,
            Notes = notes,
            EstimatedArrivalTime = estimatedArrivalTime,
            IsActive = true
        };

        vehicle.Status = VehicleStatus.Assigned;
        vehicle.CurrentGuestId = guestId;

        context.VehicleAssignments.Add(assignment);
        await context.SaveChangesAsync(cancellationToken);

        // Always fire the bell notification when a vehicle is assigned — regardless of inbound status.
        var guest = await context.Guests.FindAsync(new object[] { guestId }, cancellationToken);
        if (guest != null)
        {
            var assignMsg = $"[Inbound] {guest.FirstName} {guest.LastName}'s vehicle was assigned (dispatched from Airport).";
            context.Notifications.Add(new Notification
            {
                Message = assignMsg,
                TargetRoles = "Hotel",
                Priority = AlertSeverity.Critical,
                CreatedByStaffId = staffUserId
            });
            // Explicit Admin copy
            context.Notifications.Add(new Notification
            {
                Message = assignMsg,
                TargetRoles = "Admin",
                Priority = AlertSeverity.Critical,
                CreatedByStaffId = staffUserId
            });
        }

        // Auto-set VehicleAssigned inbound status only if guest has already arrived (and not yet at hotel)
        if (guest != null && guest.InboundStatus >= InboundStatus.Arrived && guest.InboundStatus < InboundStatus.AtHotel)
        {
            guest.InboundStatus = InboundStatus.VehicleAssigned;

            var historyEntry = new GuestStatusHistory
            {
                Id = Guid.NewGuid(),
                GuestId = guest.Id,
                Track = StatusTrack.Inbound,
                StatusValue = (int)InboundStatus.VehicleAssigned,
                StatusLabel = "Vehicle Assigned",
                ChangedByStaffId = staffUserId,
                ChangedByName = "Transport",
                ChangedByRole = UserRole.Transport,
                IsSystemGenerated = true,
                Notes = "Auto-set when vehicle was assigned.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.GuestStatusHistories.Add(historyEntry);

            // Alert (Control Room panel) — High priority
            var alertNotification = new Alert
            {
                Id = Guid.NewGuid(),
                Title = "Vehicle Assigned",
                Message = $"{guest.FirstName} {guest.LastName}: Vehicle assigned — on the way to hotel.",
                Severity = AlertSeverity.High,
                GuestId = guest.Id,
                TargetRole = UserRole.Hotel,
                IsRead = false,
                IsSystemGenerated = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Alerts.Add(alertNotification);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

// ─── Assign by barcode ────────────────────────────────────────────────────────

public record AssignVehicleByBarcodeCommand(
    Guid GuestId,
    string BarcodeValue,
    Guid StaffUserId,
    string? Notes = null) : IRequest<bool>;

public class AssignVehicleByBarcodeCommandHandler : IRequestHandler<AssignVehicleByBarcodeCommand, bool>
{
    private readonly IAppDbContext _context;
    private readonly IMediator _mediator;

    public AssignVehicleByBarcodeCommandHandler(IAppDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<bool> Handle(AssignVehicleByBarcodeCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.BarcodeValue == request.BarcodeValue, cancellationToken);

        if (vehicle == null) return false;

        return await _mediator.Send(new AssignVehicleCommand(
            request.GuestId,
            vehicle.Id,
            request.StaffUserId,
            request.Notes), cancellationToken);
    }
}
