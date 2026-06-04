using Microsoft.AspNetCore.SignalR;

namespace IsDB.Hospitality.Infrastructure.BackgroundServices;

/// <summary>
/// Proxy Hub class used exclusively for IHubContext&lt;FlightHubProxy&gt; injection
/// in the Infrastructure layer. The actual FlightHub in the API layer inherits
/// from this class so both share the same IHubContext registration.
/// </summary>
public class FlightHubProxy : Hub
{
}
