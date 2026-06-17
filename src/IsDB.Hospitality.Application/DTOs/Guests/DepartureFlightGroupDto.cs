using IsDB.Hospitality.Application.DTOs.Dashboard;
namespace IsDB.Hospitality.Application.DTOs.Guests;
public class DepartureFlightGroupDto
{
    public Guid? FlightId { get; set; }
    public string? FlightNumber { get; set; }
    public string? AirlineName { get; set; }
    public string? AirlineIataCode { get; set; }
    public DateTime? ScheduledDeparture { get; set; }
    public DateTime? ActualDeparture { get; set; }
    public string? Terminal { get; set; }
    public string? Gate { get; set; }
    public string FlightStatus { get; set; } = "Unknown";
    public bool IsExpired { get; set; }
    public List<GuestSummaryDto> Guests { get; set; } = new();
}
