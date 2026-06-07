using IsDB.Hospitality.Application.Common.Models;

namespace IsDB.Hospitality.Application.Common.Interfaces;

public interface IFlightTrackerClient
{
    /// <summary>
    /// Fetches live flight status from AviationStack.
    /// If <paramref name="apiKeyOverride"/> is provided, it takes precedence over the
    /// key configured in appsettings / IOptions (allows DB-stored key to be used at runtime).
    /// </summary>
    /// <summary>
    /// <paramref name="flightDate"/> (yyyy-MM-dd) is passed to AviationStack as the
    /// <c>flight_date</c> query parameter so that daily flights (e.g. TK334) return
    /// the correct date instance rather than today's active flight.
    /// </summary>
    Task<FlightStatusDto?> GetFlightStatusAsync(
        string flightIata,
        DateOnly? flightDate = null,
        CancellationToken cancellationToken = default,
        string? apiKeyOverride = null);
}
