using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;
using System;
using System.Collections.Generic;

namespace IsDB.Hospitality.Domain.Entities;

public class Flight : BaseEntity
{
    public string FlightNumber { get; set; } = string.Empty;
    public string AirlineName { get; set; } = string.Empty;
    public string? AirlineIataCode { get; set; }

    // Scheduled times from EventsAir
    public DateTime ScheduledDeparture { get; set; }
    public DateTime ScheduledArrival { get; set; }

    public string? DeparturePortName { get; set; }
    public string? DeparturePortIataCode { get; set; }
    public string? ArrivalPortName { get; set; }
    public string? ArrivalPortIataCode { get; set; }

    // Current status (from EventsAir or AviationStack)
    public FlightStatus Status { get; set; } = FlightStatus.Scheduled;

    // Actual times (from AviationStack)
    public DateTime? ActualDeparture { get; set; }
    public DateTime? ActualArrival { get; set; }
    public string? ActualTerminal { get; set; }
    public string? ActualGate { get; set; }

    public DateTime? LastTrackedAt { get; set; } // When AviationStack data was last updated
    public int? LiveDelayMinutes { get; set; }   // Delay in minutes from Aviationstack (Layer 2 only)

    // Navigation property for related travel bookings
    public ICollection<TravelBooking> TravelBookings { get; set; } = new List<TravelBooking>();
}
