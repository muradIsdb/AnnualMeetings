namespace IsDB.Hospitality.Application.Common.Models;

public class EventsAirTravelDto
{
    public string Id { get; set; } = string.Empty;
    public string ContactId { get; set; } = string.Empty;
    public string? TravelTypeName { get; set; }
    public string? FlightNumber { get; set; }
    public string? CarrierName { get; set; }
    public string? ArrivalDate { get; set; }
    public string? DepartureDate { get; set; }
    public string? Eta { get; set; }
    public string? Etd { get; set; }
    public string? DeparturePortName { get; set; }
    public string? DeparturePortCode { get; set; }
    public string? ArrivalPortName { get; set; }
    public string? ArrivalPortCode { get; set; }
    public string? Terminal { get; set; }
    public string? SeatClass { get; set; }
    public string? BookingNotes { get; set; }
    public string? Comment { get; set; }
}
