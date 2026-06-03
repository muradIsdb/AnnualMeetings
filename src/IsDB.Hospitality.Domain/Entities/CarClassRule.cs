using IsDB.Hospitality.Domain.Common;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Maps an EventsAir registration type name to a default car class.
/// Used by the auto-assign feature to bulk-assign DeservedCarClassId to guests.
/// </summary>
public class CarClassRule : BaseEntity
{
    /// <summary>Exact match against Guest.RegistrationTypeName (case-insensitive).</summary>
    public string RegistrationTypeName { get; set; } = string.Empty;

    /// <summary>The car class to assign to guests with this registration type.</summary>
    public Guid CarClassId { get; set; }
    public CarClass CarClass { get; set; } = null!;

    /// <summary>Lower number = higher priority when multiple rules could match.</summary>
    public int Priority { get; set; } = 10;

    /// <summary>Optional human-readable note explaining why this rule exists.</summary>
    public string? Notes { get; set; }

    /// <summary>The EventsAir event code this rule belongs to. Null = legacy (treated as current event).</summary>
    public string? EventCode { get; set; }
}
