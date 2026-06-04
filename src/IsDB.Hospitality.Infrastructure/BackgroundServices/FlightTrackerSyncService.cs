using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.ExternalClients.FlightTracker;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IsDB.Hospitality.Infrastructure.BackgroundServices;

public class FlightTrackerSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FlightTrackerSyncService> _logger;
    private readonly AviationstackOptions _options;

    public FlightTrackerSyncService(
        IServiceProvider serviceProvider,
        ILogger<FlightTrackerSyncService> logger,
        IOptions<AviationstackOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Flight Tracker Sync Service started. Interval={Interval}m, TrackingWindow={Window}h.",
            _options.SyncIntervalMinutes, _options.TrackingWindowHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncFlightsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(_options.SyncIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown — exit gracefully
                break;
            }
        }
    }

    private async Task SyncFlightsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var flightTracker = scope.ServiceProvider.GetRequiredService<IFlightTrackerClient>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<IsDB.Hospitality.Infrastructure.BackgroundServices.FlightHubProxy>>();

        try
        {
            var windowCutoff = DateTime.UtcNow.AddHours(_options.TrackingWindowHours);

            // Only track flights for guests who are expected or in transit,
            // whose scheduled arrival is within the configured tracking window,
            // and that have not already landed or been cancelled.
            var activeFlights = await context.Flights
                .Include(f => f.TravelBookings)
                    .ThenInclude(tb => tb.Guest)
                .Where(f => f.TravelBookings.Any(tb =>
                            tb.Guest.Status == GuestStatus.Expected ||
                            tb.Guest.Status == GuestStatus.ArrivedAtAirport ||
                            tb.Guest.Status == GuestStatus.DepartingHotel ||
                            tb.Guest.Status == GuestStatus.AtAirportDeparture))
                .Where(f => f.Status != FlightStatus.Landed && f.Status != FlightStatus.Cancelled)
                .Where(f => f.ScheduledArrival == null || f.ScheduledArrival <= windowCutoff)
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Tracking {Count} active flights within {Window}h window.",
                activeFlights.Count, _options.TrackingWindowHours);

            foreach (var flight in activeFlights)
            {
                var status = await flightTracker.GetFlightStatusAsync(flight.FlightNumber, cancellationToken);
                if (status == null) continue;

                bool changed = false;

                var newStatus = ParseFlightStatus(status.Status);
                if (flight.Status != newStatus) { flight.Status = newStatus; changed = true; }
                if (status.ActualArrival.HasValue && flight.ActualArrival != status.ActualArrival)
                {
                    flight.ActualArrival = status.ActualArrival;
                    changed = true;
                }
                if (status.ActualDeparture.HasValue && flight.ActualDeparture != status.ActualDeparture)
                {
                    flight.ActualDeparture = status.ActualDeparture;
                    changed = true;
                }
                if (status.Terminal != null && flight.ActualTerminal != status.Terminal) { flight.ActualTerminal = status.Terminal; changed = true; }
                if (status.Gate != null && flight.ActualGate != status.Gate) { flight.ActualGate = status.Gate; changed = true; }
                if (status.DelayMinutes.HasValue && flight.LiveDelayMinutes != status.DelayMinutes)
                {
                    flight.LiveDelayMinutes = status.DelayMinutes;
                    changed = true;
                }

                // Layer 2 timestamp — always updated on each poll
                flight.LastTrackedAt = DateTime.UtcNow;

                if (changed)
                {
                    _logger.LogInformation(
                        "Updated flight {FlightNumber} status to {Status}",
                        flight.FlightNumber, newStatus.ToString());

                    // Broadcast real-time update to all connected airport clients
                    await hubContext.Clients.Group("airport").SendAsync(
                        "FlightUpdated",
                        new
                        {
                            flightId = flight.Id,
                            flightNumber = flight.FlightNumber,
                            status = newStatus.ToString().ToLower(),
                            actualArrival = flight.ActualArrival,
                            terminal = flight.ActualTerminal,
                            gate = flight.ActualGate,
                            liveDelayMinutes = flight.LiveDelayMinutes,
                            lastTrackedAt = flight.LastTrackedAt
                        },
                        cancellationToken);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flight tracker sync failed.");
        }
    }

    private static FlightStatus ParseFlightStatus(string? status) => status?.ToLower() switch
    {
        "active" => FlightStatus.Active,
        "landed" => FlightStatus.Landed,
        "cancelled" => FlightStatus.Cancelled,
        "diverted" => FlightStatus.Diverted,
        "scheduled" => FlightStatus.Scheduled,
        _ => FlightStatus.Unknown
    };
}
