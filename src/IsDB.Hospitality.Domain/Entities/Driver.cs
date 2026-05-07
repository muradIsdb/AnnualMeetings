using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

public class Driver : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DriverStatus Status { get; set; } = DriverStatus.Available;

    /// <summary>The vehicle currently assigned to this driver (1-to-1).</summary>
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public bool IsActive { get; set; } = true;
}
