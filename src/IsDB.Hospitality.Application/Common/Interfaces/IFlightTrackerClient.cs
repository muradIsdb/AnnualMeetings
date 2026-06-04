using IsDB.Hospitality.Application.Common.Models;

namespace IsDB.Hospitality.Application.Common.Interfaces;

public interface IFlightTrackerClient
{
    /// <summary>
    /// Fetches live flight status from AviationStack.
    /// If <paramref name="apiKeyOverride"/> is provided, it takes precedence over the
    /// key configured in appsettings / IOptions (allows DB-stored key to be used at runtime).
    /// </summary>
    Task<FlightStatusDto?> GetFlightStatusAsync(
        string flightIata,
        CancellationToken cancellationToken = default,
        string? apiKeyOverride = null);
}
