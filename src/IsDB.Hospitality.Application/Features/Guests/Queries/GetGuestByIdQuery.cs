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
            .Include(g => g.TravelBookings)
                .ThenInclude(tb => tb.Flight)
            .Include(g => g.ChecklistCompletions)
                .ThenInclude(cc => cc.ChecklistItem)
            .Include(g => g.ChecklistCompletions)
                .ThenInclude(cc => cc.CompletedByStaff)
            .Include(g => g.VehicleAssignments.Where(va => va.IsActive))
                .ThenInclude(va => va.Vehicle)
                    .ThenInclude(v => v.Driver)
            .Include(g => g.DeservedCarClass)
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
            Country = guest.Country,
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
            RankValue = guest.RankValue,
            DedicatedCar = guest.DedicatedCar,
            RegistrationTypeName = guest.RegistrationTypeName,
            DeservedCarClassId = guest.DeservedCarClassId,
            DeservedCarClassName = guest.DeservedCarClass?.Name,
            DeservedCarClassColor = guest.DeservedCarClass?.Color,
            InboundStatus = guest.InboundStatus,
            InboundStatusLabel = guest.InboundStatus.ToString(),
            ReceivedByEmbassyTeam = guest.ReceivedByEmbassyTeam,
            OutboundStatus = guest.OutboundStatus,
            OutboundStatusLabel = guest.OutboundStatus?.ToString(),
            TravelBookings = guest.TravelBookings.Select(tb => new TravelBookingSummaryDto
            {
                Id = tb.Id,
                FlightId = tb.FlightId,
                FlightNumber = tb.Flight.FlightNumber,
                AirlineName = tb.Flight.AirlineName,
                ScheduledDeparture = tb.Flight.ScheduledDeparture,
                ScheduledArrival = tb.Flight.ScheduledArrival,
                DeparturePort = tb.Flight.DeparturePortName,
                DeparturePortCode = tb.Flight.DeparturePortIataCode,
                ArrivalPort = tb.Flight.ArrivalPortName,
                ArrivalPortCode = tb.Flight.ArrivalPortIataCode,
                Terminal = tb.Flight.ActualTerminal,
                FlightStatus = tb.Flight.Status.ToString(),
                IsArrival = tb.IsArrival,
                SeatClass = tb.SeatClass,
                BookingNotes = tb.BookingNotes,
                ActualArrival = tb.Flight.ActualArrival,
                ActualDeparture = tb.Flight.ActualDeparture,
                ChangedSinceLastView = tb.ChangedSinceLastView,
                PreviousFlightNumber = tb.PreviousFlightNumber,
                ChangedAt = tb.ChangedAt
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
                VehicleId = activeAssignment.VehicleId,
                VehicleMake = activeAssignment.Vehicle.Make,
                VehicleModel = activeAssignment.Vehicle.Model,
                LicensePlate = activeAssignment.Vehicle.LicensePlate,
                VehicleColor = activeAssignment.Vehicle.Color,
                CarNumber = activeAssignment.Vehicle.CarNumber,
                AssignmentType = activeAssignment.AssignmentType.ToString(),
                DriverId = activeAssignment.Vehicle.DriverId,
                DriverName = activeAssignment.Vehicle.Driver?.FullName ?? activeAssignment.Vehicle.DriverName,
                DriverPhone = activeAssignment.Vehicle.Driver?.Phone ?? activeAssignment.Vehicle.DriverPhone,
                EstimatedArrivalTime = activeAssignment.EstimatedArrivalTime,
                AssignedAt = activeAssignment.AssignedAt
            }
        };
    }
}
