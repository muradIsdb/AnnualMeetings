using IsDB.Hospitality.Infrastructure.BackgroundServices;
using Microsoft.AspNetCore.SignalR;

namespace IsDB.Hospitality.API.Hubs;

/// <summary>
/// SignalR hub for real-time flight status updates.
/// Inherits from FlightHubProxy so IHubContext&lt;FlightHubProxy&gt; resolves correctly.
/// Clients join the "airport" group on connect to receive FlightUpdated events.
/// </summary>
public class FlightHub : FlightHubProxy
{
    public override async Task OnConnectedAsync()
    {
        // All connecting clients automatically join the airport group.
        // In future this can be scoped by role using Context.User.
        await Groups.AddToGroupAsync(Context.ConnectionId, "airport");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "airport");
        await base.OnDisconnectedAsync(exception);
    }
}
