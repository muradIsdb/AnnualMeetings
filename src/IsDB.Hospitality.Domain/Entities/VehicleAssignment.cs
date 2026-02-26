using IsDB.Hospitality.Domain.Common;

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
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public string? EstimatedArrivalTime { get; set; }
}
