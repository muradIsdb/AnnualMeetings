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
        await SeedStaffUsersAsync(context);
        await SeedChecklistItemsAsync(context);
        await SeedCarClassesAsync(context);
        await SeedVehiclesAsync(context);
        await SeedSyncFieldMappingsAsync(context);
        await SeedPagePermissionsAsync(context);
            await ApplyProductionSeedAsync(context, logger);
            logger.LogInformation("Database seeded successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    /// <summary>
    /// Applies the production_seed.sql file which contains configuration data
    /// (EventsAir config, registration types, sync field values, etc.) exported
    /// from the production database. Uses INSERT OR IGNORE so it is safe to run
    /// on every startup — existing rows are never overwritten.
    /// </summary>
    private static async Task ApplyProductionSeedAsync(AppDbContext context, ILogger logger)
    {
        // The seed file is copied to the output directory under Persistence/Seed/
        // (due to CopyToOutputDirectory in the .csproj)
        var assemblyDir = AppContext.BaseDirectory;
        var seedFile = Path.Combine(assemblyDir, "Persistence", "Seed", "production_seed.sql");

        // Fall back: flat root of the output directory (in case of publish layout)
        if (!File.Exists(seedFile))
            seedFile = Path.Combine(assemblyDir, "production_seed.sql");

        if (!File.Exists(seedFile))
        {
            logger.LogWarning("production_seed.sql not found at {Path}. Skipping production seed.", seedFile);
            return;
        }

        logger.LogInformation("Applying production seed from {Path}", seedFile);

        var sql = await File.ReadAllTextAsync(seedFile);

        // Detect if we're running on PostgreSQL
        var isPostgres = context.Database.ProviderName?.Contains("Npgsql") == true;

        // Split on newlines and execute each non-empty, non-comment INSERT statement
        var statements = sql
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
            .Select(l => isPostgres ? ConvertToPostgresUpsert(l) : l)
            .ToList();

        int applied = 0;
        foreach (var stmt in statements)
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync(stmt);
                applied++;
            }
            catch (Exception ex)
            {
                // Log but continue — individual statement failures should not abort the whole seed
                logger.LogWarning(ex, "Production seed statement failed (may already exist): {Stmt}", stmt[..Math.Min(80, stmt.Length)]);
            }
        }

        logger.LogInformation("Production seed applied: {Count}/{Total} statements executed.", applied, statements.Count);

        // Always ensure TokenEndpoint is the correct Microsoft Azure AD URL (fix stale rows)
        const string correctTokenEndpoint = "https://login.microsoftonline.com/dff76352-1ded-46e8-96a4-1a83718b2d3a/oauth2/v2.0/token";
        var updateSql = isPostgres
            ? $"UPDATE \"EventsAirConfigs\" SET \"TokenEndpoint\" = '{correctTokenEndpoint}' WHERE \"TokenEndpoint\" != '{correctTokenEndpoint}'"
            : $"UPDATE EventsAirConfigs SET TokenEndpoint = '{correctTokenEndpoint}' WHERE TokenEndpoint != '{correctTokenEndpoint}'";
        await context.Database.ExecuteSqlRawAsync(updateSql);

        // Override the ClientSecret from environment variable if set.
        // This allows the secret to be stored securely in Railway env vars
        // without being committed to the repository.
        var envSecret = Environment.GetEnvironmentVariable("EVENTSAIR_CLIENT_SECRET");
        if (!string.IsNullOrWhiteSpace(envSecret))
        {
            var cfg = await context.EventsAirConfigs.FirstOrDefaultAsync();
            if (cfg != null && cfg.ClientSecret != envSecret)
            {
                cfg.ClientSecret = envSecret;
                await context.SaveChangesAsync();
                logger.LogInformation("EventsAir ClientSecret updated from EVENTSAIR_CLIENT_SECRET environment variable.");
            }
        }
    }

    private static async Task SeedStaffUsersAsync(AppDbContext context)
    {
        if (await context.StaffUsers.AnyAsync()) return;

        var seedData = new[]
        {
            (Email: "airport@isdb.org",  Name: "Hattan Baghdadi",      Role: UserRole.Airport),
            (Email: "transport@isdb.org", Name: "Nawwaf Al-Zahrani",    Role: UserRole.Transport),
            (Email: "hotel@isdb.org",     Name: "Hussain Al-Attas",     Role: UserRole.Hotel),
            (Email: "controlroom@isdb.org",Name: "Mahieddine Hamdani",  Role: UserRole.ControlRoom),
            (Email: "admin@isdb.org",     Name: "System Administrator", Role: UserRole.Admin),
        };

        var users = seedData.Select(d =>
        {
            var id = Guid.NewGuid();
            return new StaffUser
            {
                Id = id,
                Email = d.Email,
                FullName = d.Name,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123"),
                Role = d.Role,
                IsActive = true,
                Roles = new List<StaffUserRole>
                {
                    new() { StaffUserId = id, Role = d.Role, AssignedAt = DateTime.UtcNow }
                }
            };
        }).ToList();

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

    private static async Task SeedCarClassesAsync(AppDbContext context)
    {
        if (await context.CarClasses.AnyAsync()) return;

        var classes = new List<CarClass>
        {
            new() { Id = Guid.NewGuid(), Name = "Luxury Car",   Description = "High-end luxury vehicles for VIP and VVIP guests",     Color = "#7C3AED", SortOrder = 1 },
            new() { Id = Guid.NewGuid(), Name = "AMOC Car",     Description = "AMOC-designated vehicles for organizing committee",    Color = "#0369A1", SortOrder = 2 },
            new() { Id = Guid.NewGuid(), Name = "Standard Car", Description = "Standard vehicles for general participants",           Color = "#059669", SortOrder = 3 },
        };

        await context.CarClasses.AddRangeAsync(classes);
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

    private static async Task SeedSyncFieldMappingsAsync(AppDbContext context)
    {
        const string rankGuid = "3d96b87e-87b0-145e-5f45-3a17bafe26d4";
        const string dedicatedCarGuid = "d6b74b23-c8b6-d044-5d86-3a17bafe27de";

        // Seed the Rank field mapping if it doesn't already exist
        var rankExists = await context.SyncFieldMappings.AnyAsync(m => m.EventsAirFieldGuid == rankGuid);
        if (!rankExists)
        {
            context.SyncFieldMappings.Add(new SyncFieldMapping
            {
                Id = Guid.NewGuid(),
                DisplayName = "Rank",
                EventsAirFieldGuid = rankGuid,
                FieldRole = "Rank",
                Description = "Participant rank from EventsAir. Stored for display only — not used for filtering.",
                SortOrder = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            // Migrate existing Rank mapping to FieldRole="Rank" (display only)
            var rankMapping = await context.SyncFieldMappings.FirstAsync(m => m.EventsAirFieldGuid == rankGuid);
            if (rankMapping.FieldRole != "Rank")
            {
                rankMapping.FieldRole = "Rank";
                rankMapping.Description = "Participant rank from EventsAir. Stored for display only — not used for filtering.";
                rankMapping.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Seed the Dedicated Car field mapping if it doesn't already exist
        var dedicatedCarExists = await context.SyncFieldMappings.AnyAsync(m => m.EventsAirFieldGuid == dedicatedCarGuid);
        if (!dedicatedCarExists)
        {
            context.SyncFieldMappings.Add(new SyncFieldMapping
            {
                Id = Guid.NewGuid(),
                DisplayName = "Dedicated Car",
                EventsAirFieldGuid = dedicatedCarGuid,
                FieldRole = "DedicatedCar",
                Description = "Primary sync filter. Guests with a value for this field are included; others are deactivated (unless they have an active vehicle assignment).",
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds default page permissions for all non-Admin roles.
    /// Only runs when the PagePermissions table is empty (idempotent).
    /// Admin role always has implicit full access and is NOT stored here.
    /// </summary>
    private static async Task SeedPagePermissionsAsync(AppDbContext context)
    {
        if (await context.PagePermissions.AnyAsync()) return;

        // Define all pages and their default allowed roles
        var defaults = new List<(UserRole Role, string PageId)>
        {
            // Airport role
            (UserRole.Airport,      "airport.dashboard"),

            // Transport role
            (UserRole.Transport,    "transport.dashboard"),
            (UserRole.Transport,    "transport.departure_stats"),
            (UserRole.Transport,    "fleet.management"),

            // Hotel role
            (UserRole.Hotel,        "hotel.dashboard"),
            (UserRole.Hotel,        "hotel.arrivals"),
            (UserRole.Hotel,        "hotel.guests"),
            (UserRole.Hotel,        "hotel.management"),

            // ControlRoom role
            (UserRole.ControlRoom,  "airport.dashboard"),
            (UserRole.ControlRoom,  "transport.dashboard"),
            (UserRole.ControlRoom,  "transport.departure_stats"),
            (UserRole.ControlRoom,  "controlroom.dashboard"),
            (UserRole.ControlRoom,  "hotel.dashboard"),
            (UserRole.ControlRoom,  "hotel.arrivals"),
            (UserRole.ControlRoom,  "hotel.guests"),
            (UserRole.ControlRoom,  "hotel.management"),

            // Liaison role
            (UserRole.Liaison,      "liaison.dashboard"),
            (UserRole.Liaison,      "liaison.guests"),
        };

        var permissions = defaults.Select(d => new IsDB.Hospitality.Domain.Entities.PagePermission
        {
            Role = d.Role,
            PageId = d.PageId,
            IsGranted = true
        }).ToList();

        await context.PagePermissions.AddRangeAsync(permissions);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Converts a SQLite "INSERT OR IGNORE INTO ..." statement to a PostgreSQL
    /// "INSERT INTO ... ON CONFLICT DO NOTHING" statement.
    /// </summary>
    private static string ConvertToPostgresUpsert(string sqliteInsert)
    {
        // Replace "INSERT OR IGNORE INTO" with "INSERT INTO"
        var pgSql = sqliteInsert.Replace("INSERT OR IGNORE INTO", "INSERT INTO");

        // Append ON CONFLICT DO NOTHING before the trailing semicolon
        if (pgSql.TrimEnd().EndsWith(";"))
        {
            pgSql = pgSql.TrimEnd().TrimEnd(';') + " ON CONFLICT DO NOTHING;";
        }
        else
        {
            pgSql += " ON CONFLICT DO NOTHING";
        }

        // PostgreSQL uses TRUE/FALSE instead of 1/0 for booleans.
        // However, EF Core maps bool columns to integer in the schema,
        // so numeric values (0/1) work fine in PostgreSQL integer columns.

        return pgSql;
    }
}
