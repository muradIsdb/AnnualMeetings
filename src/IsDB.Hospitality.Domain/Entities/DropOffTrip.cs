using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Records a drop-off vehicle trip. Created when a vehicle is assigned with AssignmentType = DropOff.
/// The vehicle is NOT permanently linked to the guest — it returns to the pool after the trip.
/// </summary>
public class DropOffTrip : BaseEntity
{
    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!;

    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    /// <summary>Driver FK at time of trip (nullable — vehicle may have no driver linked).</summary>
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    /// <summary>Snapshot of driver name at time of trip.</summary>
    public string? DriverName { get; set; }

    /// <summary>Snapshot of driver phone at time of trip.</summary>
    public string? DriverPhone { get; set; }

    /// <summary>Snapshot of vehicle car number at time of trip.</summary>
    public string? CarNumber { get; set; }

    /// <summary>Trip destination — required.</summary>
    public string Destination { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public Guid LoggedByStaffId { get; set; }
    public StaffUser LoggedByStaff { get; set; } = null!;

    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    public DropOffTripStatus Status { get; set; } = DropOffTripStatus.InProgress;

    public DateTime? CompletedAt { get; set; }
}
