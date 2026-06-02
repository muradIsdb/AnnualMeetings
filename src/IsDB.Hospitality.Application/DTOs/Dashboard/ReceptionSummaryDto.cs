namespace IsDB.Hospitality.Application.DTOs.Dashboard;

public class ReceptionSummaryDto
{
    // ── Arrival KPI counts ──────────────────────────────────────────────────
    public int TotalArriving { get; set; }
    public int Scheduled { get; set; }
    public int ArrivedAtAirport { get; set; }
    public int ReceivedByEmbassy { get; set; }
    public int InTransitToHotel { get; set; }
    public int AtHotel { get; set; }
    
    // ── Cumulative Arrival counts ───────────────────────────────────────────
    public int EverArrived { get; set; }
    public int EverReceived { get; set; }
    
    // ── Departure KPI counts ────────────────────────────────────────────────
    public int InTransferToAirport { get; set; }
    public int AtAirport { get; set; }
    public int BoardingCompleted { get; set; }

    // ── Alerts ────────────────────────────────────────────────────────────────
    public List<ReceptionAlertGuestDto> CriticalGuests { get; set; } = new();
    public List<ReceptionAlertGuestDto> AccessibilityGuests { get; set; } = new();

    // ── Flights timeline ─────────────────────────────────────────────────────
    public List<ReceptionFlightDto> Flights { get; set; } = new();

    // ── Guest list ────────────────────────────────────────────────────────────
    public List<ReceptionGuestDto> Guests { get; set; } = new();
}

public class ReceptionAlertGuestDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? Nationality { get; set; }
    public bool IsCritical { get; set; }
    public bool RequiresAccessibility { get; set; }
    public bool HasVehicle { get; set; }
    public string? FlightNumber { get; set; }
    public DateTime? ScheduledArrival { get; set; }
    public string InboundStatusLabel { get; set; } = string.Empty;
}

public class ReceptionFlightDto
{
    public Guid FlightId { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string AirlineName { get; set; } = string.Empty;
    public string? AirlineIataCode { get; set; }
    public DateTime ScheduledArrival { get; set; }
    public string? DeparturePortName { get; set; }
    public string? ArrivalPortName { get; set; }
    public string? ActualTerminal { get; set; }
    public string? ActualGate { get; set; }
    public string FlightStatus { get; set; } = string.Empty;
    public int? LiveDelayMinutes { get; set; }
    public int TotalGuests { get; set; }
    public int Scheduled { get; set; }
    public int ArrivedAtAirport { get; set; }
    public int ReceivedByEmbassy { get; set; }
    public int InTransitToHotel { get; set; }
}

public class ReceptionGuestDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? Nationality { get; set; }
    public string? FlightNumber { get; set; }
    public string? AirlineName { get; set; }
    public DateTime? ScheduledArrival { get; set; }
    public string InboundStatusLabel { get; set; } = string.Empty;
    public int InboundStatusValue { get; set; }
    public string? ActiveVehiclePlate { get; set; }
    public bool IsCritical { get; set; }
    public bool RequiresAccessibility { get; set; }
    public bool FlightCancelled { get; set; }
}
