namespace IsDB.Hospitality.Application.DTOs.DepartureRequests;

public class DepartureRequestDto
{
    public Guid Id { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public string? HotelName { get; set; }
    public string? RoomNumber { get; set; }
    public DateTime RequestedPickupTime { get; set; }
    public string DestinationAirport { get; set; } = string.Empty;
    public string? FlightNumber { get; set; }
    public string? SpecialRequirements { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateDepartureRequestDto
{
    public string GuestName { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public string? HotelName { get; set; }
    public string? RoomNumber { get; set; }
    public DateTime RequestedPickupTime { get; set; }
    public string DestinationAirport { get; set; } = string.Empty;
    public string? FlightNumber { get; set; }
    public string? SpecialRequirements { get; set; }
}
