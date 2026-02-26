using IsDB.Hospitality.Application.Common.Models;

namespace IsDB.Hospitality.Application.Common.Interfaces;

public interface IEventsAirClient
{
    Task<List<EventsAirContactDto>> GetAllContactsAsync(CancellationToken cancellationToken = default);
    Task<EventsAirContactDto?> GetContactByIdAsync(string contactId, CancellationToken cancellationToken = default);
    Task<bool> UploadContactPhotoAsync(string contactId, byte[] photoData, string fileName, CancellationToken cancellationToken = default);
}
