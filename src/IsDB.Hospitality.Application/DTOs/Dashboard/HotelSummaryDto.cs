namespace IsDB.Hospitality.Application.DTOs.Dashboard;

public class HotelSummaryDto
{
    // ── Arrival KPI counts ──────────────────────────────────────────────────
    /// <summary>Guests currently checked in (InboundStatus = AtHotel)</summary>
    public int TotalAtHotel { get; set; }

    /// <summary>
    /// Guests en route to hotel: InboundStatus = VehicleAssigned OR ReceivedByEmbassyTeam flag = true,
    /// but NOT yet AtHotel.
    /// </summary>
    public int EnRouteToHotel { get; set; }

    // ── Departure KPI counts ────────────────────────────────────────────────
    /// <summary>Guests with any OutboundStatus set (departure journey started)</summary>
    public int DepartingActive { get; set; }

    /// <summary>AtHotel guests with null or empty RoomNumber</summary>
    public int NoRoomAssigned { get; set; }

    // ── Per-hotel guest counts ───────────────────────────────────────────────
    public List<HotelGuestCountDto> ByHotel { get; set; } = new();

    // ── Departure status breakdown ───────────────────────────────────────────
    public int OutboundAtHotel { get; set; }
    public int InTransferToAirport { get; set; }
    public int AtAirport { get; set; }
    public int BoardingCompleted { get; set; }

    // ── Recent check-ins ─────────────────────────────────────────────────────
    public List<HotelRecentCheckinDto> RecentCheckins { get; set; } = new();

    // ── Guests without room number ───────────────────────────────────────────
    public List<HotelNoRoomGuestDto> GuestsWithoutRoom { get; set; } = new();
}

public class HotelGuestCountDto
{
    public string HotelName { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    /// <summary>Guests at this hotel who have a room number assigned</summary>
    public int WithRoomCount { get; set; }
    /// <summary>Guests at this hotel who have no room number yet</summary>
    public int NoRoomCount { get; set; }
}

public class HotelRecentCheckinDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string? HotelName { get; set; }
    public string? RoomNumber { get; set; }
    public DateTime? CheckedInAt { get; set; }
}

public class HotelNoRoomGuestDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string? HotelName { get; set; }
    public DateTime? CheckedInAt { get; set; }
}
