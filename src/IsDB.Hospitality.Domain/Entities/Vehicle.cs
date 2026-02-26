using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

public class Vehicle : BaseEntity
{
    public string LicensePlate { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;
    public string? BarcodeValue { get; set; }

    public ICollection<VehicleAssignment> Assignments { get; set; } = new List<VehicleAssignment>();
}
