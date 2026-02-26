using IsDB.Hospitality.Domain.Common;

namespace IsDB.Hospitality.Domain.Entities;

public class RegistrationType : BaseEntity
{
    /// <summary>
    /// The registration type code from EventsAir (e.g., "DELEGATE", "VIP", "SPEAKER")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of this registration type
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this registration type is selected for sync from EventsAir
    /// </summary>
    public bool IsSelectedForSync { get; set; } = false;

    /// <summary>
    /// Whether this type was imported from EventsAir or manually added
    /// </summary>
    public bool IsFromEventsAir { get; set; } = false;

    /// <summary>
    /// Display order in the selection list
    /// </summary>
    public int SortOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
