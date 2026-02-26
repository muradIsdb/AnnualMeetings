using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Vehicles.Commands;

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

        // Deactivate any existing active assignment for this guest
        var existingAssignments = await _context.VehicleAssignments
            .Where(va => va.GuestId == request.GuestId && va.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingAssignments)
        {
            existing.IsActive = false;
            existing.CompletedAt = DateTime.UtcNow;
        }

        // Create new assignment
        var assignment = new VehicleAssignment
        {
            GuestId = request.GuestId,
            VehicleId = request.VehicleId,
            AssignedByStaffId = request.StaffUserId,
            Notes = request.Notes,
            EstimatedArrivalTime = request.EstimatedArrivalTime,
            IsActive = true
        };

        vehicle.Status = VehicleStatus.Assigned;

        _context.VehicleAssignments.Add(assignment);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

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
