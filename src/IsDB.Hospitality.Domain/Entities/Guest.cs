using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

public class Guest : BaseEntity
{
    public string EventsAirContactId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Organization { get; set; }
    public string? Designation { get; set; }
    public string? Nationality { get; set; }
    public string? Country { get; set; }
    public string? PassportNumber { get; set; }
    public string? PhotoUrl { get; set; }
    public string? MobileNumber { get; set; }
    public string? Email { get; set; }
    public bool IsCritical { get; set; }
    public bool RequiresAccessibility { get; set; }
    public GuestStatus Status { get; set; } = GuestStatus.Expected;
    public string? GroupCode { get; set; }
    public string? RoomNumber { get; set; }
    public string? HotelName { get; set; }
    public string? SpecialRequirements { get; set; }
    public string? Notes { get; set; }
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>EventsAir registration type ID (GUID) this guest registered under</summary>
    public string? RegistrationTypeId { get; set; }

    /// <summary>Human-readable registration type name, e.g. "Governor"</summary>
    public string? RegistrationTypeName { get; set; }

    /// <summary>Whether this guest is active (visible in operations). Set to false when their registration type is deselected from sync.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The EventsAir event code (GUID) this guest belongs to.
    /// Stamped during sync with the currently active event code.
    /// Used to filter guests when switching between events.
    /// </summary>
    public string? EventCode { get; set; }

    /// <summary>
    /// The value of the Rank custom field fetched from EventsAir (e.g. "VVIP", "VIP", "Official").
    /// Populated in the second-pass sync. Stored for display only — not used for filtering.
    /// Null if the contact has no Rank value in EventsAir.
    /// </summary>
    public string? RankValue { get; set; }

    /// <summary>
    /// The value of the "Dedicated Car" custom field from EventsAir (GUID: d6b74b23-c8b6-d044-5d86-3a17bafe27de).
    /// Used as the primary filter after Registration Types: guests without a Dedicated Car value
    /// are deactivated after sync, unless they have an active vehicle assignment.
    /// </summary>
    /// <summary>
    /// The value of the "Vehicle Types" custom field fetched from EventsAir (e.g. "Hyundai Elantra", "Toyota Land Cruiser").
    /// Populated during sync. Stored for display only.
    /// </summary>
    public string? VehicleTypeValue { get; set; }
    public string? DedicatedCar { get; set; }

    /// <summary>
    /// Whether this guest is entitled to a dedicated liaison officer.
    /// Populated from EventsAir custom field f4d27526-7af9-5ed4-ebe1-3a1d4e2e471d (checkbox).
    /// </summary>
    public bool? LiaisonOfficer { get; set; }

    /// <summary>
    /// Whether this guest is invited to attend the opening ceremony.
    /// Populated from EventsAir marketing tag "Invited to attend the opening ceremony".
    /// </summary>
    public bool? InvitedToOpeningCeremony { get; set; }

    /// <summary>
    /// The hotel name from EventsAir marketing tag "Hotel" (displayed as "Old Hotel").
    /// Populated during Sync Marketing Tags.
    /// </summary>
    public string? OldHotel { get; set; }

    // Navigation properties
    public ICollection<TravelBooking> TravelBookings { get; set; } = new List<TravelBooking>();
    public ICollection<ChecklistCompletion> ChecklistCompletions { get; set; } = new List<ChecklistCompletion>();
    public ICollection<VehicleAssignment> VehicleAssignments { get; set; } = new List<VehicleAssignment>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();

    /// <summary>
    /// The car class this guest deserves (set manually by admin).
    /// Only vehicles of this class will appear in the assignment popup.
    /// If null, all vehicles are shown with a warning.
    /// </summary>
    public Guid? DeservedCarClassId { get; set; }
    public CarClass? DeservedCarClass { get; set; }

    // ─── Journey Status Flow ──────────────────────────────────────────────────

    /// <summary>Inbound journey status (Airport → Hotel). Default: ArrivalScheduled.</summary>
    public InboundStatus InboundStatus { get; set; } = InboundStatus.ArrivalScheduled;

    /// <summary>Independent flag: Embassy team has received the guest at airport. Set by Airport/Admin.</summary>
    public bool ReceivedByEmbassyTeam { get; set; } = false;

    /// <summary>Outbound journey status (Hotel → Departure). Only meaningful when InboundStatus == AtHotel.</summary>
    public OutboundStatus? OutboundStatus { get; set; } = null;

    /// <summary>Status history log entries for this guest.</summary>
    public ICollection<GuestStatusHistory> StatusHistory { get; set; } = new List<GuestStatusHistory>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
