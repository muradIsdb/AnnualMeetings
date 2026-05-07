using IsDB.Hospitality.Domain.Common;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Departure shuttle registration submitted by an anonymous participant.
/// One record per email address (upsert on email).
/// </summary>
public class DepartureRequest : BaseEntity
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Unique per participant — used as the upsert key.</summary>
    public string Email { get; set; } = string.Empty;

    public string RoomNumber { get; set; } = string.Empty;

    // ─── FK references to Platform Settings ──────────────────────────────────
    public Guid HotelOptionId { get; set; }
    public HotelOption HotelOption { get; set; } = null!;

    public Guid PickupDayOptionId { get; set; }
    public PickupDayOption PickupDayOption { get; set; } = null!;

    public Guid PickupHourOptionId { get; set; }
    public PickupHourOption PickupHourOption { get; set; } = null!;

    /// <summary>Participant accepted the disclaimer.</summary>
    public bool DisclaimerAccepted { get; set; } = true;

    /// <summary>
    /// Unique token sent to the participant's email for managing (edit/cancel) their registration.
    /// Generated once on first submission and reused on upsert.
    /// </summary>
    public Guid ManageToken { get; set; } = Guid.NewGuid();

    /// <summary>Timestamp of first submission.</summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
