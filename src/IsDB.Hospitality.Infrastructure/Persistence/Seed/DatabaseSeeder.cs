using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IsDB.Hospitality.Infrastructure.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext context, ILogger logger)
    {
        try
        {
            await context.Database.MigrateAsync();
            await SeedStaffUsersAsync(context);
            await SeedChecklistItemsAsync(context);
            await SeedVehiclesAsync(context);
            logger.LogInformation("Database seeded successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private static async Task SeedStaffUsersAsync(AppDbContext context)
    {
        if (await context.StaffUsers.AnyAsync()) return;

        var users = new List<StaffUser>
        {
            new() {
                Id = Guid.NewGuid(),
                Email = "airport@isdb.org",
                FullName = "Hattan Baghdadi",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("IsDB@2026!"),
                Role = UserRole.AirportReceptionSpecialist,
                IsActive = true
            },
            new() {
                Id = Guid.NewGuid(),
                Email = "transport@isdb.org",
                FullName = "Nawwaf Al-Zahrani",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("IsDB@2026!"),
                Role = UserRole.TransportationSpecialist,
                IsActive = true
            },
            new() {
                Id = Guid.NewGuid(),
                Email = "hotel@isdb.org",
                FullName = "Hussain Al-Attas",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("IsDB@2026!"),
                Role = UserRole.AccommodationSpecialist,
                IsActive = true
            },
            new() {
                Id = Guid.NewGuid(),
                Email = "controlroom@isdb.org",
                FullName = "Mahieddine Hamdani",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("IsDB@2026!"),
                Role = UserRole.ControlRoom,
                IsActive = true
            },
            new() {
                Id = Guid.NewGuid(),
                Email = "admin@isdb.org",
                FullName = "System Administrator",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("IsDB@Admin2026!"),
                Role = UserRole.Administrator,
                IsActive = true
            }
        };

        await context.StaffUsers.AddRangeAsync(users);
        await context.SaveChangesAsync();
    }

    private static async Task SeedChecklistItemsAsync(AppDbContext context)
    {
        if (await context.ChecklistItems.AnyAsync()) return;

        var items = new List<ChecklistItem>
        {
            // Arrival checklist
            new() { Name = "Arrived at Airport", Order = 1, Type = ChecklistItemType.ArrivalAirport, IsRequired = true },
            new() { Name = "Passed Passport Control", Order = 2, Type = ChecklistItemType.ArrivalAirport, IsRequired = true },
            new() { Name = "Luggage Received", Order = 3, Type = ChecklistItemType.ArrivalAirport, IsRequired = true },
            new() { Name = "Received by Embassy Team", Order = 4, Type = ChecklistItemType.ArrivalAirport, IsRequired = true },
            // Departure checklist
            new() { Name = "Checked Out of Hotel", Order = 1, Type = ChecklistItemType.Departure, IsRequired = true },
            new() { Name = "Vehicle Assigned", Order = 2, Type = ChecklistItemType.Departure, IsRequired = true },
            new() { Name = "Departed to Airport", Order = 3, Type = ChecklistItemType.Departure, IsRequired = true },
            new() { Name = "Checked In at Airport", Order = 4, Type = ChecklistItemType.Departure, IsRequired = false },
        };

        await context.ChecklistItems.AddRangeAsync(items);
        await context.SaveChangesAsync();
    }

    private static async Task SeedVehiclesAsync(AppDbContext context)
    {
        if (await context.Vehicles.AnyAsync()) return;

        var vehicles = new List<Vehicle>
        {
            new() { Make = "Mercedes", Model = "S-Class", LicensePlate = "DXB-001", Color = "Black", DriverName = "Ahmed Hassan", DriverPhone = "+971-50-123-4567", BarcodeValue = "VH-DXB-001", Status = VehicleStatus.Available },
            new() { Make = "Mercedes", Model = "S-Class", LicensePlate = "DXB-002", Color = "Black", DriverName = "Mohammed Al-Rashid", DriverPhone = "+971-50-234-5678", BarcodeValue = "VH-DXB-002", Status = VehicleStatus.Available },
            new() { Make = "BMW", Model = "7 Series", LicensePlate = "DXB-003", Color = "Silver", DriverName = "Khalid Al-Mansouri", DriverPhone = "+971-50-345-6789", BarcodeValue = "VH-DXB-003", Status = VehicleStatus.Available },
            new() { Make = "Toyota", Model = "Land Cruiser", LicensePlate = "DXB-004", Color = "White", DriverName = "Abdullah Al-Sayed", DriverPhone = "+971-50-456-7890", BarcodeValue = "VH-DXB-004", Status = VehicleStatus.Available },
            new() { Make = "Toyota", Model = "Land Cruiser", LicensePlate = "DXB-005", Color = "White", DriverName = "Omar Al-Farsi", DriverPhone = "+971-50-567-8901", BarcodeValue = "VH-DXB-005", Status = VehicleStatus.Available },
            new() { Make = "Lexus", Model = "LX 600", LicensePlate = "DXB-006", Color = "Black", DriverName = "Hassan Al-Zaabi", DriverPhone = "+971-50-678-9012", BarcodeValue = "VH-DXB-006", Status = VehicleStatus.Available },
        };

        await context.Vehicles.AddRangeAsync(vehicles);
        await context.SaveChangesAsync();
    }
}
