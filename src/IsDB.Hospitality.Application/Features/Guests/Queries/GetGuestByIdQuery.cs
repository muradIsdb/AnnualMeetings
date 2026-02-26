using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Guests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Guests.Queries;

public record GetGuestByIdQuery(Guid GuestId) : IRequest<GuestDto?>;

public class GetGuestByIdQueryHandler : IRequestHandler<GetGuestByIdQuery, GuestDto?>
{
    private readonly IAppDbContext _context;

    public GetGuestByIdQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<GuestDto?> Handle(GetGuestByIdQuery request, CancellationToken cancellationToken)
    {
        var guest = await _context.Guests
            .Include(g => g.Flights)
            .Include(g => g.ChecklistCompletions)
                .ThenInclude(cc => cc.ChecklistItem)
            .Include(g => g.ChecklistCompletions)
                .ThenInclude(cc => cc.CompletedByStaff)
            .Include(g => g.VehicleAssignments.Where(va => va.IsActive))
                .ThenInclude(va => va.Vehicle)
            .FirstOrDefaultAsync(g => g.Id == request.GuestId, cancellationToken);

        if (guest == null) return null;

        // Get all checklist items to show uncompleted ones too
        var allChecklistItems = await _context.ChecklistItems
            .OrderBy(ci => ci.Order)
            .ToListAsync(cancellationToken);

        var activeAssignment = guest.VehicleAssignments.FirstOrDefault(va => va.IsActive);

        return new GuestDto
        {
            Id = guest.Id,
            EventsAirContactId = guest.EventsAirContactId,
            FullName = guest.FirstName + " " + guest.LastName,
            Title = guest.Title,
            Organization = guest.Organization,
            Designation = guest.Designation,
            Nationality = guest.Nationality,
            PhotoUrl = guest.PhotoUrl,
            MobileNumber = guest.MobileNumber,
            Email = guest.Email,
            IsCritical = guest.IsCritical,
            RequiresAccessibility = guest.RequiresAccessibility,
            Status = guest.Status,
            StatusLabel = guest.Status.ToString(),
            GroupCode = guest.GroupCode,
            RoomNumber = guest.RoomNumber,
            HotelName = guest.HotelName,
            SpecialRequirements = guest.SpecialRequirements,
            Notes = guest.Notes,
            Flights = guest.Flights.Select(f => new FlightSummaryDto
            {
                Id = f.Id,
                FlightNumber = f.FlightNumber,
                Airline = f.Airline,
                ScheduledArrival = f.ScheduledArrival,
                ActualArrival = f.ActualArrival,
                ScheduledDeparture = f.ScheduledDeparture,
                Status = f.Status.ToString(),
                IsArrival = f.IsArrival
            }).ToList(),
            ChecklistCompletions = allChecklistItems.Select(ci =>
            {
                var completion = guest.ChecklistCompletions.FirstOrDefault(cc => cc.ChecklistItemId == ci.Id);
                return new ChecklistCompletionDto
                {
                    ChecklistItemId = ci.Id,
                    ItemName = ci.Name,
                    Order = ci.Order,
                    IsCompleted = completion != null,
                    CompletedAt = completion?.CompletedAt,
                    CompletedByName = completion?.CompletedByStaff?.FullName
                };
            }).ToList(),
            ActiveVehicleAssignment = activeAssignment == null ? null : new VehicleAssignmentDto
            {
                Id = activeAssignment.Id,
                VehicleMake = activeAssignment.Vehicle.Make,
                VehicleModel = activeAssignment.Vehicle.Model,
                LicensePlate = activeAssignment.Vehicle.LicensePlate,
                DriverName = activeAssignment.Vehicle.DriverName,
                DriverPhone = activeAssignment.Vehicle.DriverPhone,
                EstimatedArrivalTime = activeAssignment.EstimatedArrivalTime,
                AssignedAt = activeAssignment.AssignedAt
            }
        };
    }
}
