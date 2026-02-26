using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Application.DTOs.Guests;

public class GuestDto
{
    public Guid Id { get; set; }
    public string EventsAirContactId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Organization { get; set; }
    public string? Designation { get; set; }
    public string? Nationality { get; set; }
    public string? PhotoUrl { get; set; }
    public string? MobileNumber { get; set; }
    public string? Email { get; set; }
    public bool IsCritical { get; set; }
    public bool RequiresAccessibility { get; set; }
    public GuestStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string? GroupCode { get; set; }
    public string? RoomNumber { get; set; }
    public string? HotelName { get; set; }
    public string? SpecialRequirements { get; set; }
    public string? Notes { get; set; }
    public List<FlightSummaryDto> Flights { get; set; } = new();
    public List<ChecklistCompletionDto> ChecklistCompletions { get; set; } = new();
    public VehicleAssignmentDto? ActiveVehicleAssignment { get; set; }
}

public class FlightSummaryDto
{
    public Guid Id { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string? Airline { get; set; }
    public DateTime? ScheduledArrival { get; set; }
    public DateTime? ActualArrival { get; set; }
    public DateTime? ScheduledDeparture { get; set; }
    public string? Status { get; set; }
    public bool IsArrival { get; set; }
    public int? DelayMinutes { get; set; }
}

public class ChecklistCompletionDto
{
    public Guid ChecklistItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByName { get; set; }
}

public class VehicleAssignmentDto
{
    public Guid Id { get; set; }
    public string VehicleMake { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? EstimatedArrivalTime { get; set; }
    public DateTime AssignedAt { get; set; }
}
