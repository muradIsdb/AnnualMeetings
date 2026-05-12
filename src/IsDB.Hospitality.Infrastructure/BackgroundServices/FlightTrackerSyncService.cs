using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IsDB.Hospitality.Infrastructure.BackgroundServices;

public class FlightTrackerSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FlightTrackerSyncService> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5);

    public FlightTrackerSyncService(IServiceProvider serviceProvider, ILogger<FlightTrackerSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Flight Tracker Sync Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncFlightsAsync(stoppingToken);
                await Task.Delay(_syncInterval, stoppingToken);
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

        try
        {
            // Only track flights for guests who are expected or in transit
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

            _logger.LogInformation("Tracking {Count} active flights.", activeFlights.Count);

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
                    _logger.LogInformation("Updated flight {FlightNumber} status to {Status}", flight.FlightNumber, newStatus.ToString());
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
