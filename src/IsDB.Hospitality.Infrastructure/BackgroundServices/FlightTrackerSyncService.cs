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
    /// <summary>Result returned by a manual sync trigger.</summary>
    public record SyncResult(int FlightsTracked, int FlightsUpdated, string Message);

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
            "Flight Tracker Sync Service started. Default interval={Interval}m, window={Window}h.",
            _options.SyncIntervalMinutes, _options.TrackingWindowHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Read effective config from DB each cycle (DB overrides appsettings)
                var (apiKey, intervalMinutes, windowHours) = await GetEffectiveConfigAsync(stoppingToken);

                if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "REPLACE_WITH_AVIATIONSTACK_API_KEY")
                {
                    _logger.LogWarning("AviationStack API key is not configured. Skipping flight sync cycle.");
                }
                else
                {
                    await SyncFlightsAsync(apiKey, windowHours, stoppingToken);
                }

                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown — exit gracefully
                break;
            }
        }
    }

    /// <summary>
    /// Returns the effective (apiKey, syncIntervalMinutes, trackingWindowHours).
    /// DB values take precedence over appsettings.json / environment variables.
    /// </summary>
    private async Task<(string apiKey, int intervalMinutes, int windowHours)> GetEffectiveConfigAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbConfig = await context.AppConfigs.FindAsync(new object[] { 1 }, ct);

            var apiKey = !string.IsNullOrWhiteSpace(dbConfig?.AviationstackApiKey)
                ? dbConfig.AviationstackApiKey
                : _options.ApiKey;

            var interval = (dbConfig?.AviationstackSyncIntervalMinutes ?? 0) > 0
                ? dbConfig!.AviationstackSyncIntervalMinutes
                : _options.SyncIntervalMinutes;

            var window = (dbConfig?.AviationstackTrackingWindowHours ?? 0) > 0
                ? dbConfig!.AviationstackTrackingWindowHours
                : _options.TrackingWindowHours;

            return (apiKey ?? string.Empty, interval, window);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read AviationStack config from DB; using appsettings defaults.");
            return (_options.ApiKey ?? string.Empty, _options.SyncIntervalMinutes, _options.TrackingWindowHours);
        }
    }

    private async Task SyncFlightsAsync(string apiKey, int trackingWindowHours, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var flightTracker = scope.ServiceProvider.GetRequiredService<IFlightTrackerClient>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<IsDB.Hospitality.Infrastructure.BackgroundServices.FlightHubProxy>>();

        try
        {
            // Track all flights for guests who are expected or in transit
            // that have not already landed or been cancelled.
            // No upper-bound window filter — AviationStack returns "scheduled" with no
            // actuals for far-future flights, so polling them is harmless and ensures
            // the sync works regardless of how far out the event is.
            var activeFlights = await context.Flights
                .Include(f => f.TravelBookings)
                    .ThenInclude(tb => tb.Guest)
                .Where(f => f.TravelBookings.Any(tb =>
                            tb.Guest.Status == GuestStatus.Expected ||
                            tb.Guest.Status == GuestStatus.ArrivedAtAirport ||
                            tb.Guest.Status == GuestStatus.DepartingHotel ||
                            tb.Guest.Status == GuestStatus.AtAirportDeparture))
                .Where(f => f.Status != FlightStatus.Landed && f.Status != FlightStatus.Cancelled)
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Tracking {Count} active flights within {Window}h window.",
                activeFlights.Count, trackingWindowHours);

            foreach (var flight in activeFlights)
            {
                var status = await flightTracker.GetFlightStatusAsync(flight.FlightNumber, cancellationToken, apiKey);
                if (status == null) continue;

                // Fix 2: Reject AviationStack results that belong to a different day's flight.
                // AviationStack returns the most recently operated flight for a given IATA code
                // (typically today's). If the DB flight is scheduled for a different date, the
                // returned data is for the wrong occurrence — skip it.
                // Allow ±1 day tolerance to handle timezone edge cases.
                if (status.ScheduledArrival.HasValue)
                {
                    var dayDiff = Math.Abs((status.ScheduledArrival.Value.Date - flight.ScheduledArrival.Date).TotalDays);
                    if (dayDiff > 1)
                    {
                        _logger.LogDebug(
                            "Skipping flight {FlightNumber}: AviationStack returned {ApiDate}, DB expects {DbDate} (diff={Diff}d)",
                            flight.FlightNumber,
                            status.ScheduledArrival.Value.Date.ToString("yyyy-MM-dd"),
                            flight.ScheduledArrival.Date.ToString("yyyy-MM-dd"),
                            dayDiff);
                        flight.LastTrackedAt = DateTime.UtcNow;
                        continue;
                    }
                }

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

    /// <summary>
    /// Triggers one immediate sync cycle outside the normal timer.
    /// Returns a summary of what was polled and updated.
    /// </summary>
    public async Task<SyncResult> TriggerSyncNowAsync(CancellationToken cancellationToken = default)
    {
        var (apiKey, _, windowHours) = await GetEffectiveConfigAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "REPLACE_WITH_AVIATIONSTACK_API_KEY")
            return new SyncResult(0, 0, "AviationStack API key is not configured.");

        var result = await SyncFlightsAndCountAsync(apiKey, windowHours, cancellationToken);
        return result;
    }

    private async Task<SyncResult> SyncFlightsAndCountAsync(string apiKey, int trackingWindowHours, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var flightTracker = scope.ServiceProvider.GetRequiredService<IFlightTrackerClient>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<FlightHubProxy>>();

        int tracked = 0, updated = 0;
        try
        {
            var activeFlights = await context.Flights
                .Include(f => f.TravelBookings).ThenInclude(tb => tb.Guest)
                .Where(f => f.TravelBookings.Any(tb =>
                    tb.Guest.Status == GuestStatus.Expected ||
                    tb.Guest.Status == GuestStatus.ArrivedAtAirport ||
                    tb.Guest.Status == GuestStatus.DepartingHotel ||
                    tb.Guest.Status == GuestStatus.AtAirportDeparture))
                .Where(f => f.Status != FlightStatus.Landed && f.Status != FlightStatus.Cancelled)
                .ToListAsync(cancellationToken);

            tracked = activeFlights.Count;

            foreach (var flight in activeFlights)
            {
                var status = await flightTracker.GetFlightStatusAsync(flight.FlightNumber, cancellationToken, apiKey);
                if (status == null) continue;

                // Fix 2: Reject AviationStack results that belong to a different day's flight.
                if (status.ScheduledArrival.HasValue)
                {
                    var dayDiff = Math.Abs((status.ScheduledArrival.Value.Date - flight.ScheduledArrival.Date).TotalDays);
                    if (dayDiff > 1)
                    {
                        _logger.LogDebug(
                            "Skipping flight {FlightNumber}: AviationStack returned {ApiDate}, DB expects {DbDate} (diff={Diff}d)",
                            flight.FlightNumber,
                            status.ScheduledArrival.Value.Date.ToString("yyyy-MM-dd"),
                            flight.ScheduledArrival.Date.ToString("yyyy-MM-dd"),
                            dayDiff);
                        flight.LastTrackedAt = DateTime.UtcNow;
                        continue;
                    }
                }
                bool changed = false;;
                var newStatus = ParseFlightStatus(status.Status);
                if (flight.Status != newStatus) { flight.Status = newStatus; changed = true; }
                if (status.ActualArrival.HasValue && flight.ActualArrival != status.ActualArrival) { flight.ActualArrival = status.ActualArrival; changed = true; }
                if (status.ActualDeparture.HasValue && flight.ActualDeparture != status.ActualDeparture) { flight.ActualDeparture = status.ActualDeparture; changed = true; }
                if (status.Terminal != null && flight.ActualTerminal != status.Terminal) { flight.ActualTerminal = status.Terminal; changed = true; }
                if (status.Gate != null && flight.ActualGate != status.Gate) { flight.ActualGate = status.Gate; changed = true; }
                if (status.DelayMinutes.HasValue && flight.LiveDelayMinutes != status.DelayMinutes) { flight.LiveDelayMinutes = status.DelayMinutes; changed = true; }
                flight.LastTrackedAt = DateTime.UtcNow;

                if (changed)
                {
                    updated++;
                    await hubContext.Clients.Group("airport").SendAsync("FlightUpdated",
                        new { flightId = flight.Id, flightNumber = flight.FlightNumber, status = newStatus.ToString().ToLower(),
                              actualArrival = flight.ActualArrival, terminal = flight.ActualTerminal, gate = flight.ActualGate,
                              liveDelayMinutes = flight.LiveDelayMinutes, lastTrackedAt = flight.LastTrackedAt },
                        cancellationToken);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            var msg = tracked == 0
                ? "No active flights within the tracking window."
                : $"Sync complete — {tracked} flight(s) polled, {updated} updated.";
            return new SyncResult(tracked, updated, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual flight sync failed.");
            return new SyncResult(tracked, updated, $"Sync failed: {ex.Message}");
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
