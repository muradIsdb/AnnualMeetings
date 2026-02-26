using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Application.DTOs.Vehicles;

public class VehicleDto
{
    public Guid Id { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public VehicleStatus Status { get; set; }
    public string? BarcodeValue { get; set; }
}

public class AssignVehicleDto
{
    public Guid GuestId { get; set; }
    public Guid VehicleId { get; set; }
    public string? Notes { get; set; }
    public string? EstimatedArrivalTime { get; set; }
}

public class AssignVehicleByBarcodeDto
{
    public Guid GuestId { get; set; }
    public string BarcodeValue { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
