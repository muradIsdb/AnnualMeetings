using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IsDB.Hospitality.API.Controllers;

[Authorize(Roles = "Administrator")]
[Route("api/registration-types")]
public class RegistrationTypesController : ApiControllerBase
{
    private readonly IAppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public RegistrationTypesController(
        IAppDbContext context,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    /// <summary>
    /// Get all registration types
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var types = await _context.RegistrationTypes
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Code,
                r.Name,
                r.Description,
                r.IsSelectedForSync,
                r.IsFromEventsAir,
                r.SortOrder,
                r.CreatedAt,
                r.UpdatedAt
            })
            .ToListAsync();

        return Ok(types);
    }

    /// <summary>
    /// Get only registration types selected for sync
    /// </summary>
    [HttpGet("selected")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSelected()
    {
        var types = await _context.RegistrationTypes
            .Where(r => r.IsSelectedForSync)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .Select(r => new { r.Id, r.Code, r.Name })
            .ToListAsync();

        return Ok(types);
    }

    /// <summary>
    /// Add a new registration type manually
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRegistrationTypeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        var code = string.IsNullOrWhiteSpace(request.Code)
            ? request.Name.ToUpperInvariant().Replace(" ", "_")
            : request.Code.ToUpperInvariant();

        var exists = await _context.RegistrationTypes.AnyAsync(r => r.Code == code);
        if (exists)
            return Conflict(new { message = $"Registration type with code '{code}' already exists." });

        var regType = new RegistrationType
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsSelectedForSync = request.IsSelectedForSync,
            IsFromEventsAir = false,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.RegistrationTypes.Add(regType);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = regType.Id }, regType);
    }

    /// <summary>
    /// Update a registration type
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRegistrationTypeRequest request)
    {
        var regType = await _context.RegistrationTypes.FindAsync(id);
        if (regType == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
            regType.Name = request.Name.Trim();

        if (request.Description != null)
            regType.Description = request.Description.Trim();

        if (request.SortOrder.HasValue)
            regType.SortOrder = request.SortOrder.Value;

        regType.IsSelectedForSync = request.IsSelectedForSync;
        regType.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(regType);
    }

    /// <summary>
    /// Delete a registration type
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var regType = await _context.RegistrationTypes.FindAsync(id);
        if (regType == null) return NotFound();

        _context.RegistrationTypes.Remove(regType);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Bulk update sync selection — pass array of IDs that should be selected
    /// </summary>
    [HttpPost("sync-selection")]
    public async Task<IActionResult> UpdateSyncSelection([FromBody] UpdateSyncSelectionRequest request)
    {
        var allTypes = await _context.RegistrationTypes.ToListAsync();

        foreach (var regType in allTypes)
        {
            regType.IsSelectedForSync = request.SelectedIds.Contains(regType.Id);
            regType.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var selectedCount = allTypes.Count(r => r.IsSelectedForSync);
        return Ok(new { message = $"Sync selection updated. {selectedCount} type(s) selected for sync.", selectedCount });
    }

    /// <summary>
    /// Import registration types from EventsAir using stored DB credentials.
    /// Calls the EventsAir GraphQL API: event(id) → registrationSetup → registrationTypes
    /// </summary>
    [HttpPost("import-from-eventsair")]
    public async Task<IActionResult> ImportFromEventsAir()
    {
        // Load credentials from the database (not from appsettings.json)
        var config = await _context.EventsAirConfigs
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (config == null || string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.EventCode))
            return BadRequest(new
            {
                message = "EventsAir integration is not configured. Please save your Client ID, Client Secret, and Event Code on the EventsAir Config page first."
            });

        if (string.IsNullOrWhiteSpace(config.ClientSecret))
            return BadRequest(new
            {
                message = "EventsAir Client Secret is missing. Please re-save your credentials on the EventsAir Config page."
            });

        // Step 1: Obtain OAuth2 access token using stored credentials
        string accessToken;
        try
        {
            accessToken = await GetEventsAirTokenAsync(
                config.TokenEndpoint,
                config.ClientId,
                config.ClientSecret);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new
            {
                message = $"Failed to authenticate with EventsAir: {ex.Message}",
                hint = "Please verify your Client ID and Client Secret on the EventsAir Config page and ensure the connection test passes."
            });
        }

        // Step 2: Query EventsAir GraphQL for registration types
        // We query event.registrations[].type to extract unique registration types,
        // as the API key has access to registrations but not to event.setup.registration.registrationTypes
        var query = new
        {
            query = "query GetRegTypes($eventId: ID!) { event(id: $eventId) { registrations(limit: 2000) { id type { id name uniqueCode } } } }",
            variables = new { eventId = config.EventCode }
        };

        List<EventsAirRegTypeRaw> eventsAirTypes;
        try
        {
            eventsAirTypes = await FetchRegistrationTypesFromGraphQLAsync(
                config.ApiBaseUrl,
                accessToken,
                query);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new
            {
                message = $"Failed to fetch registration types from EventsAir: {ex.Message}",
                hint = "Please verify your Event Code and API Base URL on the EventsAir Config page."
            });
        }

        if (eventsAirTypes.Count == 0)
            return Ok(new
            {
                message = "No registration types were returned from EventsAir. The event may have no registration types defined yet, or the Event Code may be incorrect.",
                imported = 0,
                skipped = 0,
                total = 0
            });

        // Step 3: Merge into local database (add new, skip existing by code)
        var existingCodes = (await _context.RegistrationTypes
            .Select(r => r.Code)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int imported = 0, skipped = 0;
        int sortOrder = await _context.RegistrationTypes.AnyAsync()
            ? await _context.RegistrationTypes.MaxAsync(r => r.SortOrder) + 1
            : 0;

        foreach (var ea in eventsAirTypes)
        {
            var code = !string.IsNullOrWhiteSpace(ea.UniqueCode)
                ? ea.UniqueCode!.ToUpperInvariant()
                : ea.Name.ToUpperInvariant().Replace(" ", "_").Replace("-", "_");

            if (existingCodes.Contains(code))
            {
                skipped++;
                continue;
            }

            _context.RegistrationTypes.Add(new RegistrationType
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = ea.Name,
                Description = $"Imported from EventsAir (ID: {ea.Id})",
                EventsAirId = ea.Id,
                IsSelectedForSync = false,
                IsFromEventsAir = true,
                SortOrder = sortOrder++,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            existingCodes.Add(code);
            imported++;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"Import complete. {imported} new type(s) added from EventsAir, {skipped} already existed.",
            imported,
            skipped,
            total = eventsAirTypes.Count
        });
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<string> GetEventsAirTokenAsync(string tokenEndpoint, string clientId, string clientSecret)
    {
        var cacheKey = $"eventsair_token_{clientId}";
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached != null)
            return cached;

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        // EventsAir uses Microsoft Azure AD OAuth2 with a specific scope
        // The correct endpoint is: https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token
        // The correct scope is: https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default
        const string azureAdTenantId = "dff76352-1ded-46e8-96a4-1a83718b2d3a";
        const string eventsAirScope = "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";
        var actualTokenEndpoint = $"https://login.microsoftonline.com/{azureAdTenantId}/oauth2/v2.0/token";

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("scope", eventsAirScope)
        });

        var response = await client.PostAsync(actualTokenEndpoint, form);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {body[..Math.Min(200, body.Length)]}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        var token = doc.GetProperty("access_token").GetString()!;
        var expiresIn = doc.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        _cache.Set(cacheKey, token, TimeSpan.FromSeconds(expiresIn - 60));
        return token;
    }

    private async Task<List<EventsAirRegTypeRaw>> FetchRegistrationTypesFromGraphQLAsync(
        string baseUrl,
        string accessToken,
        object query)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var body = JsonSerializer.Serialize(query);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/graphql")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"HTTP {(int)response.StatusCode}: {json[..Math.Min(300, json.Length)]}");

        var result = new List<EventsAirRegTypeRaw>();
        var doc = JsonSerializer.Deserialize<JsonElement>(json);

        // Surface GraphQL-level errors
        if (doc.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
        {
            var firstError = errors[0].TryGetProperty("message", out var msg)
                ? msg.GetString()
                : "Unknown GraphQL error";
            throw new InvalidOperationException($"EventsAir GraphQL error: {firstError}");
        }

        // Parse registrations and extract unique registration types by ID
        var registrations = doc
            .GetProperty("data")
            .GetProperty("event")
            .GetProperty("registrations");

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reg in registrations.EnumerateArray())
        {
            if (!reg.TryGetProperty("type", out var rt) || rt.ValueKind == JsonValueKind.Null)
                continue;

            var id = rt.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(id) || !seenIds.Add(id))
                continue;

            result.Add(new EventsAirRegTypeRaw
            {
                Id = id,
                Name = rt.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty,
                UniqueCode = rt.TryGetProperty("uniqueCode", out var uc) && uc.ValueKind != JsonValueKind.Null
                    ? uc.GetString()
                    : null
            });
        }

        return result;
    }

    private record EventsAirRegTypeRaw(string Id = "", string Name = "", string? UniqueCode = null);
}

public class CreateRegistrationTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool IsSelectedForSync { get; set; } = false;
    public int SortOrder { get; set; } = 0;
}

public class UpdateRegistrationTypeRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsSelectedForSync { get; set; }
    public int? SortOrder { get; set; }
}

public class UpdateSyncSelectionRequest
{
    public List<Guid> SelectedIds { get; set; } = new();
}
