using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleAssignment> VehicleAssignments => Set<VehicleAssignment>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<ChecklistCompletion> ChecklistCompletions => Set<ChecklistCompletion>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<DepartureRequest> DepartureRequests => Set<DepartureRequest>();
    public DbSet<SyncRecord> SyncRecords => Set<SyncRecord>();
    public DbSet<EventsAirConfig> EventsAirConfigs => Set<EventsAirConfig>();
    public DbSet<EventsAirSyncLog> EventsAirSyncLogs => Set<EventsAirSyncLog>();
    public DbSet<HotelOption> HotelOptions => Set<HotelOption>();
    public DbSet<PickupDayOption> PickupDayOptions => Set<PickupDayOption>();
    public DbSet<PickupHourOption> PickupHourOptions => Set<PickupHourOption>();
    public DbSet<RegistrationType> RegistrationTypes => Set<RegistrationType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
