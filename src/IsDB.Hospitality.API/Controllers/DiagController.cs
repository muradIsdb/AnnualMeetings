// TEMPORARY DIAGNOSTIC ENDPOINT — REMOVE AFTER USE
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;

namespace IsDB.Hospitality.API.Controllers;

[ApiController]
[Route("api/diag")]
public class DiagController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    public DiagController(AppDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

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
                config.LastSyncMessage,
                config.LastSyncRecordsCount,
                config.IsActive,
                config.AutoSyncEnabled,
                HasClientId = !string.IsNullOrWhiteSpace(config.ClientId),
                HasClientSecret = !string.IsNullOrWhiteSpace(config.ClientSecret),
                config.ApiBaseUrl,
                config.TokenEndpoint
            },
            totalGuests,
            guestEventCodeDistribution = guestDist,
            sampleGuests,
            vehicleTypeValueColumnExists = colExists > 0,
            appliedMigrations = migrations
        });
    }

    /// <summary>
    /// Temporary endpoint — tests EventsAir token acquisition and returns first-page contact count.
    /// Remove after diagnosis is complete.
    /// </summary>
    [HttpPost("test-sync")]
    public async Task<IActionResult> TestSync(CancellationToken ct)
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync(ct);
        if (config == null) return BadRequest(new { error = "No EventsAirConfig found." });
        if (!config.IsActive) return BadRequest(new { error = "Integration is not active." });
        if (string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.ClientSecret))
            return BadRequest(new { error = "ClientId or ClientSecret is missing." });
        if (string.IsNullOrWhiteSpace(config.EventCode))
            return BadRequest(new { error = "EventCode is missing." });

        // Read OAuthScope via raw SQL
        string oAuthScope;
        try
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT \"OAuthScope\" FROM \"EventsAirConfigs\" LIMIT 1";
            var scopeResult = await cmd.ExecuteScalarAsync(ct);
            oAuthScope = scopeResult is string s && !string.IsNullOrWhiteSpace(s)
                ? s
                : "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";
        }
        catch { oAuthScope = "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default"; }

        // Try to acquire token
        string token;
        try
        {
            var tokenEndpoint = string.IsNullOrWhiteSpace(config.TokenEndpoint)
                ? "https://eventsairprod.b2clogin.com/eventsairprod.onmicrosoft.com/oauth2/v2.0/token"
                : config.TokenEndpoint;
            var client = _httpClientFactory.CreateClient();
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = config.ClientId,
                ["client_secret"] = config.ClientSecret,
                ["scope"] = oAuthScope
            });
            var resp = await client.PostAsync(tokenEndpoint, form, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                return Ok(new { tokenAcquired = false, tokenError = body, oAuthScope, tokenEndpoint });
            token = System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("access_token").GetString() ?? "";
        }
        catch (Exception ex)
        {
            return Ok(new { tokenAcquired = false, tokenError = ex.Message, oAuthScope });
        }

        // Try a minimal GraphQL query to count contacts
        int contactCount = -1;
        string? graphqlError = null;
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var query = $"{{\"query\":\"{{ event(code: \\\"{config.EventCode}\\\") {{ contacts(first: 1) {{ totalCount }} }} }}\"}}"; 
            var resp = await client.PostAsync($"{config.ApiBaseUrl.TrimEnd('/')}/graphql",
                new StringContent(query, System.Text.Encoding.UTF8, "application/json"), ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode)
            {
                var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("event", out var evt) && evt.ValueKind != System.Text.Json.JsonValueKind.Null &&
                    evt.TryGetProperty("contacts", out var contacts) &&
                    contacts.TryGetProperty("totalCount", out var tc))
                    contactCount = tc.GetInt32();
                else
                    graphqlError = body;
            }
            else graphqlError = body;
        }
        catch (Exception ex) { graphqlError = ex.Message; }

        return Ok(new { tokenAcquired = true, oAuthScope, contactCount, graphqlError,
            eventCode = config.EventCode, apiBaseUrl = config.ApiBaseUrl });
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
