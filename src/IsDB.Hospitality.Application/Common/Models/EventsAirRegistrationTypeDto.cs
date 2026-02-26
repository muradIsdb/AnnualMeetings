namespace IsDB.Hospitality.Application.Common.Models;

/// <summary>
/// Represents a registration type as returned by the EventsAir GraphQL API.
/// </summary>
public class EventsAirRegistrationTypeDto
{
    /// <summary>The EventsAir internal ID for this registration type.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The human-readable name of the registration type (e.g. "VIP Delegate").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The optional unique code assigned to this registration type (e.g. "VIP_DELEGATE").</summary>
    public string? UniqueCode { get; set; }
}
