using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Alerts;
using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Application.DTOs.Guests;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Dashboard.Queries;

public record GetDashboardSummaryQuery(string? ActiveEventCode = null) : IRequest<DashboardSummaryDto>;

public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IAppDbContext _context;

    public GetDashboardSummaryQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var activeGuests = _context.Guests.AsNoTracking().Where(g => g.IsActive
            && (request.ActiveEventCode == null || g.EventCode == null || g.EventCode == request.ActiveEventCode));

        // ── 1. Count queries — sequential to avoid EF Core concurrent-context error ─
        // NOTE: Counts use InboundStatus/OutboundStatus and the ReceivedByEmbassyTeam boolean flag
        // (the fields updated by Airport/Hotel workflows) rather than the legacy GuestStatus field.
        // ReceivedByEmbassyTeam is a separate boolean flag — it does NOT change InboundStatus.
        var totalGuests       = await activeGuests.CountAsync(cancellationToken);
        // arrivingCount = guests past airport: Arrived + ReceivedByEmbassy flag + VehicleAssigned + AtHotel
        var arrivingCount     = await activeGuests.CountAsync(g =>
            g.InboundStatus == InboundStatus.Arrived ||
            g.ReceivedByEmbassyTeam ||
            g.InboundStatus == InboundStatus.VehicleAssigned ||
            g.InboundStatus == InboundStatus.AtHotel, cancellationToken);
        // receivedByEmbassy = cumulative: all guests who ever had embassy flag set (boolean, not enum)
        var receivedByEmbassy = await activeGuests.CountAsync(g => g.ReceivedByEmbassyTeam, cancellationToken);
        // atAirport = guests currently at airport: InboundStatus==Arrived AND not yet received by embassy
        var atAirport         = await activeGuests.CountAsync(g =>
            g.InboundStatus == InboundStatus.Arrived && !g.ReceivedByEmbassyTeam, cancellationToken);
        var onTheWayToHotel   = await activeGuests.CountAsync(g => g.InboundStatus == InboundStatus.VehicleAssigned, cancellationToken);
        var atHotel           = await activeGuests.CountAsync(g => g.InboundStatus == InboundStatus.AtHotel, cancellationToken);
        var departing         = await activeGuests.CountAsync(g =>
            g.OutboundStatus == OutboundStatus.InTransferToAirport ||
            g.OutboundStatus == OutboundStatus.AtAirport ||
            g.OutboundStatus == OutboundStatus.BoardingCompleted, cancellationToken);
        var guestsDeserving   = await activeGuests.CountAsync(g => g.DeservedCarClassId.HasValue, cancellationToken);

        // ── 2. Lightweight guest projection (only needed columns, no navigation) ───
        var guests = await activeGuests
            .Select(g => new
            {
                g.Id, g.FirstName, g.LastName, g.Designation, g.Nationality,
                g.Country, g.PhotoUrl, g.IsCritical, g.RequiresAccessibility,
                g.Status, g.Notes, g.DeservedCarClassId, g.DedicatedCar,
                g.RankValue, g.RegistrationTypeName, g.InboundStatus, g.OutboundStatus,
                g.HotelName, g.RoomNumber, g.Organization
            })
            .ToListAsync(cancellationToken);

        // ── 3. Active vehicle assignments (guestId → licensePlate) ───────────────
        var activeAssignments = await _context.VehicleAssignments.AsNoTracking()
            .Where(va => va.IsActive)
            .Select(va => new { va.GuestId, va.Vehicle!.LicensePlate })
            .ToListAsync(cancellationToken);

        // ── 4. Alerts ─────────────────────────────────────────────────────────────
        var activeAlerts = await _context.Alerts.AsNoTracking()
            .Include(a => a.Guest)
            .Where(a => !a.IsResolved)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        // ── 5. Fleet ──────────────────────────────────────────────────────────────
        var vehicles = await _context.Vehicles.AsNoTracking()
            .Where(v => v.IsActive
                && (request.ActiveEventCode == null || v.EventCode == null || v.EventCode == request.ActiveEventCode))
            .Include(v => v.CarClass)
            .ToListAsync(cancellationToken);

        var carClasses = await _context.CarClasses.AsNoTracking()
            .Where(c => request.ActiveEventCode == null || c.EventCode == null || c.EventCode == request.ActiveEventCode)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);

        var drivers = await _context.Drivers.AsNoTracking()
            .Where(d => d.IsActive)
            .ToListAsync(cancellationToken);

        // ── 6. Recent activity ────────────────────────────────────────────────────
        var recentAssignments = await _context.VehicleAssignments.AsNoTracking()
            .Include(va => va.Guest)
            .Include(va => va.Vehicle)
                .ThenInclude(v => v!.Driver)
            .OrderByDescending(va => va.AssignedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        // ── Build lookup: guestId → licensePlate ─────────────────────────────────
        var plateLookup = activeAssignments
            .GroupBy(a => a.GuestId)
            .ToDictionary(g => g.Key, g => g.First().LicensePlate);

        // Guests without any active vehicle assignment
        var guestsWithAssignment      = activeAssignments.Select(a => a.GuestId).ToHashSet();
        int guestsWithoutVehicle      = guests.Count(g => !guestsWithAssignment.Contains(g.Id));
        int guestsAssignedNoDedicated = guests.Count(g =>
            guestsWithAssignment.Contains(g.Id) && g.DedicatedCar != "True");

        // ── Build status groups ───────────────────────────────────────────────────
        var statusGroups = guests
            .GroupBy(g => g.Status)
            .Select(grp => new GuestStatusGroupDto
            {
                StatusLabel = grp.Key.ToString(),
                Count = grp.Count(),
                Guests = grp.OrderByDescending(g => g.IsCritical).Select(g => new GuestSummaryDto
                {
                    Id                    = g.Id,
                    FullName              = g.FirstName + " " + g.LastName,
                    Designation           = g.Designation,
                    Nationality           = g.Nationality,
                    Country               = g.Country,
                    PhotoUrl              = g.PhotoUrl,
                    IsCritical            = g.IsCritical,
                    RequiresAccessibility = g.RequiresAccessibility,
                    StatusLabel           = g.Status.ToString(),
                    ActiveVehiclePlate    = plateLookup.TryGetValue(g.Id, out var plate) ? plate : null,
                    Notes                 = g.Notes,
                    DedicatedCar          = g.DedicatedCar,
                    RankValue             = g.RankValue,
                    RegistrationTypeName  = g.RegistrationTypeName,
                    InboundStatus         = g.InboundStatus,
                    InboundStatusLabel    = g.InboundStatus.ToString(),
                    OutboundStatus        = g.OutboundStatus,
                    OutboundStatusLabel   = g.OutboundStatus?.ToString(),
                    HotelName             = g.HotelName,
                    RoomNumber            = g.RoomNumber,
                    Organization          = g.Organization,
                    DeservedCarClassId    = g.DeservedCarClassId,
                }).ToList()
            })
            .ToList();

        // Guests deserving per class
        var guestsByClass = guests
            .Where(g => g.DeservedCarClassId.HasValue)
            .GroupBy(g => g.DeservedCarClassId!.Value)
            .ToDictionary(grp => grp.Key, grp => grp.Count());

        // ── Recent activity ───────────────────────────────────────────────────────
        var recentActivity = recentAssignments.Select(va => new ActivityItemDto
        {
            Type         = va.IsActive ? "VehicleAssigned" : "VehicleUnassigned",
            GuestName    = va.Guest != null ? va.Guest.FirstName + " " + va.Guest.LastName : "Unknown",
            VehiclePlate = va.Vehicle?.LicensePlate,
            DriverName   = va.Vehicle?.Driver?.FullName ?? va.Vehicle?.DriverName,
            Detail       = va.IsActive
                ? va.AssignmentType.ToString() + " assignment"
                : $"Unassigned at {(va.UnassignedAt.HasValue ? va.UnassignedAt.Value.ToString("HH:mm") : "N/A")}",
            OccurredAt   = va.IsActive ? va.AssignedAt : (va.UnassignedAt ?? va.AssignedAt),
        }).ToList();

        return new DashboardSummaryDto
        {
            TotalGuests                       = totalGuests,
            ArrivingCount                     = arrivingCount,
            AtAirportCount                    = atAirport,
            ReceivedByEmbassyCount            = receivedByEmbassy,
            OnTheWayToHotelCount              = onTheWayToHotel,
            AtHotelCount                      = atHotel,
            DepartingCount                    = departing,
            ActiveAlertsCount                 = activeAlerts.Count,
            CriticalAlertsCount               = activeAlerts.Count(a => a.Severity == AlertSeverity.Critical || a.Severity == AlertSeverity.High),
            ActiveAlerts = activeAlerts.Select(a => new AlertDto
            {
                Id            = a.Id,
                GuestId       = a.GuestId,
                GuestName     = a.Guest != null ? a.Guest.FirstName + " " + a.Guest.LastName : null,
                Title         = a.Title,
                Message       = a.Message,
                Severity      = a.Severity,
                SeverityLabel = a.Severity.ToString(),
                IsResolved    = a.IsResolved,
                CreatedAt     = a.CreatedAt
            }).ToList(),
            GuestsByStatus = statusGroups,
            // Fleet stats
            VehiclesTotal                     = vehicles.Count,
            VehiclesAvailable                 = vehicles.Count(v => v.Status == VehicleStatus.Available),
            VehiclesAssigned                  = vehicles.Count(v => v.Status == VehicleStatus.Assigned),
            VehiclesOutOfService              = vehicles.Count(v => v.Status == VehicleStatus.OutOfService),
            VehiclesNotProvided               = vehicles.Count(v => v.Status == VehicleStatus.NotProvided),
            DriversTotal                      = drivers.Count,
            DriversAvailable                  = drivers.Count(d => d.Status == DriverStatus.Available),
            DriversAssigned                   = drivers.Count(d => d.Status == DriverStatus.Assigned),
            GuestsWithoutVehicle              = guestsWithoutVehicle,
            GuestsAssignedWithoutDedicatedCar = guestsAssignedNoDedicated,
            GuestsDeservingVehicle            = guestsDeserving,
            // Fleet by class
            FleetByClass = carClasses.Select(cc => new FleetByClassDto
            {
                ClassId         = cc.Id,
                ClassName       = cc.Name,
                ClassColor      = cc.Color,
                SortOrder       = cc.SortOrder,
                TotalVehicles   = vehicles.Count(v => v.CarClassId == cc.Id),
                Available       = vehicles.Count(v => v.CarClassId == cc.Id && v.Status == VehicleStatus.Available),
                Assigned        = vehicles.Count(v => v.CarClassId == cc.Id && v.Status == VehicleStatus.Assigned),
                OutOfService    = vehicles.Count(v => v.CarClassId == cc.Id && v.Status == VehicleStatus.OutOfService),
                NotProvided     = vehicles.Count(v => v.CarClassId == cc.Id && v.Status == VehicleStatus.NotProvided),
                GuestsDeserving = guestsByClass.TryGetValue(cc.Id, out var cnt) ? cnt : 0,
            }).ToList(),
            // Recent activity
            RecentActivity = recentActivity,
        };
    }
}
