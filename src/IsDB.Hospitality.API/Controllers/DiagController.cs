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
}
