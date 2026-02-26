using IsDB.Hospitality.Application.Common.Models;

namespace IsDB.Hospitality.Application.Common.Interfaces;

public interface IEventsAirClient
{
    Task<List<EventsAirContactDto>> GetAllContactsAsync(CancellationToken cancellationToken = default);
    Task<EventsAirContactDto?> GetContactByIdAsync(string contactId, CancellationToken cancellationToken = default);
    Task<bool> UploadContactPhotoAsync(string contactId, byte[] photoData, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all registration types defined for the configured event from the EventsAir GraphQL API.
    /// </summary>
    Task<List<EventsAirRegistrationTypeDto>> GetRegistrationTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all registrations for the event, optionally filtered by registration type IDs.
    /// When registrationTypeIds is empty, returns ALL registrations.
    /// Returns contacts with their registration type information.
    /// </summary>
    Task<List<EventsAirRegistrationDto>> GetRegistrationsByTypeAsync(
        IEnumerable<string> registrationTypeIds,
        CancellationToken cancellationToken = default);
}
