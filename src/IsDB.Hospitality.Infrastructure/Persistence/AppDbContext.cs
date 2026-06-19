using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Flight> Flights => Set<Flight>();
        public DbSet<TravelBooking> TravelBookings => Set<TravelBooking>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleAssignment> VehicleAssignments => Set<VehicleAssignment>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<StaffUserRole> StaffUserRoles => Set<StaffUserRole>();
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
    public DbSet<SyncFieldMapping> SyncFieldMappings => Set<SyncFieldMapping>();
    public DbSet<SyncFieldValue> SyncFieldValues => Set<SyncFieldValue>();
    public DbSet<CarClass> CarClasses => Set<CarClass>();
    public DbSet<GuestStatusHistory> GuestStatusHistories => Set<GuestStatusHistory>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRead> NotificationReads => Set<NotificationRead>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<CarClassRule> CarClassRules => Set<CarClassRule>();
    public DbSet<TravelBookingHistory> TravelBookingHistories => Set<TravelBookingHistory>();
    public DbSet<VehicleStatusHistory> VehicleStatusHistories => Set<VehicleStatusHistory>();
    public DbSet<FlightSyncLog> FlightSyncLogs => Set<FlightSyncLog>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<SyncAlert> SyncAlerts => Set<SyncAlert>();
    public DbSet<DropOffTrip> DropOffTrips => Set<DropOffTrip>();
    public DbSet<MonitoredParticipant> MonitoredParticipants => Set<MonitoredParticipant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // StaffUserRole composite PK
        modelBuilder.Entity<StaffUserRole>()
            .HasKey(r => new { r.StaffUserId, r.Role });

        modelBuilder.Entity<StaffUserRole>()
            .HasOne(r => r.StaffUser)
            .WithMany(u => u.Roles)
            .HasForeignKey(r => r.StaffUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Vehicle → Driver (1-to-1, optional)
        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.Driver)
            .WithOne(d => d.Vehicle)
            .HasForeignKey<Driver>(d => d.VehicleId)
            .OnDelete(DeleteBehavior.SetNull);

        // Vehicle → CurrentGuest (optional, no cascade to avoid cycles)
        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.CurrentGuest)
            .WithMany()
            .HasForeignKey(v => v.CurrentGuestId)
            .OnDelete(DeleteBehavior.SetNull);

        // VehicleAssignment → Driver (optional snapshot)
        modelBuilder.Entity<VehicleAssignment>()
            .HasOne(a => a.Driver)
            .WithMany()
            .HasForeignKey(a => a.DriverId)
            .OnDelete(DeleteBehavior.SetNull);

        // VehicleAssignment → UnassignedByStaff (optional)
        modelBuilder.Entity<VehicleAssignment>()
            .HasOne(a => a.UnassignedByStaff)
            .WithMany()
            .HasForeignKey(a => a.UnassignedByStaffId)
            .OnDelete(DeleteBehavior.SetNull);

        // TravelBooking -> Guest (Many-to-One)
        modelBuilder.Entity<TravelBooking>()
            .HasOne(tb => tb.Guest)
            .WithMany(g => g.TravelBookings)
            .HasForeignKey(tb => tb.GuestId)
            .OnDelete(DeleteBehavior.Cascade);

        // CarClass → Vehicles (1-to-many, optional)
        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.CarClass)
            .WithMany(c => c.Vehicles)
            .HasForeignKey(v => v.CarClassId)
            .OnDelete(DeleteBehavior.SetNull);

        // CarClass → Guests (1-to-many, optional)
        modelBuilder.Entity<Guest>()
            .HasOne(g => g.DeservedCarClass)
            .WithMany(c => c.Guests)
            .HasForeignKey(g => g.DeservedCarClassId)
            .OnDelete(DeleteBehavior.SetNull);

        // GuestStatusHistory → Guest (many-to-one)
        modelBuilder.Entity<GuestStatusHistory>()
            .HasOne(h => h.Guest)
            .WithMany(g => g.StatusHistory)
            .HasForeignKey(h => h.GuestId)
            .OnDelete(DeleteBehavior.Cascade);

        // GuestStatusHistory → StaffUser (optional)
        modelBuilder.Entity<GuestStatusHistory>()
            .HasOne(h => h.ChangedByStaff)
            .WithMany()
            .HasForeignKey(h => h.ChangedByStaffId)
            .OnDelete(DeleteBehavior.SetNull);

        // AppConfig — singleton row
        modelBuilder.Entity<AppConfig>()
            .HasKey(c => c.Id);
        modelBuilder.Entity<AppConfig>()
            .HasData(new AppConfig { Id = 1, EventTitle = "IsDB Annual Meetings 2025", MinimumLeadTimeHours = 2, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

        // DepartureRequest → HotelOption
        modelBuilder.Entity<DepartureRequest>()
            .HasOne(r => r.HotelOption)
            .WithMany()
            .HasForeignKey(r => r.HotelOptionId)
            .OnDelete(DeleteBehavior.Restrict);

        // DepartureRequest → PickupDayOption
        modelBuilder.Entity<DepartureRequest>()
            .HasOne(r => r.PickupDayOption)
            .WithMany()
            .HasForeignKey(r => r.PickupDayOptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // DepartureRequest → PickupHourOption
        modelBuilder.Entity<DepartureRequest>()
            .HasOne(r => r.PickupHourOption)
            .WithMany()
            .HasForeignKey(r => r.PickupHourOptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // DepartureRequest — unique index on Email
        modelBuilder.Entity<DepartureRequest>()
            .HasIndex(r => r.Email)
            .IsUnique();

        // Notification → CreatedByStaff
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.CreatedByStaff)
            .WithMany()
            .HasForeignKey(n => n.CreatedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        // NotificationRead composite PK
        modelBuilder.Entity<NotificationRead>()
            .HasKey(r => new { r.NotificationId, r.StaffUserId });

        modelBuilder.Entity<NotificationRead>()
            .HasOne(r => r.Notification)
            .WithMany(n => n.Reads)
            .HasForeignKey(r => r.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NotificationRead>()
            .HasOne(r => r.StaffUser)
            .WithMany()
            .HasForeignKey(r => r.StaffUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // NotificationTemplate — unique index on EventKey
        modelBuilder.Entity<NotificationTemplate>()
            .HasIndex(t => t.EventKey)
            .IsUnique();

        // CarClassRule → CarClass (many-to-one)
        modelBuilder.Entity<CarClassRule>()
            .HasOne(r => r.CarClass)
            .WithMany()
            .HasForeignKey(r => r.CarClassId)
            .OnDelete(DeleteBehavior.Cascade);

        // SystemLogs indexes
        modelBuilder.Entity<SystemLog>()
            .HasIndex(l => l.OccurredAt)
            .IsDescending();
        
        modelBuilder.Entity<SystemLog>()
            .HasIndex(l => new { l.Severity, l.OccurredAt })
            .IsDescending(false, true);
            
        modelBuilder.Entity<SystemLog>()
            .HasIndex(l => new { l.Module, l.OccurredAt })
            .IsDescending(false, true);

        modelBuilder.Entity<SystemLog>()
            .HasOne(l => l.StaffUser)
            .WithMany()
            .HasForeignKey(l => l.StaffUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // CarClassRule — unique index on RegistrationTypeName
        modelBuilder.Entity<CarClassRule>()
            .HasIndex(r => r.RegistrationTypeName)
            .IsUnique();

        // TravelBookingHistory -> TravelBooking
        modelBuilder.Entity<TravelBookingHistory>()
            .HasOne(h => h.TravelBooking)
            .WithMany(tb => tb.History)
            .HasForeignKey(h => h.TravelBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // VehicleStatusHistory → Vehicle (many-to-one)
        modelBuilder.Entity<VehicleStatusHistory>()
            .HasOne(h => h.Vehicle)
            .WithMany(v => v.StatusHistory)
            .HasForeignKey(h => h.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        // VehicleStatusHistory → StaffUser (optional)
        modelBuilder.Entity<VehicleStatusHistory>()
            .HasOne(h => h.ChangedByStaff)
            .WithMany()
            .HasForeignKey(h => h.ChangedByStaffId)
            .OnDelete(DeleteBehavior.SetNull);

        // SyncAlert → Guest (optional, no cascade to keep alerts after guest deletion)
        modelBuilder.Entity<SyncAlert>()
            .HasOne(a => a.Guest)
            .WithMany()
            .HasForeignKey(a => a.GuestId)
            .OnDelete(DeleteBehavior.SetNull);

        // SyncAlert → Vehicle (optional)
        modelBuilder.Entity<SyncAlert>()
            .HasOne(a => a.Vehicle)
            .WithMany()
            .HasForeignKey(a => a.VehicleId)
            .OnDelete(DeleteBehavior.SetNull);

        // SyncAlert indexes
        modelBuilder.Entity<SyncAlert>()
            .HasIndex(a => a.DetectedAt)
            .IsDescending();

        modelBuilder.Entity<SyncAlert>()
            .HasIndex(a => new { a.IsResolved, a.DetectedAt })
            .IsDescending(false, true);

        // TravelBooking -> Flight (Many-to-One)
        modelBuilder.Entity<TravelBooking>()
            .HasOne(tb => tb.Flight)
            .WithMany(f => f.TravelBookings)
            .HasForeignKey(tb => tb.FlightId)
            .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete of Flight if bookings exist

        // DropOffTrip → Guest (many-to-one, no cascade to preserve log after guest deletion)
        modelBuilder.Entity<DropOffTrip>()
            .HasOne(d => d.Guest)
            .WithMany()
            .HasForeignKey(d => d.GuestId)
            .OnDelete(DeleteBehavior.Restrict);

        // DropOffTrip → Vehicle (many-to-one, no cascade)
        modelBuilder.Entity<DropOffTrip>()
            .HasOne(d => d.Vehicle)
            .WithMany()
            .HasForeignKey(d => d.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // DropOffTrip → Driver (optional snapshot)
        modelBuilder.Entity<DropOffTrip>()
            .HasOne(d => d.Driver)
            .WithMany()
            .HasForeignKey(d => d.DriverId)
            .OnDelete(DeleteBehavior.SetNull);

        // DropOffTrip → LoggedByStaff
        modelBuilder.Entity<DropOffTrip>()
            .HasOne(d => d.LoggedByStaff)
            .WithMany()
            .HasForeignKey(d => d.LoggedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        // DropOffTrip indexes
        modelBuilder.Entity<DropOffTrip>()
            .HasIndex(d => d.LoggedAt)
            .IsDescending();

        modelBuilder.Entity<DropOffTrip>()
            .HasIndex(d => new { d.Status, d.LoggedAt })
            .IsDescending(false, true);
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
