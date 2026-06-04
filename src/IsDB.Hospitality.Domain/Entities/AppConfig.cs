namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Singleton configuration row for the platform.
/// Always has exactly one row with Id = 1.
/// </summary>
public class AppConfig
{
    public int Id { get; set; } = 1;

    /// <summary>Event title shown on the public departure form header.</summary>
    public string EventTitle { get; set; } = "IsDB Annual Meetings 2025";

    /// <summary>
    /// Minimum number of hours before a pickup slot can be selected on the departure form.
    /// E.g., 2 means participants cannot select a slot within the next 2 hours.
    /// </summary>
    public int MinimumLeadTimeHours { get; set; } = 2;

    /// <summary>
    /// IANA timezone identifier for the event location (e.g. "Asia/Riyadh").
    /// Used to convert UTC server time to local event time for pickup hour filtering.
    /// </summary>
    public string EventTimezone { get; set; } = "Asia/Riyadh";

    /// <summary>
    /// Placard display theme. "Light" (default white background) or "DarkNavy".
    /// </summary>
    public string PlaCardTheme { get; set; } = "Light";

    /// <summary>
    /// URL of the uploaded event logo shown on the placard.
    /// Kept for backwards compatibility but superseded by EventLogoBase64.
    /// </summary>
    public string? EventLogoUrl { get; set; }

    /// <summary>
    /// Base64-encoded event logo image stored directly in the database.
    /// Persists across Railway redeployments (no filesystem dependency).
    /// When set, the API returns a data URI (data:image/png;base64,...) as EventLogoUrl.
    /// </summary>
    public string? EventLogoBase64 { get; set; }

    /// <summary>MIME type of the base64 logo (e.g. "image/png").</summary>
    public string? EventLogoMimeType { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ─── AviationStack Flight Tracking ───────────────────────────────────────
    /// <summary>AviationStack API key stored in the database (plain text).</summary>
    public string? AviationstackApiKey { get; set; }

    /// <summary>How often (in minutes) the background service polls AviationStack. Default 5.</summary>
    public int AviationstackSyncIntervalMinutes { get; set; } = 5;

    /// <summary>Only poll flights whose ScheduledArrival is within this many hours. Default 12.</summary>
    public int AviationstackTrackingWindowHours { get; set; } = 12;
}
