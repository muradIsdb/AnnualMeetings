using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Application.DTOs.Guests;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Guests.Queries;

public record GetArrivalFlightsQuery(string? ActiveEventCode = null) : IRequest<List<ArrivalFlightGroupDto>>;

public class GetArrivalFlightsQueryHandler : IRequestHandler<GetArrivalFlightsQuery, List<ArrivalFlightGroupDto>>
{
    private readonly IAppDbContext _context;

    public GetArrivalFlightsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ArrivalFlightGroupDto>> Handle(GetArrivalFlightsQuery request, CancellationToken cancellationToken)
    {
        // Load all active guests with their inbound travel booking + flight
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
                    ArrivalTime = g.TravelBookings
                        .Where(tb => tb.IsArrival)
                        .Select(tb => (DateTime?)tb.Flight.ScheduledArrival)
                        .FirstOrDefault(),
                    ActiveVehiclePlate = g.VehicleAssignments
                        .Where(va => va.IsActive)
                        .Select(va => va.Vehicle.LicensePlate)
                        .FirstOrDefault(),
                    InboundStatus = g.InboundStatus,
                    InboundStatusLabel = g.InboundStatus.ToString()
                },
                FlightId = g.TravelBookings
                    .Where(tb => tb.IsArrival)
                    .Select(tb => (Guid?)tb.FlightId)
                    .FirstOrDefault(),
                FlightNumber = g.TravelBookings
                    .Where(tb => tb.IsArrival)
                    .Select(tb => tb.Flight.FlightNumber)
                    .FirstOrDefault(),
                AirlineName = g.TravelBookings
                    .Where(tb => tb.IsArrival)
                    .Select(tb => tb.Flight.AirlineName)
                    .FirstOrDefault(),
                AirlineIataCode = g.TravelBookings
                    .Where(tb => tb.IsArrival)
                    .Select(tb => tb.Flight.AirlineIataCode)
                    .FirstOrDefault(),
                ScheduledArrival = g.TravelBookings
                    .Where(tb => tb.IsArrival)
                    .Select(tb => (DateTime?)tb.Flight.ScheduledArrival)
                    .FirstOrDefault(),
                ActualArrival = g.TravelBookings
                    .Where(tb => tb.IsArrival)
                    .Select(tb => tb.Flight.ActualArrival)
                    .FirstOrDefault(),
                Terminal = g.TravelBookings
                    .Where(tb => tb.IsArrival)
                    .Select(tb => tb.Flight.ActualTerminal)
                    .FirstOrDefault(),
                Gate = g.TravelBookings
                    .Where(tb => tb.IsArrival)
                    .Select(tb => tb.Flight.ActualGate)
                    .FirstOrDefault(),
                FlightStatus = g.TravelBookings
                    .Where(tb => tb.IsArrival)
                    .Select(tb => (FlightStatus?)tb.Flight.Status)
                    .FirstOrDefault(),
                LiveDelayMinutes = g.TravelBookings
                    .Where(tb => tb.IsArrival)
                    .Select(tb => tb.Flight.LiveDelayMinutes)
                    .FirstOrDefault(),
                LastTrackedAt = g.TravelBookings
                    .Where(tb => tb.IsArrival)
                    .Select(tb => tb.Flight.LastTrackedAt)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // Group by FlightId (null = no flight)
        var grouped = guests
            .GroupBy(g => g.FlightId)
            .Select(grp =>
            {
                var first = grp.First();
                return new ArrivalFlightGroupDto
                {
                    FlightId = grp.Key,
                    FlightNumber = grp.Key.HasValue ? first.FlightNumber : null,
                    AirlineName = grp.Key.HasValue ? first.AirlineName : null,
                    AirlineIataCode = grp.Key.HasValue ? first.AirlineIataCode : null,
                    ScheduledArrival = grp.Key.HasValue ? first.ScheduledArrival : null,
                    ActualArrival = grp.Key.HasValue ? first.ActualArrival : null,
                    Terminal = grp.Key.HasValue ? first.Terminal : null,
                    Gate = grp.Key.HasValue ? first.Gate : null,
                    FlightStatus = grp.Key.HasValue
                        ? (first.FlightStatus?.ToString() ?? "Unknown")
                        : "Unknown",
                    LiveDelayMinutes = grp.Key.HasValue ? first.LiveDelayMinutes : null,
                    LastTrackedAt = grp.Key.HasValue ? first.LastTrackedAt : null,
                    Guests = grp
                        .Select(g => g.Guest)
                        .OrderByDescending(g => g.IsCritical)
                        .ThenBy(g => g.FullName)
                        .ToList()
                };
            })
            // Flights with a scheduled arrival first, sorted ascending (soonest first)
            // No-flight group goes to the end
            .OrderBy(f => f.ScheduledArrival.HasValue ? 0 : 1)
            .ThenBy(f => f.ScheduledArrival)
            .ToList();

        return grouped;
    }
}
