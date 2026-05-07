using IsDB.Hospitality.Application.Common.Models;

namespace IsDB.Hospitality.Application.Common.Interfaces;

public interface IFlightTrackerClient
{
    Task<FlightStatusDto?> GetFlightStatusAsync(string flightIata, CancellationToken cancellationToken = default);
}
