using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

public class VehicleAssignment : BaseEntity
{
    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!;

    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public Guid AssignedByStaffId { get; set; }
    public StaffUser AssignedByStaff { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnassignedAt { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public string? EstimatedArrivalTime { get; set; }

    /// <summary>Drop-off (temporary) or Dedicated (exclusive to guest).</summary>
    public AssignmentType AssignmentType { get; set; } = AssignmentType.DropOff;

    /// <summary>Snapshot of driver at time of assignment.</summary>
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    /// <summary>Staff who removed the assignment (for history log).</summary>
    public Guid? UnassignedByStaffId { get; set; }
    public StaffUser? UnassignedByStaff { get; set; }
}
