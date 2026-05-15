using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Immutable log entry recording a single status change for a vehicle.
/// Tracks mobilisation history: when a vehicle moved from NotProvided → Available,
/// went OutOfService, was returned to service, etc.
/// </summary>
public class VehicleStatusHistory : BaseEntity
{
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    /// <summary>Status before this change.</summary>
    public VehicleStatus OldStatus { get; set; }

    /// <summary>Status after this change.</summary>
    public VehicleStatus NewStatus { get; set; }

    /// <summary>The staff user who made this change. Null if system-triggered.</summary>
    public Guid? ChangedByStaffId { get; set; }
    public StaffUser? ChangedByStaff { get; set; }

    /// <summary>Display name of the person who made the change (snapshot at time of change).</summary>
    public string? ChangedByName { get; set; }

    /// <summary>Role of the user at the time of change (snapshot).</summary>
    public UserRole? ChangedByRole { get; set; }

    /// <summary>Optional notes attached to this status change (e.g. "Day 1 delivery batch").</summary>
    public string? Notes { get; set; }
}
