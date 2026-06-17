using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Application.DTOs.Guests;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace IsDB.Hospitality.Application.Features.Guests.Queries;
public record GetDepartureFlightsQuery(string? ActiveEventCode = null) : IRequest<List<DepartureFlightGroupDto>>;
public class GetDepartureFlightsQueryHandler : IRequestHandler<GetDepartureFlightsQuery, List<DepartureFlightGroupDto>>
{
    private readonly IAppDbContext _context;
    public GetDepartureFlightsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }
    public async Task<List<DepartureFlightGroupDto>> Handle(GetDepartureFlightsQuery request, CancellationToken cancellationToken)
    {
        // Load all active guests with their outbound travel booking + flight
        var guests = await _context.Guests
            .Where(g => g.IsActive
                && (request.ActiveEventCode == null || g.EventCode == null || g.EventCode == request.ActiveEventCode))
            .Select(g => new
            {
                Guest = new GuestSummaryDto
                {
                    Id = g.Id,
                    FullName = g.FirstName + " " + g.LastName,
                    Designation = g.Designation,
                    Nationality = g.Nationality,
                    Country = g.Country,
                    PhotoUrl = g.PhotoUrl,
                    IsCritical = g.IsCritical,
                    RequiresAccessibility = g.RequiresAccessibility,
                    StatusLabel = g.Status.ToString(),
                    Notes = g.Notes,
                    RegistrationTypeName = g.RegistrationTypeName,
                    RankValue = g.RankValue,
                    DedicatedCar = g.DedicatedCar,
                    LiaisonOfficer = g.LiaisonOfficer,
                    InvitedToOpeningCeremony = g.InvitedToOpeningCeremony,
                    OldHotel = g.OldHotel,
                    LiaisonOfficerName = g.LiaisonOfficerName,
                    LiaisonOfficerCarNumber = g.LiaisonOfficerCarNumber,
                    DeservedCarClassId = g.DeservedCarClassId,
                    DeservedCarClassName = g.DeservedCarClass != null ? g.DeservedCarClass.Name : null,
                    DeservedCarClassColor = g.DeservedCarClass != null ? g.DeservedCarClass.Color : null,
                    DepartureDate = g.TravelBookings
                        .Where(tb => !tb.IsArrival)
                        .Select(tb => (DateTime?)tb.Flight.ScheduledDeparture)
                        .FirstOrDefault(),
                    HotelName = g.HotelName,
                    RoomNumber = g.RoomNumber,
                    ActiveVehiclePlate = g.VehicleAssignments
                        .Where(va => va.IsActive)
                        .Select(va => va.Vehicle.LicensePlate)
                        .FirstOrDefault(),
                    OutboundStatus = g.OutboundStatus,
                    OutboundStatusLabel = g.OutboundStatus.ToString(),
                    InboundStatus = g.InboundStatus,
                    InboundStatusLabel = g.InboundStatus.ToString()
                },
                FlightId = g.TravelBookings
                    .Where(tb => !tb.IsArrival)
                    .Select(tb => (Guid?)tb.FlightId)
                    .FirstOrDefault(),
                FlightNumber = g.TravelBookings
                    .Where(tb => !tb.IsArrival)
                    .Select(tb => tb.Flight.FlightNumber)
                    .FirstOrDefault(),
                AirlineName = g.TravelBookings
                    .Where(tb => !tb.IsArrival)
                    .Select(tb => tb.Flight.AirlineName)
                    .FirstOrDefault(),
                AirlineIataCode = g.TravelBookings
                    .Where(tb => !tb.IsArrival)
                    .Select(tb => tb.Flight.AirlineIataCode)
                    .FirstOrDefault(),
                ScheduledDeparture = g.TravelBookings
                    .Where(tb => !tb.IsArrival)
                    .Select(tb => (DateTime?)tb.Flight.ScheduledDeparture)
                    .FirstOrDefault(),
                ActualDeparture = g.TravelBookings
                    .Where(tb => !tb.IsArrival)
                    .Select(tb => tb.Flight.ActualDeparture)
                    .FirstOrDefault(),
                Terminal = g.TravelBookings
                    .Where(tb => !tb.IsArrival)
                    .Select(tb => tb.Flight.ActualTerminal)
                    .FirstOrDefault(),
                Gate = g.TravelBookings
                    .Where(tb => !tb.IsArrival)
                    .Select(tb => tb.Flight.ActualGate)
                    .FirstOrDefault(),
                FlightStatus = g.TravelBookings
                    .Where(tb => !tb.IsArrival)
                    .Select(tb => (FlightStatus?)tb.Flight.Status)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        // Group by FlightId (null = no flight)
        var grouped = guests
            .GroupBy(g => g.FlightId)
            .Select(grp =>
            {
                var first = grp.First();
                return new DepartureFlightGroupDto
                {
                    FlightId = grp.Key,
                    FlightNumber = grp.Key.HasValue ? first.FlightNumber : null,
                    AirlineName = grp.Key.HasValue ? first.AirlineName : null,
                    AirlineIataCode = grp.Key.HasValue ? first.AirlineIataCode : null,
                    ScheduledDeparture = grp.Key.HasValue ? first.ScheduledDeparture : null,
                    ActualDeparture = grp.Key.HasValue ? first.ActualDeparture : null,
                    Terminal = grp.Key.HasValue ? first.Terminal : null,
                    Gate = grp.Key.HasValue ? first.Gate : null,
                    FlightStatus = grp.Key.HasValue
                        ? (first.FlightStatus?.ToString() ?? "Unknown")
                        : "Unknown",
                    Guests = grp
                        .Select(g => g.Guest)
                        .OrderByDescending(g => g.IsCritical)
                        .ThenBy(g => g.FullName)
                        .ToList()
                };
            })
            // Compute IsExpired: all guests have BoardingCompleted OR flight is 2+ hours past scheduled departure
            .Select(f =>
            {
                var allBoarded = f.Guests.All(g => g.OutboundStatus >= OutboundStatus.BoardingCompleted);
                var jeddahNow = DateTime.UtcNow.AddHours(3);
                var timePassed = f.ScheduledDeparture.HasValue
                    && jeddahNow > f.ScheduledDeparture.Value.AddHours(3);
                f.IsExpired = allBoarded || timePassed;
                return f;
            })
            // Expired flights move to the end
            .OrderBy(f => f.IsExpired ? 1 : 0)
            // Then: flights with a scheduled departure first, sorted ascending (soonest first)
            .ThenBy(f => f.ScheduledDeparture.HasValue ? 0 : 1)
            .ThenBy(f => f.ScheduledDeparture)
            .ToList();
        return grouped;
    }
}
