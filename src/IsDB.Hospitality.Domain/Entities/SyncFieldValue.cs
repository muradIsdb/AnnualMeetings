using IsDB.Hospitality.Domain.Common;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Represents a specific value selected for a SyncFieldMapping filter.
/// For example: Rank = "Executive Director" is selected for sync.
/// </summary>
public class SyncFieldValue : BaseEntity
{
    /// <summary>The parent field mapping</summary>
    public Guid SyncFieldMappingId { get; set; }

    /// <summary>The actual value of the custom field (e.g. "Executive Director", "VIP")</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Whether this value is selected for sync</summary>
    public bool IsSelectedForSync { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public SyncFieldMapping? SyncFieldMapping { get; set; }
}
