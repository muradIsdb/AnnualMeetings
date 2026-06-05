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
    public string? Country { get; set; }
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
    public string? RankValue { get; set; }
    public string? VehicleTypeValue { get; set; }
    public string? DedicatedCar { get; set; }
    public string? RegistrationTypeName { get; set; }
    public Guid? DeservedCarClassId { get; set; }
    public string? DeservedCarClassName { get; set; }
    public string? DeservedCarClassColor { get; set; }
    // Status Flow
    public InboundStatus InboundStatus { get; set; }
    public string InboundStatusLabel { get; set; } = string.Empty;
    public bool ReceivedByEmbassyTeam { get; set; }
    public OutboundStatus? OutboundStatus { get; set; }
    public string? OutboundStatusLabel { get; set; }
    public List<TravelBookingSummaryDto> TravelBookings { get; set; } = new();
    public List<ChecklistCompletionDto> ChecklistCompletions { get; set; } = new();
    public VehicleAssignmentDto? ActiveVehicleAssignment { get; set; }
}

public class TravelBookingSummaryDto
{
    public Guid Id { get; set; }
    public Guid FlightId { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string AirlineName { get; set; } = string.Empty;
    public DateTime ScheduledDeparture { get; set; }
    public DateTime ScheduledArrival { get; set; }
    public string? DeparturePort { get; set; }
    public string? DeparturePortCode { get; set; }
    public string? ArrivalPort { get; set; }
    public string? ArrivalPortCode { get; set; }
    public string? Terminal { get; set; }
    public string FlightStatus { get; set; } = string.Empty;
    public bool IsArrival { get; set; }
    public string? SeatClass { get; set; }
    public string? BookingNotes { get; set; }
    public DateTime? ActualArrival { get; set; }
    public DateTime? ActualDeparture { get; set; }
    // Change-tracking fields for UI highlight
    public bool ChangedSinceLastView { get; set; }
    public string? PreviousFlightNumber { get; set; }
    public DateTime? ChangedAt { get; set; }
    // Layer 2 live fields (Aviationstack-owned)
    public int? LiveDelayMinutes { get; set; }
    public DateTime? LastTrackedAt { get; set; }
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
    public Guid VehicleId { get; set; }
    public string VehicleMake { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string? VehicleColor { get; set; }
    public string? CarNumber { get; set; }
    public string? AssignmentType { get; set; }  // DropOff or Dedicated
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? EstimatedArrivalTime { get; set; }
    public DateTime AssignedAt { get; set; }
}
