using IsDB.Hospitality.Domain.Common;

namespace IsDB.Hospitality.Domain.Entities;

public class FlightSyncLog : BaseEntity
{
    /// <summary>UTC timestamp when the sync run completed.</summary>
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    /// <summary>"Manual" or "Scheduled"</summary>
    public string TriggerSource { get; set; } = "Scheduled";

    /// <summary>"Success", "Failed", or "NoFlightsInWindow"</summary>
    public string Status { get; set; } = "Success";

    /// <summary>Flights that passed the tracking-window filter (fetched from DB).</summary>
    public int FlightsInWindow { get; set; }

    /// <summary>Flights that passed the date guard and were actually queried against AviationStack.</summary>
    public int FlightsQueried { get; set; }

    /// <summary>Flights where at least one field changed and was persisted.</summary>
    public int FlightsUpdated { get; set; }

    /// <summary>Elapsed time for the sync run in milliseconds.</summary>
    public int DurationMs { get; set; }

    /// <summary>Human-readable result message shown in the UI toast.</summary>
    public string? Message { get; set; }

    /// <summary>Display name of the staff member who triggered a manual sync. Null for scheduled syncs.</summary>
    public string? InitiatedByStaffName { get; set; }
}
