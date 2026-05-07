namespace IsDB.Hospitality.Application.Common.Models;

public class FlightStatusDto
{
    public string FlightNumber { get; set; } = string.Empty;
    public string? Airline { get; set; }
    public string? DepartureAirport { get; set; }
    public string? ArrivalAirport { get; set; }
    public DateTime? ScheduledArrival { get; set; }
    public DateTime? ActualArrival { get; set; }
    public DateTime? ScheduledDeparture { get; set; }
    public DateTime? ActualDeparture { get; set; }
    public string? Status { get; set; }
    public string? Terminal { get; set; }
    public string? Gate { get; set; }
    public int? DelayMinutes { get; set; }
}
