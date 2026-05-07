using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;
using System;

namespace IsDB.Hospitality.Domain.Entities;

public class TravelBooking : BaseEntity
{
    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!;

    public Guid FlightId { get; set; } // Foreign key to the shared Flight entity
    public Flight Flight { get; set; } = null!;

    public bool IsArrival { get; set; } = true; // True for arrival, false for departure

    // Guest-specific details from EventsAir
    public string? SeatClass { get; set; }
    public string? BookingReference { get; set; }
    public string? AirlineReference { get; set; }
    public string? Comment { get; set; }
    public string? BookingNotes { get; set; }
    public string? Itinerary { get; set; }
    public int? Tickets { get; set; }
    public TravelBookingStatus Status { get; set; } = TravelBookingStatus.NewBooking; // EventsAir booking status

    // Terminal/Gate specific to this guest's booking if different from Flight
    public string? Terminal { get; set; }
    public string? Gate { get; set; }

    // Delay reason specific to this guest's booking if different from Flight
    public string? DelayReason { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    // ─── Change-tracking for UI highlight ────────────────────────────────────

    /// <summary>
    /// Set to true by the EventsAir sync whenever the flight is replaced by a different one.
    /// Cleared when the user acknowledges the change (views the guest detail page).
    /// </summary>
    public bool ChangedSinceLastView { get; set; } = false;

    /// <summary>The flight number that was replaced, for display in the UI tooltip.</summary>
    public string? PreviousFlightNumber { get; set; }

    /// <summary>UTC time the last flight change was detected by sync.</summary>
    public DateTime? ChangedAt { get; set; }

    // Navigation: history of all previous bookings for this slot
    public ICollection<TravelBookingHistory> History { get; set; } = new List<TravelBookingHistory>();
}
