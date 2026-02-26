using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IsDB.Hospitality.Infrastructure.BackgroundServices;

public class EventsAirSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventsAirSyncService> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(15);

    public EventsAirSyncService(IServiceProvider serviceProvider, ILogger<EventsAirSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventsAir Sync Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncAsync(stoppingToken);
            await Task.Delay(_syncInterval, stoppingToken);
        }
    }

    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eventsAirClient = scope.ServiceProvider.GetRequiredService<IEventsAirClient>();

        var syncRecord = new SyncRecord
        {
            ServiceName = "EventsAirSync",
            Status = SyncStatus.Pending,
            StartedAt = DateTime.UtcNow
        };
        context.SyncRecords.Add(syncRecord);
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Starting EventsAir sync...");
            var contacts = await eventsAirClient.GetAllContactsAsync(cancellationToken);

            int processed = 0, updated = 0;

            foreach (var contact in contacts)
            {
                processed++;
                var existingGuest = await context.Guests
                    .Include(g => g.Flights)
                    .FirstOrDefaultAsync(g => g.EventsAirContactId == contact.ContactId, cancellationToken);

                if (existingGuest == null)
                {
                    // Create new guest
                    var newGuest = new Guest
                    {
                        EventsAirContactId = contact.ContactId,
                        FirstName = contact.FirstName,
                        LastName = contact.LastName,
                        Title = contact.Title,
                        Organization = contact.Organization,
                        Designation = contact.Designation,
                        Nationality = contact.Nationality,
                        PassportNumber = contact.PassportNumber,
                        PhotoUrl = contact.PhotoUrl,
                        MobileNumber = contact.MobileNumber,
                        Email = contact.Email,
                        IsCritical = contact.IsCritical,
                        RequiresAccessibility = contact.RequiresAccessibility,
                        GroupCode = contact.GroupCode,
                        HotelName = contact.HotelName,
                        RoomNumber = contact.RoomNumber,
                        SpecialRequirements = contact.SpecialRequirements,
                        Status = GuestStatus.Expected,
                        LastSyncedAt = DateTime.UtcNow
                    };

                    // Add arrival flight if present
                    if (!string.IsNullOrEmpty(contact.ArrivalFlightNumber))
                    {
                        newGuest.Flights.Add(new Flight
                        {
                            FlightNumber = contact.ArrivalFlightNumber,
                            ScheduledArrival = contact.ArrivalFlightDate,
                            IsArrival = true,
                            Status = FlightStatus.Scheduled
                        });
                    }

                    // Add departure flight if present
                    if (!string.IsNullOrEmpty(contact.DepartureFlightNumber))
                    {
                        newGuest.Flights.Add(new Flight
                        {
                            FlightNumber = contact.DepartureFlightNumber,
                            ScheduledDeparture = contact.DepartureFlightDate,
                            IsArrival = false,
                            Status = FlightStatus.Scheduled
                        });
                    }

                    context.Guests.Add(newGuest);
                    updated++;
                }
                else
                {
                    // Update existing guest (non-operational fields only)
                    bool changed = false;
                    if (existingGuest.FirstName != contact.FirstName) { existingGuest.FirstName = contact.FirstName; changed = true; }
                    if (existingGuest.LastName != contact.LastName) { existingGuest.LastName = contact.LastName; changed = true; }
                    if (existingGuest.Organization != contact.Organization) { existingGuest.Organization = contact.Organization; changed = true; }
                    if (existingGuest.Designation != contact.Designation) { existingGuest.Designation = contact.Designation; changed = true; }
                    if (existingGuest.PhotoUrl != contact.PhotoUrl) { existingGuest.PhotoUrl = contact.PhotoUrl; changed = true; }
                    if (existingGuest.HotelName != contact.HotelName) { existingGuest.HotelName = contact.HotelName; changed = true; }
                    if (existingGuest.RoomNumber != contact.RoomNumber) { existingGuest.RoomNumber = contact.RoomNumber; changed = true; }

                    if (changed)
                    {
                        existingGuest.LastSyncedAt = DateTime.UtcNow;
                        updated++;
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            syncRecord.Status = SyncStatus.Success;
            syncRecord.CompletedAt = DateTime.UtcNow;
            syncRecord.RecordsProcessed = processed;
            syncRecord.RecordsUpdated = updated;
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("EventsAir sync completed. Processed: {Processed}, Updated: {Updated}", processed, updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EventsAir sync failed.");
            syncRecord.Status = SyncStatus.Failed;
            syncRecord.CompletedAt = DateTime.UtcNow;
            syncRecord.ErrorMessage = ex.Message;
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }
}
