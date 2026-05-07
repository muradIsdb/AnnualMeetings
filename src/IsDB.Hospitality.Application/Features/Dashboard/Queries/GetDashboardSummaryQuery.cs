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
        // Guests (active only)
        var guests = await _context.Guests
            .Where(g => g.IsActive)
            .Include(g => g.VehicleAssignments.Where(va => va.IsActive))
                .ThenInclude(va => va.Vehicle)
            .ToListAsync(cancellationToken);

        // Alerts
        var activeAlerts = await _context.Alerts
            .Include(a => a.Guest)
            .Where(a => !a.IsResolved)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        // Fleet
        var vehicles = await _context.Vehicles
            .Where(v => v.IsActive)
            .Include(v => v.CarClass)
            .ToListAsync(cancellationToken);

        // Car classes (for per-class breakdown including classes with 0 vehicles)
        var carClasses = await _context.CarClasses
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);

        // Guests deserving each class
        var guestsByClass = guests
            .Where(g => g.DeservedCarClassId.HasValue)
            .GroupBy(g => g.DeservedCarClassId!.Value)
            .ToDictionary(grp => grp.Key, grp => grp.Count());

        var drivers = await _context.Drivers
            .Where(d => d.IsActive)
            .ToListAsync(cancellationToken);

        // Recent activity — last 20 vehicle assignments
        var recentAssignments = await _context.VehicleAssignments
            .Include(va => va.Guest)
            .Include(va => va.Vehicle)
                .ThenInclude(v => v.Driver)
            .OrderByDescending(va => va.AssignedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var recentActivity = recentAssignments.Select(va => new ActivityItemDto
        {
            Type = va.IsActive ? "VehicleAssigned" : "VehicleUnassigned",
            GuestName = va.Guest != null ? va.Guest.FirstName + " " + va.Guest.LastName : "Unknown",
            VehiclePlate = va.Vehicle?.LicensePlate,
            DriverName = va.Vehicle?.Driver?.FullName ?? va.Vehicle?.DriverName,
            Detail = va.IsActive
                ? va.AssignmentType.ToString() + " assignment"
                : $"Unassigned at {(va.UnassignedAt.HasValue ? va.UnassignedAt.Value.ToString("HH:mm") : "N/A")}",
            OccurredAt = va.IsActive ? va.AssignedAt : (va.UnassignedAt ?? va.AssignedAt),
        }).ToList();

        // Status groups
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
            GuestsByStatus = statusGroups,

            // Fleet stats
            VehiclesTotal = vehicles.Count,
            VehiclesAvailable = vehicles.Count(v => v.Status == VehicleStatus.Available),
            VehiclesAssigned = vehicles.Count(v => v.Status == VehicleStatus.Assigned),
            VehiclesOutOfService = vehicles.Count(v => v.Status == VehicleStatus.OutOfService),
            DriversTotal = drivers.Count,
            DriversAvailable = drivers.Count(d => d.Status == DriverStatus.Available),
            DriversAssigned = drivers.Count(d => d.Status == DriverStatus.Assigned),
            GuestsWithoutVehicle = guests.Count(g => !g.VehicleAssignments.Any(va => va.IsActive)),
            GuestsAssignedWithoutDedicatedCar = guests.Count(g =>
                g.VehicleAssignments.Any(va => va.IsActive) && g.DedicatedCar != "True"),
            GuestsDeservingVehicle = guests.Count(g => g.DeservedCarClassId.HasValue),

            // Fleet by class
            FleetByClass = carClasses.Select(cc => new FleetByClassDto
            {
                ClassId = cc.Id,
                ClassName = cc.Name,
                ClassColor = cc.Color,
                SortOrder = cc.SortOrder,
                TotalVehicles = vehicles.Count(v => v.CarClassId == cc.Id),
                Available = vehicles.Count(v => v.CarClassId == cc.Id && v.Status == VehicleStatus.Available),
                Assigned = vehicles.Count(v => v.CarClassId == cc.Id && v.Status == VehicleStatus.Assigned),
                OutOfService = vehicles.Count(v => v.CarClassId == cc.Id && v.Status == VehicleStatus.OutOfService),
                GuestsDeserving = guestsByClass.TryGetValue(cc.Id, out var cnt) ? cnt : 0,
            }).ToList(),

            // Recent activity
            RecentActivity = recentActivity,
        };
    }
}
