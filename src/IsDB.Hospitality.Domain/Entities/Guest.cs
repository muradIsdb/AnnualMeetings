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

    // Navigation properties
    public ICollection<Flight> Flights { get; set; } = new List<Flight>();
    public ICollection<ChecklistCompletion> ChecklistCompletions { get; set; } = new List<ChecklistCompletion>();
    public ICollection<VehicleAssignment> VehicleAssignments { get; set; } = new List<VehicleAssignment>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<DepartureRequest> DepartureRequests { get; set; } = new List<DepartureRequest>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
