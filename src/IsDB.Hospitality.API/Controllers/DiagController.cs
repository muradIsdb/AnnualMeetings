// TEMPORARY DIAGNOSTIC ENDPOINT — REMOVE AFTER USE
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[ApiController]
[Route("api/diag")]
public class DiagController : ControllerBase
{
    private readonly AppDbContext _db;
    public DiagController(AppDbContext db) => _db = db;

    /// <summary>
    /// Temporary diagnostic endpoint — returns EventCode config and guest distribution.
    /// Remove after diagnosis is complete.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        // 1. EventsAirConfigs
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync(ct);

        // 2. Guest EventCode distribution
        var guestDist = await _db.Guests
            .GroupBy(g => g.EventCode)
            .Select(g => new { EventCode = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        // 3. Total guests
        var totalGuests = await _db.Guests.CountAsync(ct);

        // 4. Sample guests (last 5 created)
        var sampleGuests = await _db.Guests
            .OrderByDescending(g => g.CreatedAt)
            .Take(5)
            .Select(g => new { g.Id, g.FullName, g.EventCode, g.InboundStatus, g.CreatedAt })
            .ToListAsync(ct);

        // 5. Applied migrations — alias must be "Value" for SqlQueryRaw<string>
        var migrations = await _db.Database
            .SqlQueryRaw<string>(@"SELECT ""MigrationId"" AS ""Value"" FROM ""__EFMigrationsHistory"" ORDER BY ""MigrationId""")
            .ToListAsync(ct);

        // 6. Check VehicleTypeValue column exists — alias must be "Value" for SqlQueryRaw<int>
        var colExists = await _db.Database
            .SqlQueryRaw<int>(@"
                SELECT COUNT(*)::int AS ""Value""
                FROM information_schema.columns
                WHERE table_name = 'Guests' AND column_name = 'VehicleTypeValue'")
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            eventsAirConfig = config == null ? null : new
            {
                config.EventCode,
                config.LastSyncAt,
                config.LastSyncStatus,
                config.LastSyncRecordsCount,
                config.IsActive
            },
            totalGuests,
            guestEventCodeDistribution = guestDist,
            sampleGuests,
            vehicleTypeValueColumnExists = colExists > 0,
            appliedMigrations = migrations
        });
    }

    /// <summary>
    /// Temporary fix endpoint — stamps missing migrations and updates guest EventCodes.
    /// Remove after fix is confirmed.
    /// </summary>
    [HttpPost("fix")]
    public async Task<IActionResult> Fix(CancellationToken ct)
    {
        var results = new List<string>();

        // 1. Get active EventCode from config
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync(ct);
        if (config == null)
            return BadRequest(new { error = "No EventsAirConfig found in database." });

        var eventCode = config.EventCode;

        // 2. Stamp missing migrations as applied
        var missingMigrations = new[]
        {
            "20260508210000_AddOAuthScopeToEventsAirConfig",
            "20260508220000_AddPerformanceIndexes",
            "20260515100000_AddVehicleStatusHistory",
            "20260601100000_MakeVehicleLicensePlateOptional",
            "20260603100000_AddVehicleTypeValueToGuest",
            "20260604100000_AddEventCodeToNotificationsAndDepartureRequests",
            "20260604300000_NormaliseFlightNumbers"
        };

        foreach (var migrationId in missingMigrations)
        {
            var rows = await _db.Database.ExecuteSqlRawAsync(
                $@"INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                   VALUES ('{migrationId}', '9.0.0')
                   ON CONFLICT DO NOTHING", ct);
            results.Add($"Migration {migrationId}: {(rows > 0 ? "stamped" : "already existed")}");
        }

        // 3. Update guests with null EventCode to the active EventCode
        var updatedGuests = await _db.Database.ExecuteSqlRawAsync(
            $@"UPDATE ""Guests"" SET ""EventCode"" = '{eventCode}' WHERE ""EventCode"" IS NULL", ct);
        results.Add($"Guests updated with EventCode '{eventCode}': {updatedGuests} rows");

        return Ok(new
        {
            activeEventCode = eventCode,
            actions = results
        });
    }
}
