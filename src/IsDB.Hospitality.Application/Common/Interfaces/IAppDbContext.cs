using IsDB.Hospitality.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Guest> Guests { get; }
    DbSet<Flight> Flights { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<VehicleAssignment> VehicleAssignments { get; }
    DbSet<Driver> Drivers { get; }
    DbSet<StaffUserRole> StaffUserRoles { get; }
    DbSet<ChecklistItem> ChecklistItems { get; }
    DbSet<ChecklistCompletion> ChecklistCompletions { get; }
    DbSet<Alert> Alerts { get; }
    DbSet<StaffUser> StaffUsers { get; }
    DbSet<DepartureRequest> DepartureRequests { get; }
    DbSet<SyncRecord> SyncRecords { get; }
    DbSet<EventsAirConfig> EventsAirConfigs { get; }
    DbSet<EventsAirSyncLog> EventsAirSyncLogs { get; }
    DbSet<HotelOption> HotelOptions { get; }
    DbSet<PickupDayOption> PickupDayOptions { get; }
    DbSet<PickupHourOption> PickupHourOptions { get; }
    DbSet<RegistrationType> RegistrationTypes { get; }
    DbSet<SyncFieldMapping> SyncFieldMappings { get; }
    DbSet<SyncFieldValue> SyncFieldValues { get; }
    DbSet<CarClass> CarClasses { get; }
    DbSet<GuestStatusHistory> GuestStatusHistories { get; }
    DbSet<AppConfig> AppConfigs { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationRead> NotificationReads { get; }
    DbSet<CarClassRule> CarClassRules { get; }
    DbSet<TravelBooking> TravelBookings { get; }
    DbSet<TravelBookingHistory> TravelBookingHistories { get; }
    DbSet<FlightSyncLog> FlightSyncLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
