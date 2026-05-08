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
        // ── 1. Guest counts via parallel CountAsync (no full table load) ──────────
        var activeGuests = _context.Guests.AsNoTracking().Where(g => g.IsActive);

        var totalGuestsTask          = activeGuests.CountAsync(cancellationToken);
        var arrivingTask             = activeGuests.CountAsync(g => g.Status == GuestStatus.ArrivedAtAirport, cancellationToken);
        var receivedByEmbassyTask    = activeGuests.CountAsync(g => g.Status == GuestStatus.ReceivedByEmbassy, cancellationToken);
        var onTheWayToHotelTask      = activeGuests.CountAsync(g => g.Status == GuestStatus.OnTheWayToHotel, cancellationToken);
        var atHotelTask              = activeGuests.CountAsync(g => g.Status == GuestStatus.AtHotel, cancellationToken);
        var departingTask            = activeGuests.CountAsync(g => g.Status == GuestStatus.DepartingHotel || g.Status == GuestStatus.AtAirportDeparture, cancellationToken);
        var guestsDeservingTask      = activeGuests.CountAsync(g => g.DeservedCarClassId.HasValue, cancellationToken);

        // Guests without any active vehicle assignment
        var guestsWithoutVehicleTask = activeGuests
            .CountAsync(g => !_context.VehicleAssignments.Any(va => va.GuestId == g.Id && va.IsActive), cancellationToken);

        // Guests with active assignment but DedicatedCar != "True"
        var guestsAssignedNoDedicatedTask = activeGuests
            .CountAsync(g => _context.VehicleAssignments.Any(va => va.GuestId == g.Id && va.IsActive) && g.DedicatedCar != "True", cancellationToken);

        // ── 2. Guest status groups (lightweight projection, no vehicle navigation) ─
        var statusGroupsTask = activeGuests
            .Select(g => new
            {
                g.Id, g.FirstName, g.LastName, g.Designation, g.Nationality,
                g.PhotoUrl, g.IsCritical, g.RequiresAccessibility, g.Status,
                g.Notes, g.DeservedCarClassId,
                ActiveVehiclePlate = _context.VehicleAssignments
                    .Where(va => va.GuestId == g.Id && va.IsActive)
                    .Select(va => _context.Vehicles
                        .Where(v => v.Id == va.VehicleId)
                        .Select(v => v.LicensePlate)
                        .FirstOrDefault())
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // ── 3. Alerts ─────────────────────────────────────────────────────────────
        var alertsTask = _context.Alerts.AsNoTracking()
            .Include(a => a.Guest)
            .Where(a => !a.IsResolved)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        // ── 4. Fleet ──────────────────────────────────────────────────────────────
        var vehiclesTask = _context.Vehicles.AsNoTracking()
            .Where(v => v.IsActive)
            .Include(v => v.CarClass)
            .ToListAsync(cancellationToken);

        var carClassesTask = _context.CarClasses.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);

        var driversTask = _context.Drivers.AsNoTracking()
            .Where(d => d.IsActive)
            .ToListAsync(cancellationToken);

        // ── 5. Recent activity ────────────────────────────────────────────────────
        var recentAssignmentsTask = _context.VehicleAssignments.AsNoTracking()
            .Include(va => va.Guest)
            .Include(va => va.Vehicle)
                .ThenInclude(v => v.Driver)
            .OrderByDescending(va => va.AssignedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        // ── Await all in parallel ─────────────────────────────────────────────────
        await Task.WhenAll(
            totalGuestsTask, arrivingTask, receivedByEmbassyTask,
            onTheWayToHotelTask, atHotelTask, departingTask,
            guestsDeservingTask, guestsWithoutVehicleTask, guestsAssignedNoDedicatedTask,
            statusGroupsTask, alertsTask, vehiclesTask, carClassesTask,
            driversTask, recentAssignmentsTask
        );

        var statusGroupRows  = await statusGroupsTask;
        var activeAlerts     = await alertsTask;
        var vehicles         = await vehiclesTask;
        var carClasses       = await carClassesTask;
        var drivers          = await driversTask;
        var recentAssignments = await recentAssignmentsTask;

        // ── Build status groups from projection ───────────────────────────────────
        var statusGroups = statusGroupRows
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
                    ActiveVehiclePlate = g.ActiveVehiclePlate,
                    Notes = g.Notes
                }).ToList()
            })
            .ToList();

        // ── Guests deserving per class ────────────────────────────────────────────
        var guestsByClass = statusGroupRows
            .Where(g => g.DeservedCarClassId.HasValue)
            .GroupBy(g => g.DeservedCarClassId!.Value)
            .ToDictionary(grp => grp.Key, grp => grp.Count());

        // ── Recent activity ───────────────────────────────────────────────────────
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

        return new DashboardSummaryDto
        {
            TotalGuests                  = await totalGuestsTask,
            ArrivingCount                = await arrivingTask,
            ReceivedByEmbassyCount       = await receivedByEmbassyTask,
            OnTheWayToHotelCount         = await onTheWayToHotelTask,
            AtHotelCount                 = await atHotelTask,
            DepartingCount               = await departingTask,
            ActiveAlertsCount            = activeAlerts.Count,
            CriticalAlertsCount          = activeAlerts.Count(a => a.Severity == AlertSeverity.Critical || a.Severity == AlertSeverity.High),
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
            VehiclesTotal                = vehicles.Count,
            VehiclesAvailable            = vehicles.Count(v => v.Status == VehicleStatus.Available),
            VehiclesAssigned             = vehicles.Count(v => v.Status == VehicleStatus.Assigned),
            VehiclesOutOfService         = vehicles.Count(v => v.Status == VehicleStatus.OutOfService),
            DriversTotal                 = drivers.Count,
            DriversAvailable             = drivers.Count(d => d.Status == DriverStatus.Available),
            DriversAssigned              = drivers.Count(d => d.Status == DriverStatus.Assigned),
            GuestsWithoutVehicle         = await guestsWithoutVehicleTask,
            GuestsAssignedWithoutDedicatedCar = await guestsAssignedNoDedicatedTask,
            GuestsDeservingVehicle       = await guestsDeservingTask,

            // Fleet by class
            FleetByClass = carClasses.Select(cc => new FleetByClassDto
            {
                ClassId        = cc.Id,
                ClassName      = cc.Name,
                ClassColor     = cc.Color,
                SortOrder      = cc.SortOrder,
                TotalVehicles  = vehicles.Count(v => v.CarClassId == cc.Id),
                Available      = vehicles.Count(v => v.CarClassId == cc.Id && v.Status == VehicleStatus.Available),
                Assigned       = vehicles.Count(v => v.CarClassId == cc.Id && v.Status == VehicleStatus.Assigned),
                OutOfService   = vehicles.Count(v => v.CarClassId == cc.Id && v.Status == VehicleStatus.OutOfService),
                GuestsDeserving = guestsByClass.TryGetValue(cc.Id, out var cnt) ? cnt : 0,
            }).ToList(),

            // Recent activity
            RecentActivity = recentActivity,
        };
    }
}
