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
    /// Temporary endpoint — migrates car classes, hotels, pickup days/hours from UAT data.
    /// Deletes all vehicles/assignments first, then replaces config tables.
    /// Run ONCE on production, then remove.
    /// </summary>
    [HttpPost("migrate-from-uat")]
    public async Task<IActionResult> MigrateFromUat(CancellationToken ct)
    {
        var results = new List<string>();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            int rows;

            // Step 1: Delete vehicle assignments, status history, then vehicles
            rows = await _db.Database.ExecuteSqlRawAsync(@"DELETE FROM ""VehicleAssignments""", ct);
            results.Add($"VehicleAssignments deleted: {rows}");
            rows = await _db.Database.ExecuteSqlRawAsync(@"DELETE FROM ""VehicleStatusHistory""", ct);
            results.Add($"VehicleStatusHistory deleted: {rows}");
            rows = await _db.Database.ExecuteSqlRawAsync(@"DELETE FROM ""Vehicles""", ct);
            results.Add($"Vehicles deleted: {rows}");

            // Step 2: Replace CarClasses
            rows = await _db.Database.ExecuteSqlRawAsync(@"DELETE FROM ""CarClasses""", ct);
            results.Add($"CarClasses deleted: {rows}");
            var ccSql = new[]
            {
                "INSERT INTO \"CarClasses\" (\"Id\",\"Name\",\"ShortName\",\"Description\",\"IsActive\",\"DisplayOrder\",\"EventCode\",\"MaxPassengers\",\"VehicleType\",\"CreatedAt\",\"UpdatedAt\") VALUES ('a0946247-9df2-4aaf-96aa-11b580dcdb31','Mercedes S-Class W223','Merc-S23','Reserved for VVIP guests — heads of state, ministers, and senior dignitaries\n2021-2025',true,0,NULL,NULL,NULL,NOW(),NOW())",
                "INSERT INTO \"CarClasses\" (\"Id\",\"Name\",\"ShortName\",\"Description\",\"IsActive\",\"DisplayOrder\",\"EventCode\",\"MaxPassengers\",\"VehicleType\",\"CreatedAt\",\"UpdatedAt\") VALUES ('20ae982b-301b-45ed-ad60-d6a2ae06c51e','Mercedes S-Class W222','Merc-S22','2018-2020',true,0,NULL,NULL,NULL,NOW(),NOW())",
                "INSERT INTO \"CarClasses\" (\"Id\",\"Name\",\"ShortName\",\"Description\",\"IsActive\",\"DisplayOrder\",\"EventCode\",\"MaxPassengers\",\"VehicleType\",\"CreatedAt\",\"UpdatedAt\") VALUES ('16e9545c-4185-4652-b8d3-fa54ef751ede','Mercedes E-Class','Merc-E','Qualifying Alt. Governors (Ministers / Central Bank Govs), IsDB VPs, CEOs, AMOC Chairman, VIP.',true,0,NULL,NULL,NULL,NOW(),NOW())",
                "INSERT INTO \"CarClasses\" (\"Id\",\"Name\",\"ShortName\",\"Description\",\"IsActive\",\"DisplayOrder\",\"EventCode\",\"MaxPassengers\",\"VehicleType\",\"CreatedAt\",\"UpdatedAt\") VALUES ('41e07ad5-ca44-4553-ab91-af8cb1a8ab5a','Mercedes V-Class (SUV)','SUV','Executive-class SUVs for senior officials and delegations',true,0,NULL,NULL,NULL,NOW(),NOW())",
                "INSERT INTO \"CarClasses\" (\"Id\",\"Name\",\"ShortName\",\"Description\",\"IsActive\",\"DisplayOrder\",\"EventCode\",\"MaxPassengers\",\"VehicleType\",\"CreatedAt\",\"UpdatedAt\") VALUES ('53e5381f-25b7-4305-950f-6719cd8b4f99','Hyundai Sonata','Sonata','Board & DG Class',true,0,NULL,NULL,NULL,NOW(),NOW())",
                "INSERT INTO \"CarClasses\" (\"Id\",\"Name\",\"ShortName\",\"Description\",\"IsActive\",\"DisplayOrder\",\"EventCode\",\"MaxPassengers\",\"VehicleType\",\"CreatedAt\",\"UpdatedAt\") VALUES ('10967aa4-917b-4ad0-ada1-f0d1ff2d992c','Kia Optima','Optimal','Board & DG Class',true,0,NULL,NULL,NULL,NOW(),NOW())",
                "INSERT INTO \"CarClasses\" (\"Id\",\"Name\",\"ShortName\",\"Description\",\"IsActive\",\"DisplayOrder\",\"EventCode\",\"MaxPassengers\",\"VehicleType\",\"CreatedAt\",\"UpdatedAt\") VALUES ('a24f537f-246c-44a5-b9f1-ba771da2803a','Tayota Camry','Camry','Board & DG Class',true,0,NULL,NULL,NULL,NOW(),NOW())",
                "INSERT INTO \"CarClasses\" (\"Id\",\"Name\",\"ShortName\",\"Description\",\"IsActive\",\"DisplayOrder\",\"EventCode\",\"MaxPassengers\",\"VehicleType\",\"CreatedAt\",\"UpdatedAt\") VALUES ('099efcc4-66bc-4ea4-8392-226c37bfb7e2','Hyundair Elentra','Elentra','AMOC and General Pool',true,0,NULL,NULL,NULL,NOW(),NOW())",
                "INSERT INTO \"CarClasses\" (\"Id\",\"Name\",\"ShortName\",\"Description\",\"IsActive\",\"DisplayOrder\",\"EventCode\",\"MaxPassengers\",\"VehicleType\",\"CreatedAt\",\"UpdatedAt\") VALUES ('c0aa551f-a632-4496-acfb-7783ac883681','Toyota Corolla','Corolla','AMOC and General Pool',true,0,NULL,NULL,NULL,NOW(),NOW())"
            };
            foreach (var s in ccSql) await _db.Database.ExecuteSqlRawAsync(s, ct);
            results.Add($"CarClasses inserted: {ccSql.Length}");

            // Step 3: Replace HotelOptions
            rows = await _db.Database.ExecuteSqlRawAsync(@"DELETE FROM ""HotelOptions""", ct);
            results.Add($"HotelOptions deleted: {rows}");
            var hotelSql = new[]
            {
                "INSERT INTO \"HotelOptions\" (\"Id\",\"Name\",\"IsActive\",\"DisplayOrder\",\"ContractedRoomsIsDB\",\"ContractedRoomsGuest\",\"ActualOccupiedIsDB\",\"ActualOccupiedGuest\",\"CreatedAt\",\"UpdatedAt\") VALUES ('f97d2160-fd5c-46e3-aa80-ca9de3d707c6','Baku Marriott Hotel Boulevard',true,1,0,0,0,0,NOW(),NOW())",
                "INSERT INTO \"HotelOptions\" (\"Id\",\"Name\",\"IsActive\",\"DisplayOrder\",\"ContractedRoomsIsDB\",\"ContractedRoomsGuest\",\"ActualOccupiedIsDB\",\"ActualOccupiedGuest\",\"CreatedAt\",\"UpdatedAt\") VALUES ('6f7d4dd1-b05f-4226-a2f5-8c551442ea98','Courtyard by Marriott',true,2,0,0,0,0,NOW(),NOW())",
                "INSERT INTO \"HotelOptions\" (\"Id\",\"Name\",\"IsActive\",\"DisplayOrder\",\"ContractedRoomsIsDB\",\"ContractedRoomsGuest\",\"ActualOccupiedIsDB\",\"ActualOccupiedGuest\",\"CreatedAt\",\"UpdatedAt\") VALUES ('2dec110d-0e92-4bbf-9974-76aa11cc3289','InterContinental Baku',true,3,0,0,0,0,NOW(),NOW())",
                "INSERT INTO \"HotelOptions\" (\"Id\",\"Name\",\"IsActive\",\"DisplayOrder\",\"ContractedRoomsIsDB\",\"ContractedRoomsGuest\",\"ActualOccupiedIsDB\",\"ActualOccupiedGuest\",\"CreatedAt\",\"UpdatedAt\") VALUES ('6f4e3e4f-d42e-45d5-b89a-954aad58ba1c','JW Marriott Absheron Baku',true,4,0,0,0,0,NOW(),NOW())",
                "INSERT INTO \"HotelOptions\" (\"Id\",\"Name\",\"IsActive\",\"DisplayOrder\",\"ContractedRoomsIsDB\",\"ContractedRoomsGuest\",\"ActualOccupiedIsDB\",\"ActualOccupiedGuest\",\"CreatedAt\",\"UpdatedAt\") VALUES ('185e6cad-288e-43f2-bebd-6771b8cc6112','Radisson Hotel Baku',true,5,0,0,0,0,NOW(),NOW())",
                "INSERT INTO \"HotelOptions\" (\"Id\",\"Name\",\"IsActive\",\"DisplayOrder\",\"ContractedRoomsIsDB\",\"ContractedRoomsGuest\",\"ActualOccupiedIsDB\",\"ActualOccupiedGuest\",\"CreatedAt\",\"UpdatedAt\") VALUES ('aaba0c5b-a1b9-4fc9-8d63-34b174c812ee','The Ritz Carlton, Baku',true,6,0,0,0,0,NOW(),NOW())"
            };
            foreach (var s in hotelSql) await _db.Database.ExecuteSqlRawAsync(s, ct);
            results.Add($"HotelOptions inserted: {hotelSql.Length}");

            // Step 4: Replace PickupDayOptions
            rows = await _db.Database.ExecuteSqlRawAsync(@"DELETE FROM ""PickupDayOptions""", ct);
            results.Add($"PickupDayOptions deleted: {rows}");
            var daySql = new[]
            {
                "INSERT INTO \"PickupDayOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('cdf8d53d-9a50-4834-9496-35f206a1a83e','Sunday, 07 May 2026','2026-05-07',true,0,NOW(),NOW())",
                "INSERT INTO \"PickupDayOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('e84972f6-c3d9-426c-9751-3cdb1cc303d3','Friday, 8 May 2026','2026-05-08',true,0,NOW(),NOW())",
                "INSERT INTO \"PickupDayOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('b90411a4-9f86-4252-bd8c-c6c7e8f66e35','Saturday, 9 May 2026','2026-05-09',true,0,NOW(),NOW())",
                "INSERT INTO \"PickupDayOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('b6b9b7ed-eb5d-4fdb-a526-b23871589e04','Saturday, 19 Jul','2026-07-19',true,1,NOW(),NOW())",
                "INSERT INTO \"PickupDayOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('0caa7dfc-6807-41de-9181-0645444618d9','Sunday, 20 Jul','2026-07-20',true,2,NOW(),NOW())",
                "INSERT INTO \"PickupDayOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('8df72362-de27-4430-9328-c975f879d66f','Monday, 21 Jul','2026-07-21',true,3,NOW(),NOW())"
            };
            foreach (var s in daySql) await _db.Database.ExecuteSqlRawAsync(s, ct);
            results.Add($"PickupDayOptions inserted: {daySql.Length}");

            // Step 5: Replace PickupHourOptions
            rows = await _db.Database.ExecuteSqlRawAsync(@"DELETE FROM ""PickupHourOptions""", ct);
            results.Add($"PickupHourOptions deleted: {rows}");
            var hourSql = new[]
            {
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('60fee2b1-fd93-47af-8107-06a8e53d5a74','12:00 AM','00:00',true,0,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('475ced9a-ef9e-4922-a5d4-7b2814a1543e','01:00 AM','01:00',true,1,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('8306efde-316c-489e-819e-c47498c07511','02:00 AM','02:00',true,2,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('7c623451-d466-4d69-898b-b96254f080ab','03:00 AM','03:00',true,3,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('aa32d9fb-74e2-4f40-af4c-4be02e2b2330','04:00 AM','04:00',true,4,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('48527fea-ebb2-4858-bd47-fc220c2d9ef3','05:00 AM','05:00',true,5,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('d5b0f58f-e859-4977-8afd-3088b71d81e4','06:00 AM','06:00',true,6,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('218b253d-9aeb-46c4-8a7a-ec1209572715','07:00 AM','07:00',true,7,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('0b324eae-f169-42a3-a83e-3fed6153c2f1','08:00 AM','08:00',true,8,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('a655c952-d713-49e3-bafa-cca3ccc08090','09:00 AM','09:00',true,9,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('ddc6ce16-7ee4-459a-a20f-4f914083b3c7','10:00 AM','10:00',true,10,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('cdee7561-1253-41c7-8f2c-cee289d41f3d','11:00 AM','11:00',true,11,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('30f07c2c-7ed8-4dd4-84da-517cbf23046a','12:00 PM','12:00',true,12,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('54e1c612-3c0b-4f00-beeb-a8b9a050093b','01:00 PM','13:00',true,13,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('2357209b-a80f-4359-8c11-fd5aab8f768b','02:00 PM','14:00',true,14,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('9179b3e6-5e27-4e8a-9494-b5a4d9f6c56d','03:00 PM','15:00',true,15,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('545d7988-2e7f-4bba-a857-f84fe7e6eb5f','04:00 PM','16:00',true,16,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('16a69e7b-20fb-449c-96ca-d7c158a1fdaa','05:00 PM','17:00',true,17,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('98c94eb7-9809-496f-bc20-64f53b055b83','06:00 PM','18:00',true,18,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('6cf699ef-bbc0-42e0-b134-53a84d2c2ad9','07:00 PM','19:00',true,19,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('af0919b1-5a71-4964-b361-ed342a7ddbbf','08:00 PM','20:00',true,20,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('417d9879-f5a7-4dcf-b49d-faa3d5791db6','09:00 PM','21:00',true,21,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('955fffd2-1da2-49e7-ab63-b7eafd8bd95b','10:00 PM','22:00',true,22,NOW(),NOW())",
                "INSERT INTO \"PickupHourOptions\" (\"Id\",\"Label\",\"Value\",\"IsActive\",\"DisplayOrder\",\"CreatedAt\",\"UpdatedAt\") VALUES ('03eaabed-1a2b-45ed-acf9-b511a593f93f','11:00 PM','23:00',true,23,NOW(),NOW())"
            };
            foreach (var s in hourSql) await _db.Database.ExecuteSqlRawAsync(s, ct);
            results.Add($"PickupHourOptions inserted: {hourSql.Length}");

            await tx.CommitAsync(ct);
            return Ok(new { success = true, actions = results });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return StatusCode(500, new { success = false, error = ex.Message, actions = results });
        }
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
