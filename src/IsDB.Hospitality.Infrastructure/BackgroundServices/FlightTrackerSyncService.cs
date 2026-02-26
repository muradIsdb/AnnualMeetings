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
            await SyncFlightsAsync(stoppingToken);
            await Task.Delay(_syncInterval, stoppingToken);
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
                .Include(f => f.Guest)
                .Where(f => f.Guest.Status == GuestStatus.Expected ||
                            f.Guest.Status == GuestStatus.ArrivedAtAirport ||
                            f.Guest.Status == GuestStatus.DepartingHotel ||
                            f.Guest.Status == GuestStatus.AtAirportDeparture)
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
                if (status.Terminal != null && flight.Terminal != status.Terminal) { flight.Terminal = status.Terminal; changed = true; }
                if (status.Gate != null && flight.Gate != status.Gate) { flight.Gate = status.Gate; changed = true; }

                flight.LastTrackedAt = DateTime.UtcNow;

                if (changed)
                    _logger.LogInformation("Updated flight {FlightNumber} status to {Status}", flight.FlightNumber, newStatus);
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
