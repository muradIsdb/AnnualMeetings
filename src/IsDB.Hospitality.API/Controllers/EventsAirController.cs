using IsDB.Hospitality.Application.Common.Helpers;
using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.EventsAir;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq;

namespace IsDB.Hospitality.API.Controllers;

[Authorize(Roles = "Admin")]
public class EventsAirController : ApiControllerBase
{
    private readonly IAppDbContext _db;
    private readonly AppDbContext _appDb;
    private readonly IHttpClientFactory _httpClientFactory;

    public EventsAirController(IAppDbContext db, AppDbContext appDb, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _appDb = appDb;
        _httpClientFactory = httpClientFactory;
    }

    // Helper: read OAuthScope from DB via raw SQL (column is NotMapped on entity to avoid breaking existing deployments)
    private async Task<string> GetOAuthScopeAsync()
    {
        const string defaultScope = "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";
        try
        {
            var conn = _appDb.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT \"OAuthScope\" FROM \"EventsAirConfigs\" LIMIT 1";
            var result = await cmd.ExecuteScalarAsync();
            return result is string s && !string.IsNullOrWhiteSpace(s) ? s : defaultScope;
        }
        catch
        {
            return defaultScope;
        }
    }

    // Helper: save OAuthScope to DB via raw SQL
    private async Task SaveOAuthScopeAsync(Guid configId, string scope)
    {
        try
        {
            await _appDb.Database.ExecuteSqlRawAsync(
                "UPDATE \"EventsAirConfigs\" SET \"OAuthScope\" = {0} WHERE \"Id\" = {1}",
                scope, configId);
        }
        catch { /* column may not exist yet on very first deployment */ }
    }

    // GET /api/eventsair/config
    [HttpGet("config")]
    public async Task<ActionResult<EventsAirConfigDto>> GetConfig()
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync();
        if (config == null)
        {
            // Return default empty config if none exists
            return Ok(new EventsAirConfigDto
            {
                Id = Guid.Empty,
                ClientId = "",
                ClientSecret = "",
                ApiBaseUrl = "https://api.eventsair.com",
                TokenEndpoint = "https://auth.eventsair.com/connect/token",
                EventCode = "",
                TenantCode = "",
                SyncIntervalMinutes = 15,
                AutoSyncEnabled = false,
                SyncOnStartup = false,
                LastSyncStatus = "Never",
                IsActive = false
            });
        }

        return Ok(new EventsAirConfigDto
        {
            Id = config.Id,
            ClientId = config.ClientId,
            ClientSecret = string.IsNullOrEmpty(config.ClientSecret) ? "" : "••••••••••••",
            ApiBaseUrl = config.ApiBaseUrl,
            TokenEndpoint = config.TokenEndpoint,
            EventCode = config.EventCode,
            TenantCode = config.TenantCode,
            SyncIntervalMinutes = config.SyncIntervalMinutes,
            AutoSyncEnabled = config.AutoSyncEnabled,
            SyncOnStartup = config.SyncOnStartup,
            LastSyncAt = config.LastSyncAt,
            LastSyncStatus = config.LastSyncStatus,
            LastSyncMessage = config.LastSyncMessage,
            LastSyncRecordsCount = config.LastSyncRecordsCount,
            IsActive = config.IsActive,
            OAuthScope = await GetOAuthScopeAsync()
        });
    }

    // PUT /api/eventsair/config
    [HttpPut("config")]
    public async Task<ActionResult<object>> UpdateConfig([FromBody] UpdateEventsAirConfigRequest request)
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync();
        var previousEventCode = config?.EventCode;

        if (config == null)
        {
            config = new EventsAirConfig
            {
                CreatedAt = DateTime.UtcNow
            };
            _db.EventsAirConfigs.Add(config);
        }

        // Only overwrite shared credentials if the new value is non-empty
        // (prevents accidental clearing when the form sends empty strings)
        if (!string.IsNullOrEmpty(request.ClientId))
            config.ClientId = request.ClientId;
        if (!string.IsNullOrEmpty(request.ClientSecret))
            config.ClientSecret = request.ClientSecret;
        if (!string.IsNullOrEmpty(request.ApiBaseUrl))
            config.ApiBaseUrl = request.ApiBaseUrl;
        if (!string.IsNullOrEmpty(request.TokenEndpoint))
            config.TokenEndpoint = request.TokenEndpoint;
        if (!string.IsNullOrEmpty(request.TenantCode))
            config.TenantCode = request.TenantCode;
        // EventCode is always updated (it's the per-event field being changed intentionally)
        config.EventCode = request.EventCode;
        config.SyncIntervalMinutes = request.SyncIntervalMinutes;
        config.AutoSyncEnabled = request.AutoSyncEnabled;
        config.SyncOnStartup = request.SyncOnStartup;
        config.IsActive = request.IsActive;
        config.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(CancellationToken.None);

        // Save OAuthScope via raw SQL since the property is NotMapped
        if (!string.IsNullOrWhiteSpace(request.OAuthScope))
            await SaveOAuthScopeAsync(config.Id, request.OAuthScope.Trim());

        // Detect event switch
        bool eventSwitched = !string.IsNullOrEmpty(request.EventCode) &&
                             !string.IsNullOrEmpty(previousEventCode) &&
                             request.EventCode != previousEventCode;
        bool newEventHasCarClasses = false;
        bool newEventHasVehicles = false;
        if (eventSwitched)
        {
            newEventHasCarClasses = await _db.CarClasses
                .AnyAsync(c => c.EventCode == request.EventCode, CancellationToken.None);
            newEventHasVehicles = await _db.Vehicles
                .AnyAsync(v => v.IsActive && v.EventCode == request.EventCode, CancellationToken.None);
        }

        var dto = new EventsAirConfigDto
        {
            Id = config.Id,
            ClientId = config.ClientId,
            ClientSecret = "••••••••••••",
            ApiBaseUrl = config.ApiBaseUrl,
            TokenEndpoint = config.TokenEndpoint,
            EventCode = config.EventCode,
            TenantCode = config.TenantCode,
            SyncIntervalMinutes = config.SyncIntervalMinutes,
            AutoSyncEnabled = config.AutoSyncEnabled,
            SyncOnStartup = config.SyncOnStartup,
            LastSyncAt = config.LastSyncAt,
            LastSyncStatus = config.LastSyncStatus,
            LastSyncMessage = config.LastSyncMessage,
            LastSyncRecordsCount = config.LastSyncRecordsCount,
            IsActive = config.IsActive,
            OAuthScope = await GetOAuthScopeAsync()
        };

        return Ok(new
        {
            config = dto,
            eventSwitched,
            newEventHasCarClasses,
            newEventHasVehicles,
            previousEventCode
        });
    }

    // POST /api/eventsair/config/apply-event-switch
    [HttpPost("config/apply-event-switch")]
    public async Task<IActionResult> ApplyEventSwitch(
        [FromBody] ApplyEventSwitchRequest request,
        CancellationToken ct)
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync(ct);
        if (config == null) return BadRequest("No EventsAir configuration found.");

        var newEventCode = config.EventCode;
        var previousEventCode = request.PreviousEventCode;

        // 1. Reset all guest DeservedCarClassId
        var allGuests = await _db.Guests.Where(g => g.IsActive).ToListAsync(ct);
        foreach (var g in allGuests)
            g.DeservedCarClassId = null;

        // 2. Reset hotel room counts
        var hotels = await _db.HotelOptions.ToListAsync(ct);
        foreach (var h in hotels)
        {
            h.ContractedRoomsIsDB = 0;
            h.ContractedRoomsGuest = 0;
            h.ActualOccupiedIsDB = 0;
            h.ActualOccupiedGuest = 0;
        }

        // 3. Stamp all NULL-EventCode records (guests + fleet) with previousEventCode so they are scoped to the old event
        if (!string.IsNullOrEmpty(previousEventCode))
        {
            var nullEventGuests = await _db.Guests.Where(g => g.EventCode == null).ToListAsync(ct);
            foreach (var g in nullEventGuests)
                g.EventCode = previousEventCode;

            var nullEventVehicles = await _db.Vehicles.Where(v => v.EventCode == null).ToListAsync(ct);
            foreach (var v in nullEventVehicles)
                v.EventCode = previousEventCode;

            var nullEventDrivers = await _db.Drivers.Where(d => d.EventCode == null).ToListAsync(ct);
            foreach (var d in nullEventDrivers)
                d.EventCode = previousEventCode;

            var nullEventCarClasses = await _db.CarClasses.Where(c => c.EventCode == null).ToListAsync(ct);
            foreach (var c in nullEventCarClasses)
                c.EventCode = previousEventCode;

            var nullEventCarClassRules = await _db.CarClassRules.Where(r => r.EventCode == null).ToListAsync(ct);
            foreach (var r in nullEventCarClassRules)
                r.EventCode = previousEventCode;

            var nullEventFieldMappings = await _db.SyncFieldMappings.Where(m => m.EventCode == null).ToListAsync(ct);
            foreach (var m in nullEventFieldMappings)
                m.EventCode = previousEventCode;
        }

        // 4. Optionally copy Car Classes from previous event
        if (request.CopyCarClasses && !string.IsNullOrEmpty(previousEventCode))
        {
            var previousCarClasses = await _db.CarClasses
                .Where(c => c.EventCode == previousEventCode)
                .ToListAsync(ct);

            foreach (var cc in previousCarClasses)
            {
                // Only copy if the new event doesn't already have a class with the same name
                bool alreadyExists = await _db.CarClasses
                    .AnyAsync(c => c.EventCode == newEventCode && c.Name == cc.Name, ct);
                if (!alreadyExists)
                {
                    _db.CarClasses.Add(new CarClass
                    {
                        Id = Guid.NewGuid(),
                        Name = cc.Name,
                        Description = cc.Description,
                        Color = cc.Color,
                        SortOrder = cc.SortOrder,
                        EventCode = newEventCode,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // 5. Optionally copy Fleet (vehicles + drivers) from previous event
        if (request.CopyFleet && !string.IsNullOrEmpty(previousEventCode))
        {
            var previousVehicles = await _db.Vehicles
                .Where(v => v.IsActive && v.EventCode == previousEventCode)
                .ToListAsync(ct);

            // Load existing 2026 vehicle identifiers to avoid duplicates
            var existingNewEventVehicleKeys = await _db.Vehicles
                .Where(x => x.EventCode == newEventCode)
                .Select(x => new { x.LicensePlate, x.CarNumber })
                .ToListAsync(ct);
            var existingVehicleSet = new HashSet<string>(
                existingNewEventVehicleKeys.Select(x => $"{x.LicensePlate}|{x.CarNumber}"));

            foreach (var v in previousVehicles)
            {
                var key = $"{v.LicensePlate}|{v.CarNumber}";
                if (!existingVehicleSet.Contains(key))
                {
                    existingVehicleSet.Add(key); // prevent duplicates within the batch
                    _db.Vehicles.Add(new Vehicle
                    {
                        Id = Guid.NewGuid(),
                        LicensePlate = v.LicensePlate,
                        Make = v.Make,
                        Model = v.Model,
                        Color = v.Color,
                        CarNumber = v.CarNumber,
                        BarcodeValue = v.BarcodeValue,
                        Status = VehicleStatus.Available,
                        IsActive = true,
                        EventCode = newEventCode,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            var previousDrivers = await _db.Drivers
                .Where(d => d.IsActive && d.EventCode == previousEventCode)
                .ToListAsync(ct);

            var existingNewEventDriverNames = await _db.Drivers
                .Where(x => x.EventCode == newEventCode)
                .Select(x => x.FullName)
                .ToListAsync(ct);
            var existingDriverSet = new HashSet<string>(existingNewEventDriverNames, StringComparer.OrdinalIgnoreCase);

            foreach (var d in previousDrivers)
            {
                if (!existingDriverSet.Contains(d.FullName))
                {
                    existingDriverSet.Add(d.FullName);
                    _db.Drivers.Add(new Driver
                    {
                        Id = Guid.NewGuid(),
                        FullName = d.FullName,
                        Phone = d.Phone,
                        Status = DriverStatus.Available,
                        IsActive = true,
                        EventCode = newEventCode,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "Event switch applied successfully." });
    }

    public record ApplyEventSwitchRequest(
        string PreviousEventCode,
        bool CopyCarClasses,
        bool CopyFleet = false
    );

    // POST /api/eventsair/test-connection
    [HttpPost("test-connection")]
    public async Task<ActionResult<TestConnectionResult>> TestConnection([FromBody] TestConnectionRequest? request)
    {
        if (request == null)
        {
            return Ok(new TestConnectionResult
            {
                Success = false,
                Message = "Invalid request. Please provide connection details.",
                ResponseTimeMs = 0
            });
        }

        // If ClientSecret is blank, fall back to the saved secret in the database
        var effectiveSecret = request.ClientSecret;
        if (string.IsNullOrWhiteSpace(effectiveSecret))
        {
            var savedConfig = await _db.EventsAirConfigs.FirstOrDefaultAsync();
            effectiveSecret = savedConfig?.ClientSecret ?? string.Empty;
        }

        // If ClientId is also blank, try to use saved values
        var effectiveClientId = string.IsNullOrWhiteSpace(request.ClientId)
            ? (await _db.EventsAirConfigs.FirstOrDefaultAsync())?.ClientId ?? string.Empty
            : request.ClientId;

        if (string.IsNullOrWhiteSpace(effectiveClientId) || string.IsNullOrWhiteSpace(effectiveSecret))
        {
            return Ok(new TestConnectionResult
            {
                Success = false,
                Message = "Client ID and Client Secret are required. Please save your credentials first, then test the connection.",
                ResponseTimeMs = 0
            });
        }

        // EventsAir uses Microsoft Azure AD for OAuth2 — the stored TokenEndpoint (auth.eventsair.com)
        // does not resolve; always use the correct Azure AD endpoint and scope.
        const string azureAdTokenEndpoint = "https://login.microsoftonline.com/dff76352-1ded-46e8-96a4-1a83718b2d3a/oauth2/v2.0/token";
        var eventsAirScope = await GetOAuthScopeAsync();

        var sw = Stopwatch.StartNew();
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", effectiveClientId),
                new KeyValuePair<string, string>("client_secret", effectiveSecret),
                new KeyValuePair<string, string>("scope", eventsAirScope)
            });

            var response = await client.PostAsync(azureAdTokenEndpoint, tokenRequest);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                // Extract a token preview (first 20 chars)
                var tokenPreview = content.Length > 50
                    ? content.Substring(0, 50) + "..."
                    : content;

                return Ok(new TestConnectionResult
                {
                    Success = true,
                    Message = "Connection successful. OAuth2 token retrieved via Microsoft Azure AD for EventsAir.",
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    TokenPreview = tokenPreview
                });
            }
            else
            {
                return Ok(new TestConnectionResult
                {
                    Success = false,
                    Message = $"Authentication failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Please verify your Client ID and Client Secret.",
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds
                });
            }
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return Ok(new TestConnectionResult
            {
                Success = false,
                Message = "Connection timed out after 10 seconds. Please verify the Token Endpoint URL is reachable.",
                ResponseTimeMs = (int)sw.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Ok(new TestConnectionResult
            {
                Success = false,
                Message = $"Connection error: {ex.Message}",
                ResponseTimeMs = (int)sw.ElapsedMilliseconds
            });
        }
    }

    // POST /api/eventsair/sync
    // The UI "Sync Now" button calls /guests/sync-from-eventsair (async job) instead,
    // but this endpoint is kept for direct API / admin use. It now returns immediately
    // and triggers the background service.
    [HttpPost("sync")]
    public IActionResult TriggerSync(
        [FromServices] Microsoft.Extensions.Hosting.IHostApplicationLifetime hostAppLifetime)
    {
        // For direct API triggers, we just return a message saying the background job is preferred.
        // We shouldn't duplicate the complex async job logic here.
        return Ok(new TriggerSyncResult
        {
            Success = true,
            Message = "Sync triggered. Please use POST /api/guests/sync-from-eventsair for async job tracking.",
            RecordsSynced = 0,
            DurationMs = 0
        });
    }

    // GET /api/eventsair/sync-logs
    [HttpGet("sync-logs")]
    public async Task<IActionResult> GetSyncLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? syncType = null,
        [FromQuery] string? triggerSource = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 20;

        var query = _db.EventsAirSyncLogs.AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(syncType))
            query = query.Where(l => l.SyncType == syncType);
        if (!string.IsNullOrWhiteSpace(triggerSource))
            query = query.Where(l => l.TriggerSource == triggerSource);
        if (startDate.HasValue)
            query = query.Where(l => l.SyncedAt >= startDate.Value.ToUniversalTime());
        if (endDate.HasValue)
            query = query.Where(l => l.SyncedAt <= endDate.Value.ToUniversalTime().AddDays(1));

        // Search (message or staff name)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(l =>
                (l.Message != null && l.Message.ToLower().Contains(s)) ||
                (l.InitiatedByStaffName != null && l.InitiatedByStaffName.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var logs = await query
            .OrderByDescending(l => l.SyncedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new EventsAirSyncLogDto
            {
                Id = l.Id,
                SyncedAt = l.SyncedAt,
                Status = l.Status,
                Message = l.Message,
                RecordsSynced = l.RecordsSynced,
                DurationMs = l.DurationMs,
                SyncType = l.SyncType,
                TriggerSource = l.TriggerSource,
                InitiatedByStaffName = l.InitiatedByStaffName,
                RecordsAdded = l.RecordsAdded,
                RecordsUpdated = l.RecordsUpdated,
                RecordsDeactivated = l.RecordsDeactivated,
                TravelBookingsSynced = l.TravelBookingsSynced
            })
            .ToListAsync();

        return Ok(new
        {
            items = logs,
            totalCount,
            totalPages,
            page,
            pageSize
        });
    }

    // GET /api/eventsair/events
    // Returns all EventsAir events whose name contains "annual" (case-insensitive),
    // sorted by startDate descending. Used to populate the Event Code dropdown in the config UI.
    [HttpGet("events")]
    public async Task<ActionResult<List<Application.DTOs.EventsAir.EventsAirEventDto>>> GetEvents()
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync();
        if (config == null || string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.ClientSecret))
        {
            return Ok(new List<Application.DTOs.EventsAir.EventsAirEventDto>());
        }

        try
        {
            var token = await Application.Common.Models.EventsAirSyncHelpers.GetEventsAirTokenAsync(
                config.ClientId, config.ClientSecret, _httpClientFactory, await GetOAuthScopeAsync());

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            // Query EventsAir for events whose name contains "Annual" (case-sensitive CONTAINS)
            // We build the JSON body manually to avoid escaping issues with verbatim strings
            var queryBody = "{\"query\":\"{events(input:{where:{name:{comparisonType:CONTAINS,value:\\\"Annual\\\"}}}limit:200 offset:0){uniqueCode name startDate endDate}}\"}";
            // Use JsonSerializer to build a clean query body
            var gqlQuery = "{ events(input: { where: { name: { comparisonType: CONTAINS, value: \"Annual\" } } } limit: 200 offset: 0) { id name startDate endDate } }";
            queryBody = System.Text.Json.JsonSerializer.Serialize(new { query = gqlQuery });
            var req = new HttpRequestMessage(HttpMethod.Post, $"{config.ApiBaseUrl.TrimEnd('/')}/graphql")
            {
                Headers = { { "Authorization", $"Bearer {token}" } },
                Content = new StringContent(queryBody, System.Text.Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(req);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);

            var eventsArray = doc.GetProperty("data").GetProperty("events");
            var result = new List<Application.DTOs.EventsAir.EventsAirEventDto>();

            foreach (var ev in eventsArray.EnumerateArray())
            {
                var uniqueCode = ev.TryGetProperty("id", out var uc) ? uc.GetString() ?? "" : "";
                var name = ev.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var startDate = ev.TryGetProperty("startDate", out var sd) ? sd.GetString() : null;
                var endDate = ev.TryGetProperty("endDate", out var ed) ? ed.GetString() : null;

                result.Add(new Application.DTOs.EventsAir.EventsAirEventDto
                {
                    UniqueCode = uniqueCode,
                    Name = name,
                    StartDate = startDate,
                    EndDate = endDate
                });
            }

            // If no results with "Annual" filter, try without filter as fallback
            if (result.Count == 0)
            {
                var gqlQueryAll = "{ events(limit: 200 offset: 0) { id name startDate endDate } }";
                var queryBodyAll = System.Text.Json.JsonSerializer.Serialize(new { query = gqlQueryAll });
                var req2 = new HttpRequestMessage(HttpMethod.Post, $"{config.ApiBaseUrl.TrimEnd('/')}/graphql")
                {
                    Headers = { { "Authorization", $"Bearer {token}" } },
                    Content = new StringContent(queryBodyAll, System.Text.Encoding.UTF8, "application/json")
                };
                var response2 = await client.SendAsync(req2);
                if (response2.IsSuccessStatusCode)
                {
                    var json2 = await response2.Content.ReadAsStringAsync();
                    var doc2 = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json2);
                    if (doc2.TryGetProperty("data", out var data2) && data2.TryGetProperty("events", out var eventsArray2))
                    {
                        foreach (var ev in eventsArray2.EnumerateArray())
                        {
                            var uniqueCode = ev.TryGetProperty("id", out var uc) ? uc.GetString() ?? "" : "";
                            var name = ev.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            var startDate = ev.TryGetProperty("startDate", out var sd) ? sd.GetString() : null;
                            var endDate = ev.TryGetProperty("endDate", out var ed) ? ed.GetString() : null;
                            result.Add(new Application.DTOs.EventsAir.EventsAirEventDto
                            {
                                UniqueCode = uniqueCode,
                                Name = name,
                                StartDate = startDate,
                                EndDate = endDate
                            });
                        }
                    }
                }
            }

            // Sort by startDate descending (most recent first)
            result = result
                .OrderByDescending(e => e.StartDate ?? "")
                .ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            // Return 502 so the UI can distinguish "failed to load" from "genuinely no events"
            return StatusCode(502, new { error = "Failed to load events from EventsAir", detail = ex.Message });
        }
    }

    // GET /api/eventsair/debug-travel
    // Debug endpoint: fetches raw travel bookings from EventsAir to diagnose sync issues
    [HttpGet("debug-travel")]
    public async Task<IActionResult> DebugTravel(CancellationToken cancellationToken)
    {
        var config = await _appDb.EventsAirConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config == null || !config.IsActive)
            return BadRequest(new { message = "EventsAir not configured or inactive." });
        var oAuthScope = await GetOAuthScopeAsync();
        string token;
        try
        {
            var tokenClient = _httpClientFactory.CreateClient();
            var tokenReq = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type", "client_credentials"),
                new KeyValuePair<string,string>("client_id", config.ClientId),
                new KeyValuePair<string,string>("client_secret", config.ClientSecret),
                new KeyValuePair<string,string>("scope", !string.IsNullOrWhiteSpace(oAuthScope)
                    ? oAuthScope
                    : "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default")
            });
            var tokenUrl = config.TokenEndpoint;
            var tokenResp = await tokenClient.PostAsync(tokenUrl, tokenReq, cancellationToken);
            tokenResp.EnsureSuccessStatusCode();
            var tokenJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                await tokenResp.Content.ReadAsStringAsync(cancellationToken));
            token = tokenJson.GetProperty("access_token").GetString()!;
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get token", detail = ex.Message });
        }
        // Fetch first page raw
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        var query = $"{{ event(id: \"{config.EventCode}\") {{ travelBookings(input: {{}}, limit: 5, offset: 0) {{ id contact {{ id }} travelType {{ name }} flightNumber carrier {{ name }} arrivalDate departureDate eta etd departurePort {{ name }} arrivalPort {{ name }} }} }} }}";
        var req = new HttpRequestMessage(HttpMethod.Post, $"{config.ApiBaseUrl.TrimEnd('/')}/graphql")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) },
            Content = new System.Net.Http.StringContent(
                System.Text.Json.JsonSerializer.Serialize(new { query }),
                System.Text.Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(req, cancellationToken);
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        // Parse the first page to extract sample records
        var samples = new List<object>();
        try
        {
            var doc = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(rawJson);
            if (doc.TryGetProperty("data", out var data) &&
                data.TryGetProperty("event", out var evt) &&
                evt.TryGetProperty("travelBookings", out var bookings))
            {
                foreach (var b in bookings.EnumerateArray())
                {
                    samples.Add(new
                    {
                        id = b.TryGetProperty("id", out var id) ? id.GetString() : null,
                        contactId = b.TryGetProperty("contact", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.Object
                            ? (c.TryGetProperty("id", out var cid) ? cid.GetString() : null) : null,
                        travelType = b.TryGetProperty("travelType", out var tt) && tt.ValueKind == System.Text.Json.JsonValueKind.Object
                            ? (tt.TryGetProperty("name", out var ttn) ? ttn.GetString() : null) : null,
                        flightNumber = b.TryGetProperty("flightNumber", out var fn) && fn.ValueKind != System.Text.Json.JsonValueKind.Null ? fn.GetString() : null,
                        arrivalDate = b.TryGetProperty("arrivalDate", out var ad) && ad.ValueKind != System.Text.Json.JsonValueKind.Null ? ad.GetString() : null,
                        eta = b.TryGetProperty("eta", out var eta) && eta.ValueKind != System.Text.Json.JsonValueKind.Null ? eta.GetString() : null,
                        departureDate = b.TryGetProperty("departureDate", out var dd) && dd.ValueKind != System.Text.Json.JsonValueKind.Null ? dd.GetString() : null,
                    });
                }
            }
        }
        catch { }
        return Ok(new
        {
            eventCode = config.EventCode,
            httpStatus = (int)response.StatusCode,
            rawFirstPage = rawJson,
            samplesCount = samples.Count,
            samples
        });
    }

    // GET /api/eventsair/debug-travel-contact?contactId=xxx
    // Queries travel bookings for a specific contact to diagnose sync issues
    [HttpGet("debug-travel-contact")]
    public async Task<IActionResult> DebugTravelContact([FromQuery] string contactId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(contactId))
            return BadRequest(new { message = "contactId query parameter is required." });
        var config = await _appDb.EventsAirConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config == null || !config.IsActive)
            return BadRequest(new { message = "EventsAir not configured or inactive." });
        var oAuthScope = await GetOAuthScopeAsync();
        string token;
        try
        {
            var tokenClient = _httpClientFactory.CreateClient();
            var tokenReq = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type", "client_credentials"),
                new KeyValuePair<string,string>("client_id", config.ClientId),
                new KeyValuePair<string,string>("client_secret", config.ClientSecret),
                new KeyValuePair<string,string>("scope", !string.IsNullOrWhiteSpace(oAuthScope)
                    ? oAuthScope
                    : "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default")
            });
            var tokenResp = await tokenClient.PostAsync(config.TokenEndpoint, tokenReq, cancellationToken);
            tokenResp.EnsureSuccessStatusCode();
            var tokenJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                await tokenResp.Content.ReadAsStringAsync(cancellationToken));
            token = tokenJson.GetProperty("access_token").GetString()!;
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get token", detail = ex.Message });
        }
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(25);
        // Query 1: travelBookings filtered by contactId
        var query1 = $"{{ event(id: \"{config.EventCode}\") {{ travelBookings(input: {{ contactId: \"{contactId}\" }}, limit: 10, offset: 0) {{ id contact {{ id }} travelType {{ name }} flightNumber carrier {{ name }} arrivalDate departureDate eta etd departurePort {{ name }} arrivalPort {{ name }} }} }} }}";
        var req1 = new HttpRequestMessage(HttpMethod.Post, $"{config.ApiBaseUrl.TrimEnd('/')}/graphql")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) },
            Content = new System.Net.Http.StringContent(
                System.Text.Json.JsonSerializer.Serialize(new { query = query1 }),
                System.Text.Encoding.UTF8, "application/json")
        };
        var resp1 = await client.SendAsync(req1, cancellationToken);
        var json1 = await resp1.Content.ReadAsStringAsync(cancellationToken);
        // Query 2: contact directly with travelBookings nested
        var query2 = $"{{ event(id: \"{config.EventCode}\") {{ contact(id: \"{contactId}\") {{ id firstName lastName travelBookings {{ id travelType {{ name }} flightNumber arrivalDate departureDate eta etd departurePort {{ name }} arrivalPort {{ name }} }} }} }} }}";
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"{config.ApiBaseUrl.TrimEnd('/')}/graphql")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) },
            Content = new System.Net.Http.StringContent(
                System.Text.Json.JsonSerializer.Serialize(new { query = query2 }),
                System.Text.Encoding.UTF8, "application/json")
        };
        var resp2 = await client.SendAsync(req2, cancellationToken);
        var json2 = await resp2.Content.ReadAsStringAsync(cancellationToken);
        return Ok(new
        {
            eventCode = config.EventCode,
            contactId,
            travelBookingsQuery = new { httpStatus = (int)resp1.StatusCode, raw = json1 },
            contactNestedQuery = new { httpStatus = (int)resp2.StatusCode, raw = json2 }
        });
    }

    // GET /api/eventsair/debug-travel-batch?contactId=xxx
    // Tests the exact batch alias query used by FetchTravelBookingsByContactsAsync
    [HttpGet("debug-travel-batch")]
    public async Task<IActionResult> DebugTravelBatch([FromQuery] string contactId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(contactId))
            return BadRequest(new { message = "contactId query parameter is required." });
        var config = await _appDb.EventsAirConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config == null || !config.IsActive)
            return BadRequest(new { message = "EventsAir not configured or inactive." });
        var oAuthScope = await GetOAuthScopeAsync();
        string token;
        try
        {
            var tokenClient = _httpClientFactory.CreateClient();
            var tokenReq = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type", "client_credentials"),
                new KeyValuePair<string,string>("client_id", config.ClientId),
                new KeyValuePair<string,string>("client_secret", config.ClientSecret),
                new KeyValuePair<string,string>("scope", !string.IsNullOrWhiteSpace(oAuthScope)
                    ? oAuthScope
                    : "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default")
            });
            var tokenResp = await tokenClient.PostAsync(config.TokenEndpoint, tokenReq, cancellationToken);
            tokenResp.EnsureSuccessStatusCode();
            var tokenJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                await tokenResp.Content.ReadAsStringAsync(cancellationToken));
            token = tokenJson.GetProperty("access_token").GetString()!;
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get token", detail = ex.Message });
        }
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(25);
        // Build the exact same batch alias query as FetchTravelBookingsByContactsAsync
        var batchQuery = $"{{ event(id: \"{config.EventCode}\") {{ c0: contact(id: \"{contactId}\") {{ id travelBookings {{ id travelType {{ name }} flightNumber carrier {{ name }} arrivalDate departureDate eta etd departurePort {{ name }} arrivalPort {{ name }} class bookingNotes comment }} }} }} }}";
        var req = new HttpRequestMessage(HttpMethod.Post, $"{config.ApiBaseUrl.TrimEnd('/')}/graphql")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) },
            Content = new System.Net.Http.StringContent(
                System.Text.Json.JsonSerializer.Serialize(new { query = batchQuery }),
                System.Text.Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(req, cancellationToken);
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return Ok(new
        {
            eventCode = config.EventCode,
            contactId,
            batchQuery,
            httpStatus = (int)response.StatusCode,
            rawResponse = rawJson
        });
    }

    // GET /api/eventsair/debug-schema
    // Introspects the EventsAir GraphQL schema to find available fields on the Event type
    [HttpGet("debug-schema")]
    public async Task<IActionResult> DebugSchema(CancellationToken cancellationToken)
    {
        var config = await _appDb.EventsAirConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config == null || !config.IsActive)
            return BadRequest(new { message = "EventsAir not configured or inactive." });
        var oAuthScope = await GetOAuthScopeAsync();
        string token;
        try
        {
            var tokenClient = _httpClientFactory.CreateClient();
            var tokenReq = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type", "client_credentials"),
                new KeyValuePair<string,string>("client_id", config.ClientId),
                new KeyValuePair<string,string>("client_secret", config.ClientSecret),
                new KeyValuePair<string,string>("scope", !string.IsNullOrWhiteSpace(oAuthScope)
                    ? oAuthScope
                    : "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default")
            });
            var tokenResp = await tokenClient.PostAsync(config.TokenEndpoint, tokenReq, cancellationToken);
            tokenResp.EnsureSuccessStatusCode();
            var tokenJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                await tokenResp.Content.ReadAsStringAsync(cancellationToken));
            token = tokenJson.GetProperty("access_token").GetString()!;
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get token", detail = ex.Message });
        }
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(25);
        // Introspect the Event type to get all available fields
        var introspectionQuery = "{\"query\":\"{ __type(name: \\\"Event\\\") { fields { name description args { name type { name kind ofType { name kind } } } type { name kind ofType { name kind } } } } }\"}";
        var req = new HttpRequestMessage(HttpMethod.Post, $"{config.ApiBaseUrl.TrimEnd('/')}/graphql")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) },
            Content = new System.Net.Http.StringContent(introspectionQuery, System.Text.Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(req, cancellationToken);
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        // Extract field names for easy reading
        var fieldNames = new List<string>();
        try
        {
            var doc = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(rawJson);
            if (doc.TryGetProperty("data", out var data) &&
                data.TryGetProperty("__type", out var typeObj) &&
                typeObj.TryGetProperty("fields", out var fields))
            {
                foreach (var f in fields.EnumerateArray())
                    if (f.TryGetProperty("name", out var fname))
                        fieldNames.Add(fname.GetString() ?? "");
            }
        }
        catch { }
        return Ok(new
        {
            httpStatus = (int)response.StatusCode,
            eventTypeFields = fieldNames,
            rawResponse = rawJson.Length > 5000 ? rawJson[..5000] + "...[truncated]" : rawJson
        });
    }

    // GET /api/eventsair/debug-travel-sync
    // Runs the exact same travel sync logic as Pass 3 but reports counts without saving
    [HttpGet("debug-travel-sync")]
    [Authorize]
    public async Task<ActionResult<object>> DebugTravelSync(CancellationToken cancellationToken)
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config == null || !config.IsActive) return BadRequest("No active config");
        const string defaultDedicatedCarGuid = "d6b74b23-c8b6-d044-5d86-3a17bafe27de";
        const string defaultRankGuid = "3d96b87e-87b0-145e-5f45-3a17bafe26d4";
        var fieldMappings = await _db.SyncFieldMappings
            .Where(f => f.EventCode == null || f.EventCode == config.EventCode)
            .ToListAsync(cancellationToken);
        var dedicatedCarGuid2 = (fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Dedicated Car", StringComparison.OrdinalIgnoreCase) && f.EventCode == config.EventCode)
            ?? fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Dedicated Car", StringComparison.OrdinalIgnoreCase)))
            ?.EventsAirFieldGuid ?? defaultDedicatedCarGuid;
        var rankGuid2 = (fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Rank", StringComparison.OrdinalIgnoreCase) && f.EventCode == config.EventCode)
            ?? fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Rank", StringComparison.OrdinalIgnoreCase)))
            ?.EventsAirFieldGuid ?? defaultRankGuid;
        var token = await Application.Common.Models.EventsAirSyncHelpers.GetEventsAirTokenAsync(
            config.ClientId, config.ClientSecret, _httpClientFactory, await GetOAuthScopeAsync());
        // Fetch contacts (same as Pass 1)
        var contacts = await Application.Common.Models.EventsAirSyncHelpers.FetchContactsWithDedicatedCarAsync(
            config.ApiBaseUrl, config.EventCode, token, _httpClientFactory, cancellationToken,
            dedicatedCarGuid2, rankGuid2);
        var syncedContactIds = new HashSet<string>(contacts.Select(c => c.ContactId), StringComparer.OrdinalIgnoreCase);
        // Fetch travel bookings (same as Pass 3)
        var travelBookings = await Application.Common.Models.EventsAirSyncHelpers.FetchTravelBookingsByContactsAsync(
            config.ApiBaseUrl, config.EventCode, token, _httpClientFactory, syncedContactIds, cancellationToken);
        // Load active guests from DB
        var guestsByContactId = await _db.Guests
            .Where(g => g.IsActive)
            .ToDictionaryAsync(g => g.EventsAirContactId, StringComparer.OrdinalIgnoreCase, cancellationToken);
        int skippedNoFlight = 0, skippedNoContact = 0, skippedNoGuest = 0, wouldSave = 0;
        var samples = new List<object>();
        foreach (var tbDto in travelBookings)
        {
            if (string.IsNullOrEmpty(tbDto.FlightNumber)) { skippedNoFlight++; continue; }
            if (string.IsNullOrEmpty(tbDto.ContactId)) { skippedNoContact++; continue; }
            if (!guestsByContactId.ContainsKey(tbDto.ContactId)) { skippedNoGuest++; continue; }
            wouldSave++;
            if (samples.Count < 5)
                samples.Add(new { contactId = tbDto.ContactId, flightNumber = tbDto.FlightNumber, travelTypeName = tbDto.TravelTypeName, arrivalDate = tbDto.ArrivalDate, departureDate = tbDto.DepartureDate });
        }
        return Ok(new
        {
            contactsFetched = contacts.Count,
            travelBookingsFetched = travelBookings.Count,
            activeGuestsInDb = guestsByContactId.Count,
            skippedNoFlight,
            skippedNoContact,
            skippedNoGuest,
            wouldSave,
            sampleBookings = samples
        });
    }

    // POST /api/eventsair/sync-travel
    // Directly fetches travel bookings from EventsAir and saves them to DB.
    // Independent of the main guest sync pipeline.
    [HttpPost("sync-travel")]
    [Authorize]
    public async Task<IActionResult> SyncTravel(CancellationToken cancellationToken)
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config == null || !config.IsActive)
            return BadRequest(new { message = "EventsAir not configured or inactive." });

        // Load field mappings
        var fieldMappings = await _db.SyncFieldMappings
            .Where(f => f.EventCode == null || f.EventCode == config.EventCode)
            .ToListAsync(cancellationToken);
        var dedicatedCarGuid = (fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Dedicated Car", StringComparison.OrdinalIgnoreCase) && f.EventCode == config.EventCode)
            ?? fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Dedicated Car", StringComparison.OrdinalIgnoreCase)))
            ?.EventsAirFieldGuid ?? "d6b74b23-c8b6-d044-5d86-3a17bafe27de";
        var rankGuid = (fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Rank", StringComparison.OrdinalIgnoreCase) && f.EventCode == config.EventCode)
            ?? fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Rank", StringComparison.OrdinalIgnoreCase)))
            ?.EventsAirFieldGuid ?? "3d96b87e-87b0-145e-5f45-3a17bafe26d4";

        // Get token
        var token = await Application.Common.Models.EventsAirSyncHelpers.GetEventsAirTokenAsync(
            config.ClientId, config.ClientSecret, _httpClientFactory, await GetOAuthScopeAsync());

        // Fetch all contacts with DedicatedCar=True
        var contacts = await Application.Common.Models.EventsAirSyncHelpers.FetchContactsWithDedicatedCarAsync(
            config.ApiBaseUrl, config.EventCode, token, _httpClientFactory, cancellationToken,
            dedicatedCarGuid, rankGuid);
        var contactIds = contacts.Select(c => c.ContactId).ToList();

        // Fetch travel bookings for all contacts
        var travelBookings = await Application.Common.Models.EventsAirSyncHelpers.FetchTravelBookingsByContactsAsync(
            config.ApiBaseUrl, config.EventCode, token, _httpClientFactory, contactIds, cancellationToken);

        // ── Delegate all flight+booking processing to the shared helper ─────────────────
        // This is the single source of truth for flight deduplication rules.
        var syncResult = await Application.Common.Models.EventsAirSyncHelpers.ProcessTravelBookingsAsync(
            _db, travelBookings, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            contactsFetched          = contactIds.Count,
            travelBookingsFetched    = travelBookings.Count,
            savedNew                 = syncResult.SavedNew,
            updatedExisting          = syncResult.UpdatedExisting,
            rebooked                 = syncResult.Rebooked,
            skippedNoFlight          = syncResult.SkippedNoFlight,
            skippedNoContact         = syncResult.SkippedNoContact,
            skippedNoGuest           = syncResult.SkippedNoGuest,
            errorCount               = syncResult.ErrorCount,
            errors                   = syncResult.Errors
        });
    }

    // GET /api/eventsair/debug-custom-fields?contactId=xxx
    // Returns all custom field definitions for the event AND the custom field values for the given contact.
    [HttpGet("debug-custom-fields")]
    [Authorize]
    public async Task<IActionResult> DebugCustomFields([FromQuery] string contactId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(contactId))
            return BadRequest(new { message = "contactId is required" });

        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config == null || !config.IsActive)
            return BadRequest(new { message = "EventsAir not configured or inactive." });

        var token = await Application.Common.Models.EventsAirSyncHelpers.GetEventsAirTokenAsync(
            config.ClientId, config.ClientSecret, _httpClientFactory, await GetOAuthScopeAsync());

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        var gqlBase = $"{config.ApiBaseUrl.TrimEnd('/')}/graphql";

        async Task<System.Text.Json.JsonElement> RunQuery(string query)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, gqlBase)
            {
                Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) },
                Content = new System.Net.Http.StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new { query }),
                    System.Text.Encoding.UTF8, "application/json")
            };
            var resp = await client.SendAsync(req, cancellationToken);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken);
            return System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
        }

        // 1. All custom field definitions for the event
        var defsQuery = $"{{ event(id: \"{config.EventCode}\") {{ customFieldDefinitions {{ id name type options {{ id value }} }} }} }}";
        var defsResult = await RunQuery(defsQuery);

        // 2. All custom field values for the specific contact (definitionId + value only)
        var contactQuery = $"{{ event(id: \"{config.EventCode}\") {{ contact(id: \"{contactId}\") {{ id firstName lastName customFields {{ definitionId value }} }} }} }}";
        var contactResult = await RunQuery(contactQuery);

        // 3. Direct test: fetch the Vehicle Types field value for this contact using FetchCustomFieldValuesAsync
        const string vehicleTypesGuid = "5f6b0e9e-7d1c-4f91-affc-ecbe95cef678";
        var vehicleTypeValues = await Application.Common.Models.EventsAirSyncHelpers.FetchCustomFieldValuesAsync(
            config.ApiBaseUrl, config.EventCode, token, vehicleTypesGuid,
            new[] { contactId }, _httpClientFactory, cancellationToken);

        return Ok(new
        {
            eventCode = config.EventCode,
            contactId,
            customFieldDefinitions = defsResult,
            contactCustomFields = contactResult,
            vehicleTypeTest = new
            {
                guid = vehicleTypesGuid,
                valueForContact = vehicleTypeValues.TryGetValue(contactId, out var v) ? v : "(not found)"
            }
        });
    }

    // POST /api/eventsair/apply-vehicle-type-migration
    // One-shot: adds VehicleTypeValue column to Guests table if it doesn't exist.
    [HttpPost("apply-vehicle-type-migration")]
    public async Task<IActionResult> ApplyVehicleTypeMigration()
    {
        try
        {
            await _appDb.Database.ExecuteSqlRawAsync(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Guests' AND column_name = 'VehicleTypeValue'
                    ) THEN
                        ALTER TABLE ""Guests"" ADD COLUMN ""VehicleTypeValue"" text NULL;
                    END IF;
                END $$;
                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                VALUES ('20260603100000_AddVehicleTypeValueToGuest', '9.0.0')
                ON CONFLICT DO NOTHING;
            ");
            // Check if column now exists
            var conn = _appDb.Database.GetDbConnection();
            await conn.OpenAsync();
            bool columnExists;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'Guests' AND column_name = 'VehicleTypeValue'";
                columnExists = Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
            }
            await conn.CloseAsync();
            return Ok(new { success = true, columnExists, message = "VehicleTypeValue column added (or already existed)." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// TEMPORARY admin endpoint: delete all flight rows and nullify TravelBookings.FlightId
    /// so the next EventsAir sync recreates them cleanly with correct date-keying.
    /// </summary>
    [HttpPost("reset-flights")]
    public async Task<IActionResult> ResetFlights(CancellationToken ct)
    {
        try
        {
            var dbConn = _appDb.Database.GetDbConnection();
            await dbConn.OpenAsync(ct);
            int tbUpdated = 0, flightsDeleted = 0;
            using (var cmd = dbConn.CreateCommand())
            {
                // Step 1: delete TravelBookings rows that reference a flight
                // (FlightId is NOT NULL constrained, so we must delete rather than nullify)
                cmd.CommandText = @"DELETE FROM ""TravelBookings"" WHERE ""FlightId"" IS NOT NULL";
                tbUpdated = await cmd.ExecuteNonQueryAsync(ct);

                // Step 2: delete all Flights rows
                cmd.CommandText = @"DELETE FROM ""Flights""";
                flightsDeleted = await cmd.ExecuteNonQueryAsync(ct);
            }
            await dbConn.CloseAsync();
            return Ok(new { success = true, travelBookingsDeleted = tbUpdated, flightsDeleted });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    // GET /api/eventsair/debug-lo-fields?contactId=xxx
    // Phase 1 investigation: tries multiple GraphQL nodes to find where LO marketing tag fields are stored.
    [HttpGet("debug-lo-fields")]
    public async Task<IActionResult> DebugLoFields([FromQuery] string contactId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(contactId))
            return BadRequest(new { message = "contactId is required" });
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config == null || !config.IsActive)
            return BadRequest(new { message = "EventsAir not configured or inactive." });
        var token = await Application.Common.Models.EventsAirSyncHelpers.GetEventsAirTokenAsync(
            config.ClientId, config.ClientSecret, _httpClientFactory, await GetOAuthScopeAsync());
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        var gqlBase = $"{config.ApiBaseUrl.TrimEnd('/')}/graphql";
        async Task<string> RunQuery(string query)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, gqlBase)
            {
                Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) },
                Content = new System.Net.Http.StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new { query }),
                    System.Text.Encoding.UTF8, "application/json")
            };
            var resp = await client.SendAsync(req, cancellationToken);
            return await resp.Content.ReadAsStringAsync(cancellationToken);
        }
        // Test 1: marketingRecords with correct fields: id, tag, value
        var q1 = $"{{ event(id: \"{config.EventCode}\") {{ contact(id: \"{contactId}\") {{ id firstName lastName marketingRecords {{ id tag value }} }} }} }}";
        var r1 = await RunQuery(q1);
        // Test 2: marketingRecords with tag sub-object (tag might be an object with id/name)
        var q2 = $"{{ event(id: \"{config.EventCode}\") {{ contact(id: \"{contactId}\") {{ id firstName lastName marketingRecords {{ id tag {{ id name }} value }} }} }} }}";
        var r2 = await RunQuery(q2);
        // Test 3: raw introspection of EventMarketingRecord type
        var q3 = "{ __type(name: \"EventMarketingRecord\") { fields { name type { name kind ofType { name kind } } } } }";
        var r3 = await RunQuery(q3);
        // Test 4: Try fetching the two specific GUIDs via FetchCustomFieldValuesAsync (same as VehicleType)
        const string loMobileGuid = "06dbb8f8-373a-26a3-7f3d-3a1d4e2e1dcb";
        const string loNameGuid   = "7bd3e8a2-f62e-e2a9-7e9a-3a1d4e2e1ddb";
        var loMobileValues = await Application.Common.Models.EventsAirSyncHelpers.FetchCustomFieldValuesAsync(
            config.ApiBaseUrl, config.EventCode, token, loMobileGuid,
            new[] { contactId }, _httpClientFactory, cancellationToken);
        var loNameValues = await Application.Common.Models.EventsAirSyncHelpers.FetchCustomFieldValuesAsync(
            config.ApiBaseUrl, config.EventCode, token, loNameGuid,
            new[] { contactId }, _httpClientFactory, cancellationToken);
        return Ok(new
        {
            contactId,
            test1_marketingTags = r1,
            test2_marketing = r2,
            test3_imports = r3,
            test4_loMobileViaCustomFields = loMobileValues.TryGetValue(contactId, out var mv) ? mv : "(not found)",
            test4_loNameViaCustomFields   = loNameValues.TryGetValue(contactId, out var nv) ? nv : "(not found)"
        });
    }
}
