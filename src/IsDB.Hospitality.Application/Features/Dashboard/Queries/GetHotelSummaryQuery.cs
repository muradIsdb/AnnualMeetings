using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Dashboard.Queries;

public record GetHotelSummaryQuery(string? ActiveEventCode = null) : IRequest<HotelSummaryDto>;

public class GetHotelSummaryQueryHandler : IRequestHandler<GetHotelSummaryQuery, HotelSummaryDto>
{
    private readonly IAppDbContext _context;

    public GetHotelSummaryQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<HotelSummaryDto> Handle(GetHotelSummaryQuery request, CancellationToken cancellationToken)
    {
        // ── 1. Load all active guests ────────────────────────────────────────────
        var guests = await _context.Guests
            .AsNoTracking()
            .Include(g => g.StatusHistory)
            .Where(g => g.IsActive
                && (request.ActiveEventCode == null || g.EventCode == null || g.EventCode == request.ActiveEventCode))
            .ToListAsync(cancellationToken);

        // ── 2. KPI counts ────────────────────────────────────────────────────────
        var atHotelGuests = guests
            .Where(g => g.InboundStatus == InboundStatus.AtHotel)
            .ToList();

        int totalAtHotel = atHotelGuests.Count;

        // En Route: VehicleAssigned OR (ReceivedByEmbassyTeam flag = true AND not yet AtHotel)
        int enRouteToHotel = guests.Count(g =>
            g.InboundStatus != InboundStatus.AtHotel &&
            (g.InboundStatus == InboundStatus.VehicleAssigned || g.ReceivedByEmbassyTeam));

        // Departing: any outbound status set
        int departingActive = atHotelGuests.Count(g => g.OutboundStatus.HasValue);

        // No room assigned: AtHotel but no room number
        int noRoomAssigned = atHotelGuests.Count(g => string.IsNullOrWhiteSpace(g.RoomNumber));

        // ── 3. Per-hotel guest counts ────────────────────────────────────────────
        var byHotel = atHotelGuests
            .Where(g => !string.IsNullOrWhiteSpace(g.HotelName))
            .GroupBy(g => g.HotelName!)
            .Select(grp => new HotelGuestCountDto
            {
                HotelName     = grp.Key,
                GuestCount    = grp.Count(),
                WithRoomCount = grp.Count(g => !string.IsNullOrWhiteSpace(g.RoomNumber)),
                NoRoomCount   = grp.Count(g => string.IsNullOrWhiteSpace(g.RoomNumber))
            })
            .OrderByDescending(h => h.GuestCount)
            .ToList();

        // Guests with no hotel name assigned — group as "Unassigned"
        int unassignedHotel = atHotelGuests.Count(g => string.IsNullOrWhiteSpace(g.HotelName));
        if (unassignedHotel > 0)
        {
            byHotel.Add(new HotelGuestCountDto
            {
                HotelName     = "Unassigned",
                GuestCount    = unassignedHotel,
                WithRoomCount = 0,
                NoRoomCount   = unassignedHotel
            });
        }

        // ── 4. Departure status breakdown ────────────────────────────────────────
        int outboundAtHotel     = atHotelGuests.Count(g => !g.OutboundStatus.HasValue || g.OutboundStatus == OutboundStatus.AtHotel);
        int inTransferToAirport = atHotelGuests.Count(g => g.OutboundStatus == OutboundStatus.InTransferToAirport);
        int atAirport           = atHotelGuests.Count(g => g.OutboundStatus == OutboundStatus.AtAirport);
        int boardingCompleted   = atHotelGuests.Count(g => g.OutboundStatus == OutboundStatus.BoardingCompleted);

        // ── 5. Recent check-ins (last 10 guests who reached AtHotel) ────────────
        static string Initials(string firstName, string lastName)
        {
            var f = string.IsNullOrWhiteSpace(firstName) ? "" : firstName.Trim()[0].ToString().ToUpper();
            var l = string.IsNullOrWhiteSpace(lastName)  ? "" : lastName.Trim()[0].ToString().ToUpper();
            return f + l;
        }

        var recentCheckins = atHotelGuests
            .Select(g =>
            {
                // Find the history entry where AtHotel was set
                var checkinEntry = g.StatusHistory
                    .Where(h => h.Track == StatusTrack.Inbound && h.StatusValue == (int)InboundStatus.AtHotel)
                    .OrderByDescending(h => h.CreatedAt)
                    .FirstOrDefault();

                return new HotelRecentCheckinDto
                {
                    Id          = g.Id,
                    FullName    = $"{g.FirstName} {g.LastName}".Trim(),
                    Initials    = Initials(g.FirstName, g.LastName),
                    HotelName   = g.HotelName,
                    RoomNumber  = g.RoomNumber,
                    CheckedInAt = checkinEntry?.CreatedAt
                };
            })
            .OrderByDescending(x => x.CheckedInAt ?? DateTime.MinValue)
            .Take(10)
            .ToList();

        // ── 6. Guests without room number ────────────────────────────────────────
        var guestsWithoutRoom = atHotelGuests
            .Where(g => string.IsNullOrWhiteSpace(g.RoomNumber))
            .Select(g =>
            {
                var checkinEntry = g.StatusHistory
                    .Where(h => h.Track == StatusTrack.Inbound && h.StatusValue == (int)InboundStatus.AtHotel)
                    .OrderByDescending(h => h.CreatedAt)
                    .FirstOrDefault();

                return new HotelNoRoomGuestDto
                {
                    Id          = g.Id,
                    FullName    = $"{g.FirstName} {g.LastName}".Trim(),
                    Initials    = Initials(g.FirstName, g.LastName),
                    HotelName   = g.HotelName,
                    CheckedInAt = checkinEntry?.CreatedAt
                };
            })
            .OrderBy(x => x.CheckedInAt ?? DateTime.MaxValue)
            .ToList();

        return new HotelSummaryDto
        {
            TotalAtHotel        = totalAtHotel,
            EnRouteToHotel      = enRouteToHotel,
            DepartingActive     = departingActive,
            NoRoomAssigned      = noRoomAssigned,
            ByHotel             = byHotel,
            OutboundAtHotel     = outboundAtHotel,
            InTransferToAirport = inTransferToAirport,
            AtAirport           = atAirport,
            BoardingCompleted   = boardingCompleted,
            RecentCheckins      = recentCheckins,
            GuestsWithoutRoom   = guestsWithoutRoom
        };
    }
}
