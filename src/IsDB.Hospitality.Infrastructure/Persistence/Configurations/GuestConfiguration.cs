using IsDB.Hospitality.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsDB.Hospitality.Infrastructure.Persistence.Configurations;

public class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.EventsAirContactId).IsRequired().HasMaxLength(100);
        builder.HasIndex(g => g.EventsAirContactId).IsUnique();
        builder.Property(g => g.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(g => g.LastName).IsRequired().HasMaxLength(100);
        builder.Property(g => g.Title).HasMaxLength(50);
        builder.Property(g => g.Organization).HasMaxLength(200);
        builder.Property(g => g.Designation).HasMaxLength(200);
        builder.Property(g => g.Nationality).HasMaxLength(100);
        builder.Property(g => g.PassportNumber).HasMaxLength(50);
        builder.Property(g => g.PhotoUrl).HasMaxLength(500);
        builder.Property(g => g.MobileNumber).HasMaxLength(50);
        builder.Property(g => g.Email).HasMaxLength(200);
        builder.Property(g => g.GroupCode).HasMaxLength(50);
        builder.Property(g => g.RoomNumber).HasMaxLength(20);
        builder.Property(g => g.HotelName).HasMaxLength(200);
        builder.Property(g => g.SpecialRequirements).HasMaxLength(1000);
        builder.Property(g => g.Notes).HasMaxLength(2000);

        builder.HasMany(g => g.Flights).WithOne(f => f.Guest).HasForeignKey(f => f.GuestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(g => g.ChecklistCompletions).WithOne(cc => cc.Guest).HasForeignKey(cc => cc.GuestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(g => g.VehicleAssignments).WithOne(va => va.Guest).HasForeignKey(va => va.GuestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(g => g.Alerts).WithOne(a => a.Guest).HasForeignKey(a => a.GuestId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(g => g.DepartureRequests).WithOne(dr => dr.Guest).HasForeignKey(dr => dr.GuestId).OnDelete(DeleteBehavior.SetNull);

        builder.Ignore(g => g.FullName);
    }
}

public class FlightConfiguration : IEntityTypeConfiguration<Flight>
{
    public void Configure(EntityTypeBuilder<Flight> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.FlightNumber).IsRequired().HasMaxLength(20);
        builder.Property(f => f.Airline).HasMaxLength(100);
        builder.Property(f => f.DepartureAirportCode).HasMaxLength(10);
        builder.Property(f => f.ArrivalAirportCode).HasMaxLength(10);
        builder.Property(f => f.Terminal).HasMaxLength(20);
        builder.Property(f => f.Gate).HasMaxLength(20);
        builder.Property(f => f.DelayReason).HasMaxLength(500);
    }
}

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.LicensePlate).IsRequired().HasMaxLength(20);
        builder.HasIndex(v => v.LicensePlate).IsUnique();
        builder.Property(v => v.Make).IsRequired().HasMaxLength(50);
        builder.Property(v => v.Model).IsRequired().HasMaxLength(50);
        builder.Property(v => v.Color).HasMaxLength(30);
        builder.Property(v => v.DriverName).HasMaxLength(100);
        builder.Property(v => v.DriverPhone).HasMaxLength(50);
        builder.Property(v => v.BarcodeValue).HasMaxLength(100);
        builder.HasIndex(v => v.BarcodeValue);
    }
}

public class VehicleAssignmentConfiguration : IEntityTypeConfiguration<VehicleAssignment>
{
    public void Configure(EntityTypeBuilder<VehicleAssignment> builder)
    {
        builder.HasKey(va => va.Id);
        builder.Property(va => va.Notes).HasMaxLength(500);
        builder.Property(va => va.EstimatedArrivalTime).HasMaxLength(20);
        builder.HasOne(va => va.Vehicle).WithMany(v => v.Assignments).HasForeignKey(va => va.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(va => va.AssignedByStaff).WithMany().HasForeignKey(va => va.AssignedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.HasKey(ci => ci.Id);
        builder.Property(ci => ci.Name).IsRequired().HasMaxLength(100);
        builder.Property(ci => ci.Description).HasMaxLength(500);
    }
}

public class ChecklistCompletionConfiguration : IEntityTypeConfiguration<ChecklistCompletion>
{
    public void Configure(EntityTypeBuilder<ChecklistCompletion> builder)
    {
        builder.HasKey(cc => cc.Id);
        builder.HasIndex(cc => new { cc.GuestId, cc.ChecklistItemId }).IsUnique();
        builder.Property(cc => cc.Notes).HasMaxLength(500);
        builder.HasOne(cc => cc.ChecklistItem).WithMany(ci => ci.Completions).HasForeignKey(cc => cc.ChecklistItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(cc => cc.CompletedByStaff).WithMany().HasForeignKey(cc => cc.CompletedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Title).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Message).IsRequired().HasMaxLength(1000);
        builder.Property(a => a.ResolutionNotes).HasMaxLength(500);
        builder.HasOne(a => a.ResolvedByStaff).WithMany().HasForeignKey(a => a.ResolvedByStaffId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class StaffUserConfiguration : IEntityTypeConfiguration<StaffUser>
{
    public void Configure(EntityTypeBuilder<StaffUser> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Email).IsRequired().HasMaxLength(200);
        builder.HasIndex(s => s.Email).IsUnique();
        builder.Property(s => s.FullName).IsRequired().HasMaxLength(200);
        builder.Property(s => s.PasswordHash).IsRequired();
        builder.Property(s => s.RefreshToken).HasMaxLength(500);
    }
}

public class DepartureRequestConfiguration : IEntityTypeConfiguration<DepartureRequest>
{
    public void Configure(EntityTypeBuilder<DepartureRequest> builder)
    {
        builder.HasKey(dr => dr.Id);
        builder.Property(dr => dr.GuestName).IsRequired().HasMaxLength(200);
        builder.Property(dr => dr.GuestEmail).HasMaxLength(200);
        builder.Property(dr => dr.GuestPhone).HasMaxLength(50);
        builder.Property(dr => dr.HotelName).HasMaxLength(200);
        builder.Property(dr => dr.RoomNumber).HasMaxLength(20);
        builder.Property(dr => dr.DestinationAirport).IsRequired().HasMaxLength(100);
        builder.Property(dr => dr.FlightNumber).HasMaxLength(20);
        builder.Property(dr => dr.SpecialRequirements).HasMaxLength(1000);
        builder.HasOne(dr => dr.ProcessedByStaff).WithMany().HasForeignKey(dr => dr.ProcessedByStaffId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class SyncRecordConfiguration : IEntityTypeConfiguration<SyncRecord>
{
    public void Configure(EntityTypeBuilder<SyncRecord> builder)
    {
        builder.HasKey(sr => sr.Id);
        builder.Property(sr => sr.ServiceName).IsRequired().HasMaxLength(100);
        builder.Property(sr => sr.ErrorMessage).HasMaxLength(2000);
        builder.Property(sr => sr.Details).HasMaxLength(4000);
    }
}
