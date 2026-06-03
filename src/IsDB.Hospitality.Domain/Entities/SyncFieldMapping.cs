using IsDB.Hospitality.Domain.Common;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Represents a configurable EventsAir custom contact field used during sync.
/// </summary>
public class SyncFieldMapping : BaseEntity
{
    /// <summary>Human-readable display name shown on the Field Mappings page (e.g. "Rank", "Dedicated Car")</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The EventsAir custom contact field GUID</summary>
    public string EventsAirFieldGuid { get; set; } = string.Empty;

    /// <summary>Optional description of what this field represents</summary>
    public string? Description { get; set; }

    /// <summary>Display order on the Field Mappings page</summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>The EventsAir event code this mapping belongs to. NULL = applies to all events (legacy).</summary>
    public string? EventCode { get; set; }

    /// <summary>
    /// Controls how this field is used during sync:
    /// - "DedicatedCar": Primary filter — guests without a value for this field are deactivated
    ///   (unless they have an active vehicle assignment). Value stored in Guest.DedicatedCar.
    /// - "Rank": Display only — value is stored in Guest.RankValue but never used for filtering.
    /// - "Filter": Legacy filter — guests are included only if their value matches a selected value.
    /// Defaults to "Filter" for backward compatibility.
    /// </summary>
    public string FieldRole { get; set; } = "Filter";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation: the selected values for this field
    public ICollection<SyncFieldValue> SelectedValues { get; set; } = new List<SyncFieldValue>();
}
