using IsDB.Hospitality.Application.DTOs.Alerts;
using IsDB.Hospitality.Application.DTOs.Guests;

namespace IsDB.Hospitality.Application.DTOs.Dashboard;

public class DashboardSummaryDto
{
    public int TotalGuests { get; set; }
    public int ArrivingCount { get; set; }
    public int ReceivedByEmbassyCount { get; set; }
    public int OnTheWayToHotelCount { get; set; }
    public int AtHotelCount { get; set; }
    public int DepartingCount { get; set; }
    public int ActiveAlertsCount { get; set; }
    public int CriticalAlertsCount { get; set; }
    public List<AlertDto> ActiveAlerts { get; set; } = new();
    public List<GuestStatusGroupDto> GuestsByStatus { get; set; } = new();
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
    public string? PhotoUrl { get; set; }
    public bool IsCritical { get; set; }
    public bool RequiresAccessibility { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string? ActiveVehiclePlate { get; set; }
    public string? Notes { get; set; }
    public string? RegistrationTypeName { get; set; }
}
