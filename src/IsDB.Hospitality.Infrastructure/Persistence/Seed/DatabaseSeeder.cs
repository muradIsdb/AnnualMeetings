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
            (Email: "vendor@isdb.org",    Name: "Vendor User",           Role: UserRole.Vendor),
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
            new() { Id = Guid.NewGuid(), Name = "VVIP Luxury",       Description = "Reserved for VVIP guests — heads of state, ministers, and senior dignitaries",           Color = "#7C3AED", SortOrder = 1 },
            new() { Id = Guid.NewGuid(), Name = "Executive Luxury",  Description = "Premium luxury sedans for senior executives and VIP guests",                          Color = "#B45309", SortOrder = 2 },
            new() { Id = Guid.NewGuid(), Name = "Executive SUV",     Description = "Executive-class SUVs for senior officials and delegations",                          Color = "#0E7490", SortOrder = 3 },
            new() { Id = Guid.NewGuid(), Name = "Board & DG Class",  Description = "Dedicated vehicles for Board of Governors members and the Director General",          Color = "#DC2626", SortOrder = 4 },
            new() { Id = Guid.NewGuid(), Name = "AMOC",              Description = "AMOC-designated vehicles for the organizing committee",                               Color = "#0369A1", SortOrder = 5 },
            new() { Id = Guid.NewGuid(), Name = "General Pool",      Description = "General pool vehicles for standard participants and staff",                          Color = "#059669", SortOrder = 6 },
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
        const string vehicleTypeGuid = "5f6b0e9e-7d1c-4f91-affc-ecbe95cef678";
        const string liaisonOfficerGuid = "f4d27526-7af9-5ed4-ebe1-3a1d4e2e471d";

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

        // Seed the Vehicle Types field mapping if it doesn't already exist
        var vehicleTypeExists = await context.SyncFieldMappings.AnyAsync(m => m.EventsAirFieldGuid == vehicleTypeGuid);
        if (!vehicleTypeExists)
        {
            context.SyncFieldMappings.Add(new SyncFieldMapping
            {
                Id = Guid.NewGuid(),
                DisplayName = "Vehicle Types",
                EventsAirFieldGuid = vehicleTypeGuid,
                FieldRole = "VehicleType",
                Description = "Preferred vehicle type from EventsAir (e.g. Hyundai Elantra, Toyota Land Cruiser). Stored for display only.",
                SortOrder = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Seed the Liaison Officer field mapping if it doesn't already exist
        var liaisonOfficerExists = await context.SyncFieldMappings.AnyAsync(m => m.EventsAirFieldGuid == liaisonOfficerGuid);
        if (!liaisonOfficerExists)
        {
            context.SyncFieldMappings.Add(new SyncFieldMapping
            {
                Id = Guid.NewGuid(),
                DisplayName = "Liaison Officer",
                EventsAirFieldGuid = liaisonOfficerGuid,
                FieldRole = "LiaisonOfficer",
                Description = "Boolean flag indicating whether the guest is entitled to a dedicated liaison officer. Fetched from EventsAir checkbox field.",
                SortOrder = 4,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }


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
        // Npgsql maps C# bool to PostgreSQL 'boolean', which rejects integer literals.
        // We convert known boolean columns by replacing their positional integer values.
        pgSql = ConvertBooleanColumnsToPostgres(pgSql);
        return pgSql;
    }

    /// <summary>
    /// Replaces integer 0/1 values for known boolean columns with PostgreSQL true/false literals.
    /// Works by parsing the column list from the INSERT statement and replacing values at the
    /// matching positions in the VALUES clause.
    /// </summary>
    private static string ConvertBooleanColumnsToPostgres(string sql)
    {
        // Known boolean columns across all seeded tables
        var booleanColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "IsSelectedForSync", "IsFromEventsAir", "AutoSyncEnabled",
            "SyncOnStartup", "IsActive"
        };

        // Extract column list: INSERT INTO "Table" ("Col1", "Col2", ...) VALUES (...)
        var colMatch = System.Text.RegularExpressions.Regex.Match(
            sql, @"\(([^)]+)\)\s+VALUES\s+\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!colMatch.Success) return sql;

        var columns = colMatch.Groups[1].Value
            .Split(',')
            .Select(c => c.Trim().Trim('"'))
            .ToList();

        // Find boolean column positions (0-indexed)
        var boolPositions = new HashSet<int>();
        for (int i = 0; i < columns.Count; i++)
            if (booleanColumns.Contains(columns[i]))
                boolPositions.Add(i);

        if (boolPositions.Count == 0) return sql;

        // Extract VALUES clause and replace 0/1 at bool positions
        var valuesStart = sql.IndexOf("VALUES", StringComparison.OrdinalIgnoreCase);
        var prefix = sql[..valuesStart];
        var valuesPart = sql[valuesStart..];

        // Parse the values tuple: VALUES ('a', 'b', 0, 1, ...)
        var tupleMatch = System.Text.RegularExpressions.Regex.Match(
            valuesPart, @"\((.+)\)", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (!tupleMatch.Success) return sql;

        // Tokenize values respecting quoted strings
        var rawValues = tupleMatch.Groups[1].Value;
        var tokens = new List<string>();
        int depth = 0; var current = new System.Text.StringBuilder(); bool inQuote = false;
        foreach (char ch in rawValues)
        {
            if (ch == '\'' && !inQuote) { inQuote = true; current.Append(ch); }
            else if (ch == '\'' && inQuote) { inQuote = false; current.Append(ch); }
            else if (ch == ',' && !inQuote && depth == 0)
            {
                tokens.Add(current.ToString().Trim());
                current.Clear();
            }
            else { current.Append(ch); }
        }
        if (current.Length > 0) tokens.Add(current.ToString().Trim());

        // Replace 0/1 at boolean positions
        for (int i = 0; i < tokens.Count && i < columns.Count; i++)
        {
            if (!boolPositions.Contains(i)) continue;
            if (tokens[i] == "0") tokens[i] = "false";
            else if (tokens[i] == "1") tokens[i] = "true";
        }

        // Reconstruct the SQL
        var suffix = valuesPart[(tupleMatch.Index + tupleMatch.Length)..];
        return prefix + "VALUES (" + string.Join(", ", tokens) + ")" + suffix;
    }
}
