using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.ExternalClients.FlightTracker;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

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
                var (apiKey, intervalMinutes, windowHours, dateGuardDays) = await GetEffectiveConfigAsync(stoppingToken);

                if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "REPLACE_WITH_AVIATIONSTACK_API_KEY")
                {
                    _logger.LogWarning("AviationStack API key is not configured. Skipping flight sync cycle.");
                }
                else
                {
                    await SyncFlightsAsync(apiKey, windowHours, dateGuardDays, stoppingToken);
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
    /// Returns the effective (apiKey, syncIntervalMinutes, trackingWindowHours, dateGuardDays).
    /// DB values take precedence over appsettings.json / environment variables.
    /// </summary>
    private async Task<(string apiKey, int intervalMinutes, int windowHours, int dateGuardDays)> GetEffectiveConfigAsync(CancellationToken ct)
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

            var dateGuard = (dbConfig?.AviationstackDateGuardDays ?? 0) > 0
                ? dbConfig!.AviationstackDateGuardDays
                : _options.DateGuardDays;

            return (apiKey ?? string.Empty, interval, window, dateGuard);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read AviationStack config from DB; using appsettings defaults.");
            return (_options.ApiKey ?? string.Empty, _options.SyncIntervalMinutes, _options.TrackingWindowHours, _options.DateGuardDays);
        }
    }

    private async Task SyncFlightsAsync(string apiKey, int trackingWindowHours, int dateGuardDays, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var flightTracker = scope.ServiceProvider.GetRequiredService<IFlightTrackerClient>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<IsDB.Hospitality.Infrastructure.BackgroundServices.FlightHubProxy>>();

        var sw = Stopwatch.StartNew();
        int inWindow = 0, queried = 0, updated = 0;
        string logStatus = "Success";
        string? logMessage = null;

        try
        {
            // Only poll flights whose scheduled arrival falls within the configured tracking
            // window: i.e. arriving between now and (now + trackingWindowHours).
            var windowCutoff = DateTime.UtcNow.AddHours(trackingWindowHours);
            var activeFlights = await context.Flights
                .Include(f => f.TravelBookings)
                    .ThenInclude(tb => tb.Guest)
                .Where(f => f.TravelBookings.Any(tb =>
                            tb.Guest.Status == GuestStatus.Expected ||
                            tb.Guest.Status == GuestStatus.ArrivedAtAirport ||
                            tb.Guest.Status == GuestStatus.DepartingHotel ||
                            tb.Guest.Status == GuestStatus.AtAirportDeparture))
                .Where(f => f.Status != FlightStatus.Landed && f.Status != FlightStatus.Cancelled)
                .Where(f => f.ScheduledArrival <= windowCutoff)
                .ToListAsync(cancellationToken);

            inWindow = activeFlights.Count;

            _logger.LogInformation(
                "Tracking {Count} active flights within {Window}h window.",
                activeFlights.Count, trackingWindowHours);

            foreach (var flight in activeFlights)
            {
                var status = await flightTracker.GetFlightStatusAsync(flight.FlightNumber, cancellationToken, apiKey);
                if (status == null) continue;

                // Reject AviationStack results that belong to a different day's flight (configurable tolerance).
                if (status.ScheduledArrival.HasValue)
                {
                    var dayDiff = Math.Abs((status.ScheduledArrival.Value.Date - flight.ScheduledArrival.Date).TotalDays);
                    if (dayDiff > dateGuardDays)
                    {
                        _logger.LogDebug(
                            "Skipping flight {FlightNumber}: AviationStack returned {ApiDate}, DB expects {DbDate} (diff={Diff}d, guard={Guard}d)",
                            flight.FlightNumber,
                            status.ScheduledArrival.Value.Date.ToString("yyyy-MM-dd"),
                            flight.ScheduledArrival.Date.ToString("yyyy-MM-dd"),
                            dayDiff,
                            dateGuardDays);
                        flight.LastTrackedAt = DateTime.UtcNow;
                        continue;
                    }
                }

                queried++;
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
                // Update carrier name from AviationStack if the DB has a missing or placeholder value
                if (!string.IsNullOrWhiteSpace(status.Airline) &&
                    (string.IsNullOrWhiteSpace(flight.AirlineName) || flight.AirlineName == "Unknown"))
                {
                    flight.AirlineName = status.Airline;
                    changed = true;
                }

                flight.LastTrackedAt = DateTime.UtcNow;

                if (changed)
                {
                    updated++;
                    _logger.LogInformation(
                        "Updated flight {FlightNumber} status to {Status}",
                        flight.FlightNumber, newStatus.ToString());

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

            if (inWindow == 0)
                logMessage = "No active flights within the tracking window.";
            else if (queried == 0)
                logMessage = $"Scheduled sync — {inWindow} flight(s) in window, none due for live data yet.";
            else
                logMessage = $"Scheduled sync — {queried} flight(s) queried, {updated} updated.";
        }
        catch (Exception ex)
        {
            logStatus = "Failed";
            logMessage = $"Sync failed: {ex.Message}";
            _logger.LogError(ex, "Flight tracker sync failed.");
        }
        finally
        {
            sw.Stop();
            try
            {
                using var logScope = _serviceProvider.CreateScope();
                var logContext = logScope.ServiceProvider.GetRequiredService<AppDbContext>();
                logContext.FlightSyncLogs.Add(new FlightSyncLog
                {
                    SyncedAt = DateTime.UtcNow,
                    TriggerSource = "Scheduled",
                    Status = logStatus,
                    FlightsInWindow = inWindow,
                    FlightsQueried = queried,
                    FlightsUpdated = updated,
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    Message = logMessage,
                    InitiatedByStaffName = null
                });
                await logContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to write FlightSyncLog entry.");
            }
        }
    }

    /// <summary>
    /// Triggers one immediate sync cycle outside the normal timer.
    /// Returns a summary of what was polled and updated.
    /// </summary>
    public async Task<SyncResult> TriggerSyncNowAsync(CancellationToken cancellationToken = default, string? initiatedByStaffName = null)
    {
        var (apiKey, _, windowHours, dateGuardDays) = await GetEffectiveConfigAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "REPLACE_WITH_AVIATIONSTACK_API_KEY")
            return new SyncResult(0, 0, "AviationStack API key is not configured.");

        var result = await SyncFlightsAndCountAsync(apiKey, windowHours, dateGuardDays, cancellationToken, initiatedByStaffName);
        return result;
    }

    private async Task<SyncResult> SyncFlightsAndCountAsync(string apiKey, int trackingWindowHours, int dateGuardDays, CancellationToken cancellationToken, string? initiatedByStaffName = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var flightTracker = scope.ServiceProvider.GetRequiredService<IFlightTrackerClient>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<FlightHubProxy>>();

        var sw = Stopwatch.StartNew();
        int inWindow = 0, queried = 0, updated = 0;
        string logStatus = "Success";
        string? logMessage = null;

        try
        {
            // Apply the same tracking window filter as the background sync.
            var windowCutoff = DateTime.UtcNow.AddHours(trackingWindowHours);
            var activeFlights = await context.Flights
                .Include(f => f.TravelBookings).ThenInclude(tb => tb.Guest)
                .Where(f => f.TravelBookings.Any(tb =>
                    tb.Guest.Status == GuestStatus.Expected ||
                    tb.Guest.Status == GuestStatus.ArrivedAtAirport ||
                    tb.Guest.Status == GuestStatus.DepartingHotel ||
                    tb.Guest.Status == GuestStatus.AtAirportDeparture))
                .Where(f => f.Status != FlightStatus.Landed && f.Status != FlightStatus.Cancelled)
                .Where(f => f.ScheduledArrival <= windowCutoff)
                .ToListAsync(cancellationToken);

            inWindow = activeFlights.Count;

            foreach (var flight in activeFlights)
            {
                var status = await flightTracker.GetFlightStatusAsync(flight.FlightNumber, cancellationToken, apiKey);
                if (status == null) continue;

                // Reject AviationStack results that belong to a different day's flight (configurable tolerance).
                if (status.ScheduledArrival.HasValue)
                {
                    var dayDiff = Math.Abs((status.ScheduledArrival.Value.Date - flight.ScheduledArrival.Date).TotalDays);
                    if (dayDiff > dateGuardDays)
                    {
                        _logger.LogDebug(
                            "Skipping flight {FlightNumber}: AviationStack returned {ApiDate}, DB expects {DbDate} (diff={Diff}d, guard={Guard}d)",
                            flight.FlightNumber,
                            status.ScheduledArrival.Value.Date.ToString("yyyy-MM-dd"),
                            flight.ScheduledArrival.Date.ToString("yyyy-MM-dd"),
                            dayDiff,
                            dateGuardDays);
                        flight.LastTrackedAt = DateTime.UtcNow;
                        continue;
                    }
                }

                // This flight passed the date guard — count it as actually queried
                queried++;

                bool changed = false;
                var newStatus = ParseFlightStatus(status.Status);
                if (flight.Status != newStatus) { flight.Status = newStatus; changed = true; }
                if (status.ActualArrival.HasValue && flight.ActualArrival != status.ActualArrival) { flight.ActualArrival = status.ActualArrival; changed = true; }
                if (status.ActualDeparture.HasValue && flight.ActualDeparture != status.ActualDeparture) { flight.ActualDeparture = status.ActualDeparture; changed = true; }
                if (status.Terminal != null && flight.ActualTerminal != status.Terminal) { flight.ActualTerminal = status.Terminal; changed = true; }
                if (status.Gate != null && flight.ActualGate != status.Gate) { flight.ActualGate = status.Gate; changed = true; }
                if (status.DelayMinutes.HasValue && flight.LiveDelayMinutes != status.DelayMinutes) { flight.LiveDelayMinutes = status.DelayMinutes; changed = true; }
                // Update carrier name from AviationStack if the DB has a missing or placeholder value
                if (!string.IsNullOrWhiteSpace(status.Airline) &&
                    (string.IsNullOrWhiteSpace(flight.AirlineName) || flight.AirlineName == "Unknown"))
                { flight.AirlineName = status.Airline; changed = true; }
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

            if (inWindow == 0)
                logMessage = "No active flights within the tracking window.";
            else if (queried == 0)
                logMessage = $"Sync complete — {inWindow} flight(s) in window, none due for live data yet.";
            else
                logMessage = $"Sync complete — {queried} flight(s) queried, {updated} updated.";

            return new SyncResult(queried, updated, logMessage);
        }
        catch (Exception ex)
        {
            logStatus = "Failed";
            logMessage = $"Sync failed: {ex.Message}";
            _logger.LogError(ex, "Manual flight sync failed.");
            return new SyncResult(queried, updated, logMessage);
        }
        finally
        {
            sw.Stop();
            try
            {
                // Write sync log entry — use a fresh scope so it always commits even if main context failed
                using var logScope = _serviceProvider.CreateScope();
                var logContext = logScope.ServiceProvider.GetRequiredService<AppDbContext>();
                logContext.FlightSyncLogs.Add(new FlightSyncLog
                {
                    SyncedAt = DateTime.UtcNow,
                    TriggerSource = "Manual",
                    Status = logStatus,
                    FlightsInWindow = inWindow,
                    FlightsQueried = queried,
                    FlightsUpdated = updated,
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    Message = logMessage,
                    InitiatedByStaffName = initiatedByStaffName
                });
                await logContext.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to write FlightSyncLog entry.");
            }
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
