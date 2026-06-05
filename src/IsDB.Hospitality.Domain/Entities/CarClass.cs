using IsDB.Hospitality.Domain.Common;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Represents a car class (e.g., "Luxury Car", "AMOC Car", "Standard Car").
/// Vehicles are assigned to a class, and guests are given a "deserved class"
/// so that only matching vehicles appear during assignment.
/// </summary>
public class CarClass : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Hex color code for UI badge display (e.g., "#4F46E5").</summary>
    public string? Color { get; set; }

    /// <summary>Display sort order in lists.</summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>The EventsAir event code this car class belongs to. Null = legacy (treated as current event).</summary>
    public string? EventCode { get; set; }

    // Navigation
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<Guest> Guests { get; set; } = new List<Guest>();
}
