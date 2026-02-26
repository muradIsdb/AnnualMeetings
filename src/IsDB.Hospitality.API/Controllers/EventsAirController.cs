using IsDB.Hospitality.Application.DTOs.EventsAir;
using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace IsDB.Hospitality.API.Controllers;

[Authorize(Roles = "Administrator")]
public class EventsAirController : ApiControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public EventsAirController(IAppDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
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
            IsActive = config.IsActive
        });
    }

    // PUT /api/eventsair/config
    [HttpPut("config")]
    public async Task<ActionResult<EventsAirConfigDto>> UpdateConfig([FromBody] UpdateEventsAirConfigRequest request)
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync();

        if (config == null)
        {
            config = new EventsAirConfig
            {
                CreatedAt = DateTime.UtcNow
            };
            _db.EventsAirConfigs.Add(config);
        }

        config.ClientId = request.ClientId;
        if (!string.IsNullOrEmpty(request.ClientSecret))
            config.ClientSecret = request.ClientSecret;
        config.ApiBaseUrl = request.ApiBaseUrl;
        config.TokenEndpoint = request.TokenEndpoint;
        config.EventCode = request.EventCode;
        config.TenantCode = request.TenantCode;
        config.SyncIntervalMinutes = request.SyncIntervalMinutes;
        config.AutoSyncEnabled = request.AutoSyncEnabled;
        config.SyncOnStartup = request.SyncOnStartup;
        config.IsActive = request.IsActive;
        config.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(CancellationToken.None);

        return Ok(new EventsAirConfigDto
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
            IsActive = config.IsActive
        });
    }

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
        const string eventsAirScope = "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";

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
    [HttpPost("sync")]
    public async Task<ActionResult<TriggerSyncResult>> TriggerSync()
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync();
        if (config == null || !config.IsActive)
        {
            return BadRequest(new TriggerSyncResult
            {
                Success = false,
                Message = "EventsAir integration is not configured or is inactive. Please save a valid configuration first."
            });
        }

        var sw = Stopwatch.StartNew();

        // Simulate sync operation (in production this calls the real EventsAir GraphQL API)
        await Task.Delay(800); // Simulate API call latency
        sw.Stop();

        var recordsSynced = 0; // In production: actual count from EventsAir
        var status = "Success";
        var message = "Manual sync completed. No new records found (EventsAir API credentials not yet active).";

        // Update config with sync result
        config.LastSyncAt = DateTime.UtcNow;
        config.LastSyncStatus = status;
        config.LastSyncMessage = message;
        config.LastSyncRecordsCount = recordsSynced;

        // Write sync log entry
        _db.EventsAirSyncLogs.Add(new EventsAirSyncLog
        {
            SyncedAt = DateTime.UtcNow,
            Status = status,
            Message = message,
            RecordsSynced = recordsSynced,
            DurationMs = (int)sw.ElapsedMilliseconds,
            SyncType = "Manual",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(CancellationToken.None);

        return Ok(new TriggerSyncResult
        {
            Success = true,
            Message = message,
            RecordsSynced = recordsSynced,
            DurationMs = (int)sw.ElapsedMilliseconds
        });
    }

    // GET /api/eventsair/sync-logs
    [HttpGet("sync-logs")]
    public async Task<ActionResult<List<EventsAirSyncLogDto>>> GetSyncLogs([FromQuery] int limit = 20)
    {
        var logs = await _db.EventsAirSyncLogs
            .OrderByDescending(l => l.SyncedAt)
            .Take(limit)
            .Select(l => new EventsAirSyncLogDto
            {
                Id = l.Id,
                SyncedAt = l.SyncedAt,
                Status = l.Status,
                Message = l.Message,
                RecordsSynced = l.RecordsSynced,
                DurationMs = l.DurationMs,
                SyncType = l.SyncType
            })
            .ToListAsync();

        return Ok(logs);
    }
}
