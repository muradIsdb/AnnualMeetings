using IsDB.Hospitality.Application.DTOs.Dashboard;

namespace IsDB.Hospitality.Application.DTOs.Guests;

public class ArrivalFlightGroupDto
{
    public Guid? FlightId { get; set; }
    public string? FlightNumber { get; set; }
    public string? AirlineName { get; set; }
    public string? AirlineIataCode { get; set; }
    public DateTime? ScheduledArrival { get; set; }
    public DateTime? ActualArrival { get; set; }
    public string? Terminal { get; set; }
    public string? Gate { get; set; }
    public string FlightStatus { get; set; } = "Unknown";
    // Layer 2 live fields (Aviationstack-owned)
    public int? LiveDelayMinutes { get; set; }
    public DateTime? LastTrackedAt { get; set; }
    public List<GuestSummaryDto> Guests { get; set; } = new();
}
