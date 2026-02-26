using IsDB.Hospitality.Domain.Common;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Represents a configurable EventsAir custom contact field that is used as a sync filter.
/// For example: "Rank" mapped to EventsAir custom field GUID 3d96b87e-87b0-145e-5f45-3a17bafe26d4
/// </summary>
public class SyncFieldMapping : BaseEntity
{
    /// <summary>Human-readable display name shown as a tab on the Registration Types page (e.g. "Rank")</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The EventsAir custom contact field GUID</summary>
    public string EventsAirFieldGuid { get; set; } = string.Empty;

    /// <summary>Optional description of what this field represents</summary>
    public string? Description { get; set; }

    /// <summary>Display order for the tabs on the Registration Types page</summary>
    public int SortOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation: the selected values for this field
    public ICollection<SyncFieldValue> SelectedValues { get; set; } = new List<SyncFieldValue>();
}
