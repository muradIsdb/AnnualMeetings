using IsDB.Hospitality.Domain.Common;

namespace IsDB.Hospitality.Domain.Entities;

public class DepartureRequest : BaseEntity
{
    public Guid? GuestId { get; set; }
    public Guest? Guest { get; set; }

    // Fields for unregistered guests
    public string GuestName { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public string? HotelName { get; set; }
    public string? RoomNumber { get; set; }

    public DateTime RequestedPickupTime { get; set; }
    public string DestinationAirport { get; set; } = string.Empty;
    public string? FlightNumber { get; set; }
    public string? SpecialRequirements { get; set; }
    public bool IsProcessed { get; set; } = false;
    public DateTime? ProcessedAt { get; set; }
    public Guid? ProcessedByStaffId { get; set; }
    public StaffUser? ProcessedByStaff { get; set; }
}
