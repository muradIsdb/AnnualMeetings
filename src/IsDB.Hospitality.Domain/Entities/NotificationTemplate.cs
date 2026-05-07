using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// A configurable template for system-generated notifications.
/// Admin can edit the message template and priority through the UI.
/// </summary>
public class NotificationTemplate : BaseEntity
{
    /// <summary>Unique machine key identifying this event, e.g. "inbound.vehicle_assigned".</summary>
    public string EventKey { get; set; } = string.Empty;

    /// <summary>Human-readable label shown in the admin UI, e.g. "Vehicle Assigned (First Time)".</summary>
    public string EventLabel { get; set; } = string.Empty;

    /// <summary>
    /// Message template with optional placeholders: {GuestName}, {VehiclePlate}, {VehicleMake}, {VehicleModel}.
    /// Example: "[Inbound] {GuestName}'s vehicle was assigned (dispatched from Airport)."
    /// </summary>
    public string MessageTemplate { get; set; } = string.Empty;

    /// <summary>Comma-separated target roles, e.g. "Hotel,Admin". Read-only — not editable by admin.</summary>
    public string TargetRoles { get; set; } = "All";

    /// <summary>Notification priority. Admin can change this.</summary>
    public AlertSeverity Priority { get; set; } = AlertSeverity.Medium;

    /// <summary>Human-readable description of when this notification fires.</summary>
    public string Description { get; set; } = string.Empty;
}
