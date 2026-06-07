using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Records issues detected during EventsAir synchronisation that require
/// manual review by Admin or Transport staff.
/// </summary>
public class SyncAlert : BaseEntity
{
    public SyncAlertType AlertType { get; set; }

    /// <summary>FK to the affected guest (may be null if guest was fully deleted).</summary>
    public Guid? GuestId { get; set; }
    public Guest? Guest { get; set; }

    /// <summary>Snapshot of the guest's full name at detection time.</summary>
    public string GuestName { get; set; } = string.Empty;

    /// <summary>EventsAir contact ID snapshot.</summary>
    public string? EventsAirContactId { get; set; }

    /// <summary>FK to the vehicle that was assigned at detection time (nullable).</summary>
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    /// <summary>Snapshot of the vehicle plate number.</summary>
    public string? VehiclePlate { get; set; }

    /// <summary>Snapshot of the car class name at detection time.</summary>
    public string? CarClassName { get; set; }

    /// <summary>
    /// For CarClassMismatch: old car class name or VehicleTypeValue.
    /// For RegTypeChanged: old registration type name.
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// For CarClassMismatch: new car class name or VehicleTypeValue.
    /// For RegTypeChanged: new registration type name.
    /// </summary>
    public string? NewValue { get; set; }

    public SyncAlertSource SyncSource { get; set; }

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    public bool IsResolved { get; set; } = false;

    public DateTime? ResolvedAt { get; set; }

    /// <summary>Username or display name of the resolver.</summary>
    public string? ResolvedByUserName { get; set; }

    /// <summary>Optional note added by the resolver.</summary>
    public string? Notes { get; set; }
}
