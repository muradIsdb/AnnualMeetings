using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Immutable log entry recording a single status change for a guest's journey.
/// Used for the status history timeline and rollback validation.
/// </summary>
public class GuestStatusHistory : BaseEntity
{
    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!;

    /// <summary>Which track this entry belongs to (Inbound or Outbound).</summary>
    public StatusTrack Track { get; set; }

    /// <summary>
    /// The status value as an integer.
    /// For Inbound: cast to InboundStatus. For Outbound: cast to OutboundStatus.
    /// Using int to avoid polymorphic enum column complexity.
    /// </summary>
    public int StatusValue { get; set; }

    /// <summary>Human-readable status label for display (snapshot at time of change).</summary>
    public string StatusLabel { get; set; } = string.Empty;

    /// <summary>The staff user who made this change. Null if system-triggered.</summary>
    public Guid? ChangedByStaffId { get; set; }
    public StaffUser? ChangedByStaff { get; set; }

    /// <summary>Display name of the person who made the change (snapshot).</summary>
    public string? ChangedByName { get; set; }

    /// <summary>Role of the user at the time of change (snapshot).</summary>
    public UserRole? ChangedByRole { get; set; }

    /// <summary>Whether this change was made by the system automatically (e.g. VehicleAssigned).</summary>
    public bool IsSystemGenerated { get; set; } = false;

    /// <summary>Optional notes attached to this status change.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Whether this entry has been rolled back (undone).
    /// Rolled-back entries are kept for audit purposes but marked as inactive.
    /// </summary>
    public bool IsRolledBack { get; set; } = false;

    /// <summary>The ID of the history entry that rolled this one back (if applicable).</summary>
    public Guid? RolledBackByHistoryId { get; set; }
}
