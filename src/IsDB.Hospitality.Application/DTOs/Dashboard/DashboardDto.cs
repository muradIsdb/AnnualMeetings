using IsDB.Hospitality.Application.DTOs.Alerts;
using IsDB.Hospitality.Application.DTOs.Guests;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Application.DTOs.Dashboard;

public class DashboardSummaryDto
{
    public int TotalGuests { get; set; }
    public int ArrivingCount { get; set; }
    /// <summary>Guests with InboundStatus==Arrived AND ReceivedByEmbassyTeam==false (currently at airport, not yet handed over).</summary>
    public int AtAirportCount { get; set; }
    public int ReceivedByEmbassyCount { get; set; }
    public int OnTheWayToHotelCount { get; set; }
    public int AtHotelCount { get; set; }
    public int DepartingCount { get; set; }
    public int ActiveAlertsCount { get; set; }
    public int CriticalAlertsCount { get; set; }
    public List<AlertDto> ActiveAlerts { get; set; } = new();
    public List<GuestStatusGroupDto> GuestsByStatus { get; set; } = new();

    // Fleet stats
    public int VehiclesTotal { get; set; }
    public int VehiclesAvailable { get; set; }
    public int VehiclesAssigned { get; set; }
    public int VehiclesOutOfService { get; set; }
    public int VehiclesNotProvided { get; set; }
    public int DriversTotal { get; set; }
    public int DriversAvailable { get; set; }
    public int DriversAssigned { get; set; }
    public int GuestsWithoutVehicle { get; set; }
    /// <summary>Active guests who have an active vehicle assignment but DedicatedCar != "True" (anomaly indicator).</summary>
    public int GuestsAssignedWithoutDedicatedCar { get; set; }
    /// <summary>Active guests who have a DeservedCarClassId set (i.e., deserve a vehicle).</summary>
    public int GuestsDeservingVehicle { get; set; }

    // Fleet by class
    public List<FleetByClassDto> FleetByClass { get; set; } = new();

    // Recent activity
    public List<ActivityItemDto> RecentActivity { get; set; } = new();
}

public class FleetByClassDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string? ClassColor { get; set; }
    public int SortOrder { get; set; }
    public int TotalVehicles { get; set; }
    public int Available { get; set; }
    public int Assigned { get; set; }
    public int OutOfService { get; set; }
    public int NotProvided { get; set; }
    public int GuestsDeserving { get; set; }
}

public class ActivityItemDto
{
    public string Type { get; set; } = string.Empty;  // "VehicleAssigned", "VehicleUnassigned", "StatusChanged"
    public string GuestName { get; set; } = string.Empty;
    public string? VehiclePlate { get; set; }
    public string? DriverName { get; set; }
    public string? Detail { get; set; }
    public DateTime OccurredAt { get; set; }
}

public class GuestStatusGroupDto
{
    public string StatusLabel { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<GuestSummaryDto> Guests { get; set; } = new();
}

public class GuestSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? Nationality { get; set; }
    public string? Country { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsCritical { get; set; }
    public bool RequiresAccessibility { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string? ActiveVehiclePlate { get; set; }
    public string? Notes { get; set; }
    public string? RegistrationTypeName { get; set; }
    public string? RankValue { get; set; }
    public string? VehicleTypeValue { get; set; }
    public string? DedicatedCar { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public Guid? DeservedCarClassId { get; set; }
    public string? DeservedCarClassName { get; set; }
    public string? DeservedCarClassColor { get; set; }
    public InboundStatus InboundStatus { get; set; }
    public string InboundStatusLabel { get; set; } = string.Empty;
    /// <summary>True if any travel booking has ChangedSinceLastView=true (flight was re-booked in EventsAir).</summary>
    public bool HasChangedFlights { get; set; }
    public OutboundStatus? OutboundStatus { get; set; }
    public string? OutboundStatusLabel { get; set; }
    public string? HotelName { get; set; }
    public string? RoomNumber { get; set; }
    public string? Organization { get; set; }
}

public class RegistrationTypeStatsDto
{
    public string RegistrationTypeName { get; set; } = string.Empty;
    public int Total { get; set; }
    public int ArrivalScheduled { get; set; }
    public int Arrived { get; set; }
    public int InTransitToHotel { get; set; }
    public int AtHotel { get; set; }
    public int Departed { get; set; }
}
