namespace IsDB.Hospitality.Application.DTOs.EventsAir;

public class EventsAirConfigDto
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty; // masked on read
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string EventCode { get; set; } = string.Empty;
    public string TenantCode { get; set; } = string.Empty;
    public int SyncIntervalMinutes { get; set; }
    public bool AutoSyncEnabled { get; set; }
    public bool SyncOnStartup { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string LastSyncStatus { get; set; } = string.Empty;
    public string? LastSyncMessage { get; set; }
    public int LastSyncRecordsCount { get; set; }
    public bool IsActive { get; set; }
}

public class UpdateEventsAirConfigRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; } // null = keep existing
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string EventCode { get; set; } = string.Empty;
    public string TenantCode { get; set; } = string.Empty;
    public int SyncIntervalMinutes { get; set; }
    public bool AutoSyncEnabled { get; set; }
    public bool SyncOnStartup { get; set; }
    public bool IsActive { get; set; }
}

public class TestConnectionRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; } // null or empty = use saved secret from DB
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string EventCode { get; set; } = string.Empty;
    public string TenantCode { get; set; } = string.Empty;
}

public class TestConnectionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? ResponseTimeMs { get; set; }
    public string? TokenPreview { get; set; }
}

public class EventsAirSyncLogDto
{
    public Guid Id { get; set; }
    public DateTime SyncedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public int RecordsSynced { get; set; }
    public int DurationMs { get; set; }
    public string SyncType { get; set; } = string.Empty;
}

public class TriggerSyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int RecordsSynced { get; set; }
    public int DurationMs { get; set; }
}
