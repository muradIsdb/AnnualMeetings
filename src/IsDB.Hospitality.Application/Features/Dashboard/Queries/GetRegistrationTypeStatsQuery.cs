using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Dashboard.Queries;

public record GetRegistrationTypeStatsQuery(string? ActiveEventCode = null) : IRequest<List<RegistrationTypeStatsDto>>;

public class GetRegistrationTypeStatsQueryHandler
    : IRequestHandler<GetRegistrationTypeStatsQuery, List<RegistrationTypeStatsDto>>
{
    private readonly IAppDbContext _context;

    public GetRegistrationTypeStatsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<List<RegistrationTypeStatsDto>> Handle(
        GetRegistrationTypeStatsQuery request,
        CancellationToken cancellationToken)
    {
        var guests = await _context.Guests
            .Where(g => g.IsActive
                && (request.ActiveEventCode == null || g.EventCode == null || g.EventCode == request.ActiveEventCode))
            .Select(g => new
            {
                g.RegistrationTypeName,
                g.InboundStatus,
                g.OutboundStatus,
                g.Status,
            })
            .ToListAsync(cancellationToken);

        var grouped = guests
            .GroupBy(g => string.IsNullOrWhiteSpace(g.RegistrationTypeName)
                ? "Unspecified"
                : g.RegistrationTypeName)
            .Select(grp => new RegistrationTypeStatsDto
            {
                RegistrationTypeName = grp.Key,
                Total = grp.Count(),
                ArrivalScheduled = grp.Count(g => g.InboundStatus == InboundStatus.ArrivalScheduled),
                Arrived = grp.Count(g =>
                    g.InboundStatus == InboundStatus.Arrived ||
                    g.InboundStatus == InboundStatus.ReceivedByEmbassyTeam ||
                    g.InboundStatus == InboundStatus.VehicleAssigned),
                InTransitToHotel = grp.Count(g =>
                    g.Status == GuestStatus.OnTheWayToHotel),
                AtHotel = grp.Count(g => g.InboundStatus == InboundStatus.AtHotel),
                Departed = grp.Count(g =>
                    g.Status == GuestStatus.DepartingHotel ||
                    g.Status == GuestStatus.AtAirportDeparture ||
                    g.Status == GuestStatus.Departed),
            })
            .OrderBy(r => r.RegistrationTypeName)
            .ToList();

        return grouped;
    }
}
