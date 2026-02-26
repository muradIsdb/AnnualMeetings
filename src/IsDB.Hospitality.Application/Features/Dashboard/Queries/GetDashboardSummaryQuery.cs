using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Alerts;
using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Application.DTOs.Guests;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Dashboard.Queries;

public record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IAppDbContext _context;

    public GetDashboardSummaryQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var guests = await _context.Guests
            .Include(g => g.VehicleAssignments.Where(va => va.IsActive))
                .ThenInclude(va => va.Vehicle)
            .ToListAsync(cancellationToken);

        var activeAlerts = await _context.Alerts
            .Include(a => a.Guest)
            .Where(a => !a.IsResolved)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var statusGroups = guests
            .GroupBy(g => g.Status)
            .Select(grp => new GuestStatusGroupDto
            {
                StatusLabel = grp.Key.ToString(),
                Count = grp.Count(),
                Guests = grp.OrderByDescending(g => g.IsCritical).Select(g => new GuestSummaryDto
                {
                    Id = g.Id,
                    FullName = g.FirstName + " " + g.LastName,
                    Designation = g.Designation,
                    Nationality = g.Nationality,
                    PhotoUrl = g.PhotoUrl,
                    IsCritical = g.IsCritical,
                    RequiresAccessibility = g.RequiresAccessibility,
                    StatusLabel = g.Status.ToString(),
                    ActiveVehiclePlate = g.VehicleAssignments.FirstOrDefault()?.Vehicle.LicensePlate,
                    Notes = g.Notes
                }).ToList()
            })
            .ToList();

        return new DashboardSummaryDto
        {
            TotalGuests = guests.Count,
            ArrivingCount = guests.Count(g => g.Status == GuestStatus.ArrivedAtAirport),
            ReceivedByEmbassyCount = guests.Count(g => g.Status == GuestStatus.ReceivedByEmbassy),
            OnTheWayToHotelCount = guests.Count(g => g.Status == GuestStatus.OnTheWayToHotel),
            AtHotelCount = guests.Count(g => g.Status == GuestStatus.AtHotel),
            DepartingCount = guests.Count(g => g.Status == GuestStatus.DepartingHotel || g.Status == GuestStatus.AtAirportDeparture),
            ActiveAlertsCount = activeAlerts.Count,
            CriticalAlertsCount = activeAlerts.Count(a => a.Severity == AlertSeverity.Critical || a.Severity == AlertSeverity.High),
            ActiveAlerts = activeAlerts.Select(a => new AlertDto
            {
                Id = a.Id,
                GuestId = a.GuestId,
                GuestName = a.Guest != null ? a.Guest.FirstName + " " + a.Guest.LastName : null,
                Title = a.Title,
                Message = a.Message,
                Severity = a.Severity,
                SeverityLabel = a.Severity.ToString(),
                IsResolved = a.IsResolved,
                CreatedAt = a.CreatedAt
            }).ToList(),
            GuestsByStatus = statusGroups
        };
    }
}
