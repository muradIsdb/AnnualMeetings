using IsDB.Hospitality.Domain.Common;

namespace IsDB.Hospitality.Domain.Entities;

public class EventsAirConfig : BaseEntity
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.eventsair.com";
    public string TokenEndpoint { get; set; } = "https://auth.eventsair.com/oauth/token";
    public string EventCode { get; set; } = string.Empty;
    public string TenantCode { get; set; } = string.Empty;
    public int SyncIntervalMinutes { get; set; } = 15;
    public bool AutoSyncEnabled { get; set; } = true;
    public bool SyncOnStartup { get; set; } = true;
    public DateTime? LastSyncAt { get; set; }
    public string LastSyncStatus { get; set; } = "Never";
    public string? LastSyncMessage { get; set; }
    public int LastSyncRecordsCount { get; set; } = 0;
    public int LastSyncDeactivatedCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    // OAuthScope is stored in the DB but NOT mapped via EF Core to avoid breaking existing deployments.
    // It is read/written using raw SQL in EventsAirController. Column added by AddOAuthScopeToEventsAirConfig migration.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string OAuthScope { get; set; } = "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";
}

public class EventsAirSyncLog : BaseEntity
{
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = string.Empty; // Success, Failed, Partial
    public string? Message { get; set; }
    // Legacy combined count (kept for backward compat)
    public int RecordsSynced { get; set; }
    public int DurationMs { get; set; }
    public string SyncType { get; set; } = "Scheduled"; // Scheduled, Manual
    // Enriched fields
    public string TriggerSource { get; set; } = "System Auto-Sync"; // System Auto-Sync, Admin UI Button, API Trigger, Startup
    public Guid? InitiatedByStaffId { get; set; }
    public string? InitiatedByStaffName { get; set; }
    public int RecordsAdded { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsDeactivated { get; set; }
    public int TravelBookingsSynced { get; set; }
}
