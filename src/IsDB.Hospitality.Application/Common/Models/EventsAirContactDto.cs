namespace IsDB.Hospitality.Application.Common.Models;

public class EventsAirContactDto
{
    public string ContactId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Organization { get; set; }
    public string? Designation { get; set; }
    public string? Nationality { get; set; }
    public string? PassportNumber { get; set; }
    public string? PhotoUrl { get; set; }
    public string? MobileNumber { get; set; }
    public string? Email { get; set; }
    public bool IsCritical { get; set; }
    public bool RequiresAccessibility { get; set; }
    public string? GroupCode { get; set; }
    public string? HotelName { get; set; }
    public string? RoomNumber { get; set; }
    public string? SpecialRequirements { get; set; }
    public string? ArrivalFlightNumber { get; set; }
    public DateTime? ArrivalFlightDate { get; set; }
    public string? DepartureFlightNumber { get; set; }
    public DateTime? DepartureFlightDate { get; set; }
}
