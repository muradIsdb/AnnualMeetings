using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

public class Vehicle : BaseEntity
{
    public string? LicensePlate { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    /// <summary>Defaults to NotProvided — set to Available when the vehicle physically arrives on site.</summary>
    public VehicleStatus Status { get; set; } = VehicleStatus.NotProvided;
    public string? BarcodeValue { get; set; }
    /// <summary>Sticker number placed on the vehicle windscreen. Used for QR code generation.</summary>
    public string? CarNumber { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>The driver currently assigned to this vehicle (1-to-1).</summary>
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    /// <summary>Current active guest assignment (if any).</summary>
    public Guid? CurrentGuestId { get; set; }
    public Guest? CurrentGuest { get; set; }

    /// <summary>Assignment type when a guest is assigned: DropOff or Dedicated.</summary>
    public AssignmentType? CurrentAssignmentType { get; set; }

    public ICollection<VehicleAssignment> Assignments { get; set; } = new List<VehicleAssignment>();
    public ICollection<VehicleStatusHistory> StatusHistory { get; set; } = new List<VehicleStatusHistory>();

    /// <summary>The car class this vehicle belongs to (e.g., "Luxury Car").</summary>
    public Guid? CarClassId { get; set; }
    public CarClass? CarClass { get; set; }
}
