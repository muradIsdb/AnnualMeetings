using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

public class Flight : BaseEntity
{
    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!;

    public string FlightNumber { get; set; } = string.Empty;
    public string? Airline { get; set; }
    public string? DepartureAirportCode { get; set; }
    public string? ArrivalAirportCode { get; set; }
    public DateTime? ScheduledArrival { get; set; }
    public DateTime? ActualArrival { get; set; }
    public DateTime? ScheduledDeparture { get; set; }
    public DateTime? ActualDeparture { get; set; }
    public FlightStatus Status { get; set; } = FlightStatus.Scheduled;
    public bool IsArrival { get; set; } = true;
    public string? Terminal { get; set; }
    public string? Gate { get; set; }
    public string? DelayReason { get; set; }
    public DateTime? LastTrackedAt { get; set; }
}
