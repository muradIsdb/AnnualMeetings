namespace IsDB.Hospitality.Application.Common.Models;

/// <summary>
/// Represents a single EventsAir registration, combining the contact details
/// with the registration type they registered under.
/// </summary>
public class EventsAirRegistrationDto
{
    /// <summary>EventsAir registration ID (GUID string)</summary>
    public string RegistrationId { get; set; } = string.Empty;

    /// <summary>EventsAir contact ID (GUID string)</summary>
    public string ContactId { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? JobTitle { get; set; }
    public string? OrganizationName { get; set; }
    public string? PrimaryEmail { get; set; }

    /// <summary>EventsAir registration type ID (GUID string)</summary>
    public string RegistrationTypeId { get; set; } = string.Empty;

    /// <summary>Human-readable registration type name, e.g. "Governor"</summary>
    public string RegistrationTypeName { get; set; } = string.Empty;

    /// <summary>Short code for the registration type, e.g. "GOV"</summary>
    public string? RegistrationTypeCode { get; set; }
}
