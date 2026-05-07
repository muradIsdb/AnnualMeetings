using IsDB.Hospitality.Domain.Common;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Audit trail for travel booking changes. A new record is written every time
/// EventsAir sync replaces a guest's arrival or departure booking with a different flight.
/// </summary>
public class TravelBookingHistory : BaseEntity
{
    /// <summary>The booking that was changed.</summary>
    public Guid TravelBookingId { get; set; }
    public TravelBooking TravelBooking { get; set; } = null!;

    /// <summary>Denormalised guest reference for easy querying.</summary>
    public Guid GuestId { get; set; }

    /// <summary>Flight number before the change.</summary>
    public string PreviousFlightNumber { get; set; } = string.Empty;

    public string? PreviousAirlineName { get; set; }
    public DateTime? PreviousScheduledArrival { get; set; }
    public DateTime? PreviousScheduledDeparture { get; set; }
    public string? PreviousDeparturePort { get; set; }
    public string? PreviousArrivalPort { get; set; }
    public string? PreviousSeatClass { get; set; }

    /// <summary>UTC timestamp when the sync detected this change.</summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
