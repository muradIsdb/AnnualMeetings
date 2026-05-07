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
    /// Null means use the default IsDB logo.
    /// </summary>
    public string? EventLogoUrl { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
