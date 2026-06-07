namespace IsDB.Hospitality.Application.DTOs.Dashboard;

/// <summary>
/// Arrivals grouped by calendar day — used by the Control Room Dashboard "Arrivals by Day" chart.
/// </summary>
public class ArrivalsByDayDto
{
    public List<DayArrivalDto> Days { get; set; } = new();
}

public class DayArrivalDto
{
    /// <summary>Date label, e.g. "Jun 8"</summary>
    public string DateLabel { get; set; } = string.Empty;
    /// <summary>ISO date string, e.g. "2026-06-08"</summary>
    public string DateIso { get; set; } = string.Empty;
    /// <summary>Number of guests whose inbound flight is scheduled on this day</summary>
    public int Scheduled { get; set; }
    /// <summary>Number of guests who have actually arrived (InboundStatus >= Arrived) on this day</summary>
    public int Arrived { get; set; }
    /// <summary>Number of guests still pending (Scheduled - Arrived)</summary>
    public int Pending { get; set; }
}

/// <summary>
/// User activity summary — used by the Control Room Dashboard "User Activity" section.
/// Derived from GuestStatusHistory and VehicleAssignments in the last 24 hours.
/// </summary>
public class UserActivitySummaryDto
{
    // ── KPI counts (last 24 hours) ──────────────────────────────────────────
    /// <summary>Number of distinct staff users who have performed any action in the last 24 hours</summary>
    public int ActiveUsers { get; set; }
    /// <summary>Total status changes + vehicle assignments in the last 24 hours</summary>
    public int TotalActions { get; set; }
    /// <summary>Guest status changes (InboundStatus or OutboundStatus) in last 24 hours</summary>
    public int GuestUpdates { get; set; }
    /// <summary>Vehicle assignment actions in last 24 hours</summary>
    public int Assignments { get; set; }
    /// <summary>Hotel-related status changes (AtHotel, room updates) in last 24 hours</summary>
    public int HotelUpdates { get; set; }
    /// <summary>Guests marked as Arrived (InboundStatus = Arrived) in last 24 hours</summary>
    public int MarkedAsArrived { get; set; }

    // ── Recent activity feed ─────────────────────────────────────────────────
    public List<ActivityFeedItemDto> RecentFeed { get; set; } = new();

    // ── Hourly breakdown (last 24 hours, 0-23) ───────────────────────────────
    public List<HourlyActivityDto> HourlyBreakdown { get; set; } = new();
}

public class ActivityFeedItemDto
{
    public string Type { get; set; } = string.Empty; // "Vehicle", "Hotel", "Guest", "Arrival"
    public string ActorName { get; set; } = string.Empty;
    public string ActorInitials { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? GuestName { get; set; }
    public DateTime OccurredAt { get; set; }
}

public class HourlyActivityDto
{
    /// <summary>Hour label, e.g. "06:00"</summary>
    public string HourLabel { get; set; } = string.Empty;
    public int Count { get; set; }
}
