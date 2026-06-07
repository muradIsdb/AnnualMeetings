using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Application.Features.Dashboard.Queries;
using IsDB.Hospitality.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[Authorize]
public class DashboardController : ApiControllerBase
{
    private readonly IAppDbContext _db;
    public DashboardController(IAppDbContext db) { _db = db; }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken ct)
    {
        var activeEventCode = (await _db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;
        var result = await Mediator.Send(new GetDashboardSummaryQuery(activeEventCode));
        return Ok(result);
    }

    [HttpGet("registration-type-stats")]
    public async Task<ActionResult<List<RegistrationTypeStatsDto>>> GetRegistrationTypeStats(CancellationToken ct)
    {
        var activeEventCode = (await _db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;
        var result = await Mediator.Send(new GetRegistrationTypeStatsQuery(activeEventCode));
        return Ok(result);
    }

    /// <summary>
    /// Returns hotel-focused summary data: occupancy, en-route guests, departure status, recent check-ins.
    /// Used by the Hotel Dashboard (Admin and Hotel roles).
    /// </summary>
    [HttpGet("hotel-summary")]
    public async Task<ActionResult<HotelSummaryDto>> GetHotelSummary(CancellationToken ct)
    {
        var activeEventCode = (await _db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;
        var result = await Mediator.Send(new GetHotelSummaryQuery(activeEventCode));
        return Ok(result);
    }

    /// <summary>
    /// Returns reception-focused summary data filtered by inbound flight ScheduledArrival date range.
    /// Used by the Reception Dashboard (Admin and Airport roles).
    /// </summary>
    [HttpGet("reception-summary")]
    public async Task<ActionResult<ReceptionSummaryDto>> GetReceptionSummary(
        [FromQuery] string? from = null,
        [FromQuery] string? to = null)
    {
        var fromDate = !string.IsNullOrWhiteSpace(from) && DateTime.TryParse(from, out var f)
            ? f : DateTime.UtcNow.Date;
        var toDate = !string.IsNullOrWhiteSpace(to) && DateTime.TryParse(to, out var t)
            ? t : fromDate;
        var result = await Mediator.Send(new GetReceptionSummaryQuery(fromDate, toDate));
        return Ok(result);
    }

    /// <summary>
    /// Returns arrivals grouped by calendar day — for the Control Room Dashboard "Arrivals by Day" chart.
    /// Looks at all inbound TravelBookings grouped by the flight's ScheduledArrival date.
    /// </summary>
    [HttpGet("arrivals-by-day")]
    public async Task<ActionResult<ArrivalsByDayDto>> GetArrivalsByDay(CancellationToken ct)
    {
        var activeEventCode = (await _db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;

        // Load all active inbound travel bookings with their flight scheduled arrival
        var bookings = await _db.TravelBookings
            .AsNoTracking()
            .Include(tb => tb.Flight)
            .Include(tb => tb.Guest)
            .Where(tb =>
                tb.IsArrival &&
                tb.Guest.IsActive &&
                (activeEventCode == null || tb.Guest.EventCode == null || tb.Guest.EventCode == activeEventCode))
            .Select(tb => new
            {
                GuestId       = tb.GuestId,
                ScheduledDate = tb.Flight.ScheduledArrival.Date,
                InboundStatus = tb.Guest.InboundStatus
            })
            .ToListAsync(ct);

        // Deduplicate: one entry per guest (earliest flight date)
        var perGuest = bookings
            .GroupBy(b => b.GuestId)
            .Select(g => g.OrderBy(b => b.ScheduledDate).First())
            .ToList();

        // Group by date
        var grouped = perGuest
            .GroupBy(b => b.ScheduledDate)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var scheduled = g.Count();
                var arrived   = g.Count(b => b.InboundStatus >= InboundStatus.Arrived);
                return new DayArrivalDto
                {
                    DateLabel = g.Key.ToString("MMM d"),
                    DateIso   = g.Key.ToString("yyyy-MM-dd"),
                    Scheduled = scheduled,
                    Arrived   = arrived,
                    Pending   = scheduled - arrived
                };
            })
            .ToList();

        return Ok(new ArrivalsByDayDto { Days = grouped });
    }

    /// <summary>
    /// Returns user activity summary for the Control Room Dashboard.
    /// Aggregates GuestStatusHistory and VehicleAssignments from the last 24 hours.
    /// </summary>
    [HttpGet("user-activity")]
    public async Task<ActionResult<UserActivitySummaryDto>> GetUserActivity(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddHours(-24);

        // ── 1. Status history entries in last 24 hours ────────────────────────
        var statusHistory = await _db.GuestStatusHistories
            .AsNoTracking()
            .Include(h => h.ChangedByStaff)
            .Include(h => h.Guest)
            .Where(h => h.CreatedAt >= since && !h.IsSystemGenerated && !h.IsRolledBack)
            .Select(h => new
            {
                h.Id,
                h.CreatedAt,
                h.Track,
                h.StatusValue,
                h.ChangedByStaffId,
                ChangedByName = h.ChangedByName ?? (h.ChangedByStaff != null ? h.ChangedByStaff.FullName : null),
                GuestName     = h.Guest != null ? h.Guest.FirstName + " " + h.Guest.LastName : null,
                h.IsSystemGenerated
            })
            .ToListAsync(ct);

        // ── 2. Vehicle assignments in last 24 hours ───────────────────────────
        var assignments = await _db.VehicleAssignments
            .AsNoTracking()
            .Include(va => va.AssignedByStaff)
            .Include(va => va.Guest)
            .Include(va => va.Vehicle)
            .Where(va => va.AssignedAt >= since)
            .Select(va => new
            {
                va.Id,
                va.AssignedAt,
                va.AssignedByStaffId,
                AssignedByName = va.AssignedByStaff != null ? va.AssignedByStaff.FullName : null,
                GuestName      = va.Guest != null ? va.Guest.FirstName + " " + va.Guest.LastName : null,
                VehiclePlate   = va.Vehicle != null ? va.Vehicle.LicensePlate : null
            })
            .ToListAsync(ct);

        // ── 3. KPI counts ─────────────────────────────────────────────────────
        var activeUserIds = new HashSet<Guid?>();
        foreach (var h in statusHistory) activeUserIds.Add(h.ChangedByStaffId);
        foreach (var a in assignments)   activeUserIds.Add(a.AssignedByStaffId);
        activeUserIds.Remove(null);

        int activeUsers    = activeUserIds.Count;
        int guestUpdates   = statusHistory.Count;
        int assignmentCount = assignments.Count;
        int totalActions   = guestUpdates + assignmentCount;

        // Hotel updates: InboundStatus = AtHotel (4) changes
        int hotelUpdates   = statusHistory.Count(h =>
            h.Track == StatusTrack.Inbound && h.StatusValue == (int)InboundStatus.AtHotel);

        // Marked as arrived: InboundStatus = Arrived (1) changes
        int markedAsArrived = statusHistory.Count(h =>
            h.Track == StatusTrack.Inbound && h.StatusValue == (int)InboundStatus.Arrived);

        // ── 4. Recent activity feed (last 20 actions combined) ────────────────
        static string GetInitials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "??";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpper();
            return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
        }

        var feedItems = new List<ActivityFeedItemDto>();

        // Add status history items to feed
        foreach (var h in statusHistory.OrderByDescending(x => x.CreatedAt).Take(20))
        {
            string type, action;
            if (h.Track == StatusTrack.Inbound && h.StatusValue == (int)InboundStatus.AtHotel)
            {
                type   = "Hotel";
                action = $"Checked in at hotel";
            }
            else if (h.Track == StatusTrack.Inbound && h.StatusValue == (int)InboundStatus.Arrived)
            {
                type   = "Arrival";
                action = $"Marked as arrived at airport";
            }
            else if (h.Track == StatusTrack.Inbound)
            {
                type   = "Guest";
                action = $"Updated inbound status";
            }
            else
            {
                type   = "Guest";
                action = $"Updated outbound status";
            }

            feedItems.Add(new ActivityFeedItemDto
            {
                Type         = type,
                ActorName    = h.ChangedByName ?? "System",
                ActorInitials = GetInitials(h.ChangedByName),
                Action       = action,
                GuestName    = h.GuestName,
                OccurredAt   = h.CreatedAt
            });
        }

        // Add vehicle assignment items to feed
        foreach (var a in assignments.OrderByDescending(x => x.AssignedAt).Take(20))
        {
            feedItems.Add(new ActivityFeedItemDto
            {
                Type          = "Vehicle",
                ActorName     = a.AssignedByName ?? "System",
                ActorInitials = GetInitials(a.AssignedByName),
                Action        = $"Assigned vehicle {a.VehiclePlate ?? ""}",
                GuestName     = a.GuestName,
                OccurredAt    = a.AssignedAt
            });
        }

        // Sort combined feed and take top 15
        var recentFeed = feedItems
            .OrderByDescending(x => x.OccurredAt)
            .Take(15)
            .ToList();

        // ── 5. Hourly breakdown ───────────────────────────────────────────────
        // Build 24-hour buckets from since to now
        var nowUtc = DateTime.UtcNow;
        var hourlyBreakdown = new List<HourlyActivityDto>();

        for (int i = 23; i >= 0; i--)
        {
            var bucketStart = nowUtc.AddHours(-i - 1);
            var bucketEnd   = nowUtc.AddHours(-i);
            var label       = bucketStart.ToString("HH:00");

            int count = statusHistory.Count(h => h.CreatedAt >= bucketStart && h.CreatedAt < bucketEnd)
                      + assignments.Count(a => a.AssignedAt >= bucketStart && a.AssignedAt < bucketEnd);

            hourlyBreakdown.Add(new HourlyActivityDto { HourLabel = label, Count = count });
        }

        return Ok(new UserActivitySummaryDto
        {
            ActiveUsers     = activeUsers,
            TotalActions    = totalActions,
            GuestUpdates    = guestUpdates,
            Assignments     = assignmentCount,
            HotelUpdates    = hotelUpdates,
            MarkedAsArrived = markedAsArrived,
            RecentFeed      = recentFeed,
            HourlyBreakdown = hourlyBreakdown
        });
    }
}
