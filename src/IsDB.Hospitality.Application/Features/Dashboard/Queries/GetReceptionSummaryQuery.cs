using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Dashboard.Queries;

public record GetReceptionSummaryQuery(DateTime From, DateTime To) : IRequest<ReceptionSummaryDto>;

public class GetReceptionSummaryQueryHandler : IRequestHandler<GetReceptionSummaryQuery, ReceptionSummaryDto>
{
    private readonly IAppDbContext _context;

    public GetReceptionSummaryQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ReceptionSummaryDto> Handle(GetReceptionSummaryQuery request, CancellationToken cancellationToken)
    {
        // Normalise date range to UTC day boundaries
        var fromUtc = DateTime.SpecifyKind(request.From.Date, DateTimeKind.Utc);
        var toUtc   = DateTime.SpecifyKind(request.To.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        // ── 1. Find all active inbound travel bookings in the date range ─────────
        var bookingsInRange = await _context.TravelBookings
            .AsNoTracking()
            .Include(tb => tb.Flight)
            .Include(tb => tb.Guest)
            .Where(tb =>
                tb.IsArrival &&
                tb.Guest.IsActive &&
                tb.Flight.ScheduledArrival >= fromUtc &&
                tb.Flight.ScheduledArrival <= toUtc)
            .ToListAsync(cancellationToken);

        // ── 2. Active vehicle assignments ────────────────────────────────────────
        var guestIds = bookingsInRange.Select(tb => tb.GuestId).Distinct().ToHashSet();
        var activeAssignments = await _context.VehicleAssignments
            .AsNoTracking()
            .Where(va => va.IsActive && guestIds.Contains(va.GuestId))
            .Select(va => new { va.GuestId, va.Vehicle!.LicensePlate })
            .ToListAsync(cancellationToken);
        var assignmentMap = activeAssignments.ToDictionary(a => a.GuestId, a => a.LicensePlate);

        // ── 3. Build per-guest data (one entry per unique guest) ─────────────────
        // A guest may have multiple travel bookings; pick the earliest inbound arrival
        var guestBookings = bookingsInRange
            .GroupBy(tb => tb.GuestId)
            .Select(g =>
            {
                var earliest = g.OrderBy(tb => tb.Flight.ScheduledArrival).First();
                return new
                {
                    Guest        = earliest.Guest,
                    Flight       = earliest.Flight,
                    Booking      = earliest,
                    HasVehicle   = assignmentMap.ContainsKey(earliest.GuestId),
                    VehiclePlate = assignmentMap.TryGetValue(earliest.GuestId, out var p) ? p : null
                };
            })
            .OrderBy(x => x.Flight.ScheduledArrival)
            .ToList();

        // ── 4. KPI counts ────────────────────────────────────────────────────────
        static string InboundLabel(InboundStatus s) => s switch
        {
            InboundStatus.ArrivalScheduled   => "Scheduled",
            InboundStatus.Arrived            => "Arrived at Airport",
            InboundStatus.ReceivedByEmbassyTeam => "Received by Embassy",
            InboundStatus.VehicleAssigned    => "In Transit → Hotel",
            InboundStatus.AtHotel            => "At Hotel",
            _                                => s.ToString()
        };

        int total      = guestBookings.Count;
        
        // Arrival (current status)
        int scheduled  = guestBookings.Count(x => x.Guest.InboundStatus == InboundStatus.ArrivalScheduled);
        int arrived    = guestBookings.Count(x => x.Guest.InboundStatus == InboundStatus.Arrived);
        int received   = guestBookings.Count(x => x.Guest.InboundStatus == InboundStatus.ReceivedByEmbassyTeam);
        int inTransit  = guestBookings.Count(x => x.Guest.InboundStatus == InboundStatus.VehicleAssigned);
        int atHotel    = guestBookings.Count(x => x.Guest.InboundStatus == InboundStatus.AtHotel);
        
        // Arrival (cumulative)
        // EverArrived: Any status >= Arrived (1)
        int everArrived = guestBookings.Count(x => (int)x.Guest.InboundStatus >= (int)InboundStatus.Arrived);
        // EverReceived: Any status >= ReceivedByEmbassyTeam (2)
        int everReceived = guestBookings.Count(x => (int)x.Guest.InboundStatus >= (int)InboundStatus.ReceivedByEmbassyTeam);
        
        // Departure (current status)
        int inTransferToAirport = guestBookings.Count(x => x.Guest.OutboundStatus == OutboundStatus.InTransferToAirport);
        int atAirport           = guestBookings.Count(x => x.Guest.OutboundStatus == OutboundStatus.AtAirport);
        int boardingCompleted   = guestBookings.Count(x => x.Guest.OutboundStatus == OutboundStatus.BoardingCompleted);

        // ── 5. Alert guests ──────────────────────────────────────────────────────
        var criticalGuests = guestBookings
            .Where(x => x.Guest.IsCritical)
            .Select(x => new ReceptionAlertGuestDto
            {
                Id                    = x.Guest.Id,
                FullName              = $"{x.Guest.FirstName} {x.Guest.LastName}".Trim(),
                Designation           = x.Guest.Designation,
                Nationality           = x.Guest.Nationality,
                IsCritical            = true,
                RequiresAccessibility = x.Guest.RequiresAccessibility,
                HasVehicle            = x.HasVehicle,
                FlightNumber          = x.Flight.FlightNumber,
                ScheduledArrival      = x.Flight.ScheduledArrival,
                InboundStatusLabel    = InboundLabel(x.Guest.InboundStatus)
            }).ToList();

        var accessibilityGuests = guestBookings
            .Where(x => x.Guest.RequiresAccessibility && !x.Guest.IsCritical)
            .Select(x => new ReceptionAlertGuestDto
            {
                Id                    = x.Guest.Id,
                FullName              = $"{x.Guest.FirstName} {x.Guest.LastName}".Trim(),
                Designation           = x.Guest.Designation,
                Nationality           = x.Guest.Nationality,
                IsCritical            = false,
                RequiresAccessibility = true,
                HasVehicle            = x.HasVehicle,
                FlightNumber          = x.Flight.FlightNumber,
                ScheduledArrival      = x.Flight.ScheduledArrival,
                InboundStatusLabel    = InboundLabel(x.Guest.InboundStatus)
            }).ToList();

        // ── 6. Flights timeline ──────────────────────────────────────────────────
        var flights = guestBookings
            .GroupBy(x => x.Flight.Id)
            .Select(g =>
            {
                var f = g.First().Flight;
                return new ReceptionFlightDto
                {
                    FlightId         = f.Id,
                    FlightNumber     = f.FlightNumber,
                    AirlineName      = f.AirlineName,
                    AirlineIataCode  = f.AirlineIataCode,
                    ScheduledArrival = f.ScheduledArrival,
                    DeparturePortName = f.DeparturePortName,
                    ArrivalPortName  = f.ArrivalPortName,
                    ActualTerminal   = f.ActualTerminal,
                    ActualGate       = f.ActualGate,
                    ActualArrival    = f.ActualArrival,
                    FlightStatus     = f.Status.ToString(),
                    LiveDelayMinutes = f.LiveDelayMinutes,
                    TotalGuests      = g.Count(),
                    Scheduled        = g.Count(x => x.Guest.InboundStatus == InboundStatus.ArrivalScheduled),
                    ArrivedAtAirport = g.Count(x => x.Guest.InboundStatus == InboundStatus.Arrived),
                    ReceivedByEmbassy = g.Count(x => x.Guest.InboundStatus == InboundStatus.ReceivedByEmbassyTeam),
                    InTransitToHotel = g.Count(x => x.Guest.InboundStatus == InboundStatus.VehicleAssigned)
                };
            })
            .OrderBy(f => f.ScheduledArrival)
            .ToList();

        // ── 7. Guest list ────────────────────────────────────────────────────────
        var guests = guestBookings
            .Select(x => new ReceptionGuestDto
            {
                Id                    = x.Guest.Id,
                FullName              = $"{x.Guest.FirstName} {x.Guest.LastName}".Trim(),
                Designation           = x.Guest.Designation,
                Nationality           = x.Guest.Nationality,
                FlightNumber          = x.Flight.FlightNumber,
                AirlineName           = x.Flight.AirlineName,
                ScheduledArrival      = x.Flight.ScheduledArrival,
                ActualArrival         = x.Flight.ActualArrival,
                InboundStatusLabel    = InboundLabel(x.Guest.InboundStatus),
                InboundStatusValue    = (int)x.Guest.InboundStatus,
                ActiveVehiclePlate    = x.VehiclePlate,
                IsCritical            = x.Guest.IsCritical,
                RequiresAccessibility = x.Guest.RequiresAccessibility,
                FlightCancelled       = x.Flight.Status == FlightStatus.Cancelled
            })
            .ToList();

        return new ReceptionSummaryDto
        {
            TotalArriving       = total,
            Scheduled           = scheduled,
            ArrivedAtAirport    = arrived,
            ReceivedByEmbassy   = received,
            InTransitToHotel    = inTransit,
            AtHotel             = atHotel,
            EverArrived         = everArrived,
            EverReceived        = everReceived,
            InTransferToAirport = inTransferToAirport,
            AtAirport           = atAirport,
            BoardingCompleted   = boardingCompleted,
            CriticalGuests      = criticalGuests,
            AccessibilityGuests = accessibilityGuests,
            Flights             = flights,
            Guests              = guests
        };
    }
}
