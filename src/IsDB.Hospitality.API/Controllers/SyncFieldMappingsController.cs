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

/// <summary>
/// Manages configurable EventsAir custom field sync filters (e.g. "Rank").
/// Each SyncFieldMapping represents one custom contact field in EventsAir,
/// identified by its GUID, and holds a set of selected values to filter on during sync.
/// </summary>
[Authorize(Roles = "Administrator")]
[Route("api/sync-field-mappings")]
public class SyncFieldMappingsController : ApiControllerBase
{
    private readonly IAppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public SyncFieldMappingsController(
        IAppDbContext context,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    // ── Field Mapping CRUD ────────────────────────────────────────────────────

    /// <summary>Get all field mappings with their selected values</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var mappings = await _context.SyncFieldMappings
            .Include(m => m.SelectedValues)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.DisplayName)
            .Select(m => new
            {
                m.Id,
                m.DisplayName,
                m.EventsAirFieldGuid,
                m.Description,
                m.SortOrder,
                m.CreatedAt,
                m.UpdatedAt,
                SelectedValues = m.SelectedValues
                    .OrderBy(v => v.Value)
                    .Select(v => new
                    {
                        v.Id,
                        v.Value,
                        v.IsSelectedForSync,
                        v.CreatedAt,
                        v.UpdatedAt
                    })
            })
            .ToListAsync();

        return Ok(mappings);
    }

    /// <summary>Create a new field mapping</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSyncFieldMappingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest(new { message = "DisplayName is required." });

        if (string.IsNullOrWhiteSpace(request.EventsAirFieldGuid))
            return BadRequest(new { message = "EventsAirFieldGuid is required." });

        var exists = await _context.SyncFieldMappings
            .AnyAsync(m => m.EventsAirFieldGuid == request.EventsAirFieldGuid.Trim());
        if (exists)
            return Conflict(new { message = $"A field mapping for GUID '{request.EventsAirFieldGuid}' already exists." });

        var mapping = new SyncFieldMapping
        {
            Id = Guid.NewGuid(),
            DisplayName = request.DisplayName.Trim(),
            EventsAirFieldGuid = request.EventsAirFieldGuid.Trim().ToLower(),
            Description = request.Description?.Trim(),
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.SyncFieldMappings.Add(mapping);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = mapping.Id }, mapping);
    }

    /// <summary>Update a field mapping's display name or description</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSyncFieldMappingRequest request)
    {
        var mapping = await _context.SyncFieldMappings.FindAsync(id);
        if (mapping == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            mapping.DisplayName = request.DisplayName.Trim();
        if (request.Description != null)
            mapping.Description = request.Description.Trim();
        if (!string.IsNullOrWhiteSpace(request.EventsAirFieldGuid))
            mapping.EventsAirFieldGuid = request.EventsAirFieldGuid.Trim().ToLower();
        if (request.SortOrder.HasValue)
            mapping.SortOrder = request.SortOrder.Value;
        mapping.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(mapping);
    }

    /// <summary>Delete a field mapping and all its selected values</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var mapping = await _context.SyncFieldMappings
            .Include(m => m.SelectedValues)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (mapping == null) return NotFound();

        _context.SyncFieldValues.RemoveRange(mapping.SelectedValues);
        _context.SyncFieldMappings.Remove(mapping);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── Value Discovery from EventsAir ────────────────────────────────────────

    /// <summary>
    /// Scan EventsAir contacts and return all unique values for this field's GUID.
    /// This populates the multi-select list on the Registration Types page.
    /// </summary>
    [HttpPost("{id}/load-values-from-eventsair")]
    public async Task<IActionResult> LoadValuesFromEventsAir(Guid id)
    {
        var mapping = await _context.SyncFieldMappings
            .Include(m => m.SelectedValues)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (mapping == null) return NotFound();

        var config = await _context.EventsAirConfigs
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (config == null || string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.EventCode))
            return BadRequest(new { message = "EventsAir integration is not configured." });

        string accessToken;
        try
        {
            accessToken = await GetEventsAirTokenAsync(config.ClientId, config.ClientSecret);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = $"Failed to authenticate with EventsAir: {ex.Message}" });
        }

        // Fetch all registrations and extract unique values for this custom field
        List<string> uniqueValues;
        try
        {
            uniqueValues = await FetchUniqueCustomFieldValuesAsync(
                config.ApiBaseUrl,
                accessToken,
                config.EventCode,
                mapping.EventsAirFieldGuid);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = $"Failed to fetch values from EventsAir: {ex.Message}" });
        }

        if (uniqueValues.Count == 0)
            return Ok(new { message = "No values found for this field in EventsAir.", added = 0, total = 0 });

        // Merge into SyncFieldValues — add new values, keep existing selections
        var existingValues = mapping.SelectedValues
            .ToDictionary(v => v.Value, StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach (var val in uniqueValues)
        {
            if (existingValues.ContainsKey(val)) continue;

            _context.SyncFieldValues.Add(new SyncFieldValue
            {
                Id = Guid.NewGuid(),
                SyncFieldMappingId = mapping.Id,
                Value = val,
                IsSelectedForSync = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            added++;
        }

        if (added > 0) await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"Loaded {uniqueValues.Count} unique value(s) from EventsAir. {added} new value(s) added.",
            added,
            total = uniqueValues.Count
        });
    }

    // ── Value Selection ───────────────────────────────────────────────────────

    /// <summary>
    /// Bulk update which values are selected for sync for a given field mapping.
    /// Pass the array of SyncFieldValue IDs that should be selected.
    /// </summary>
    [HttpPost("{id}/value-selection")]
    public async Task<IActionResult> UpdateValueSelection(Guid id, [FromBody] UpdateValueSelectionRequest request)
    {
        var mapping = await _context.SyncFieldMappings
            .Include(m => m.SelectedValues)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (mapping == null) return NotFound();

        foreach (var val in mapping.SelectedValues)
        {
            val.IsSelectedForSync = request.SelectedValueIds.Contains(val.Id);
            val.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var selectedCount = mapping.SelectedValues.Count(v => v.IsSelectedForSync);
        return Ok(new
        {
            message = $"Value selection updated. {selectedCount} value(s) selected for sync.",
            selectedCount
        });
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> GetEventsAirTokenAsync(string clientId, string clientSecret)
    {
        var cacheKey = $"eventsair_token_{clientId}";
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached != null)
            return cached;

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        const string azureAdTenantId = "dff76352-1ded-46e8-96a4-1a83718b2d3a";
        const string eventsAirScope = "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";
        var tokenEndpoint = $"https://login.microsoftonline.com/{azureAdTenantId}/oauth2/v2.0/token";

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("scope", eventsAirScope)
        });

        var response = await client.PostAsync(tokenEndpoint, form);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {body[..Math.Min(200, body.Length)]}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        var token = doc.GetProperty("access_token").GetString()!;
        var expiresIn = doc.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
        _cache.Set(cacheKey, token, TimeSpan.FromSeconds(expiresIn - 60));
        return token;
    }

    private async Task<List<string>> FetchUniqueCustomFieldValuesAsync(
        string baseUrl,
        string accessToken,
        string eventCode,
        string fieldGuid)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(120);

        var uniqueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int offset = 0;
        const int pageSize = 200;

        while (true)
        {
            // Query registrations with contact custom fields
            var query = $@"{{
                event(id: ""{eventCode}"") {{
                    registrations(limit: {pageSize}, offset: {offset}) {{
                        contact {{
                            customFields {{
                                field {{ id }}
                                value
                            }}
                        }}
                    }}
                }}
            }}";

            var body = JsonSerializer.Serialize(new { query });
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/graphql")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {json[..Math.Min(300, json.Length)]}");

            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            if (doc.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            {
                var msg = errors[0].TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
                throw new InvalidOperationException($"GraphQL error: {msg}");
            }

            var registrations = doc.GetProperty("data").GetProperty("event").GetProperty("registrations");
            int count = 0;

            foreach (var reg in registrations.EnumerateArray())
            {
                count++;
                if (!reg.TryGetProperty("contact", out var contact) || contact.ValueKind == JsonValueKind.Null)
                    continue;
                if (!contact.TryGetProperty("customFields", out var customFields) || customFields.ValueKind == JsonValueKind.Null)
                    continue;

                foreach (var cf in customFields.EnumerateArray())
                {
                    if (!cf.TryGetProperty("field", out var field) || field.ValueKind == JsonValueKind.Null)
                        continue;
                    var fieldId = field.TryGetProperty("id", out var fid) ? fid.GetString() ?? "" : "";
                    if (!string.Equals(fieldId, fieldGuid, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var value = cf.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null
                        ? v.GetString()
                        : null;
                    if (!string.IsNullOrWhiteSpace(value))
                        uniqueValues.Add(value!.Trim());
                }
            }

            if (count < pageSize) break;
            offset += pageSize;
        }

        return uniqueValues.OrderBy(v => v).ToList();
    }
}

public class CreateSyncFieldMappingRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string EventsAirFieldGuid { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; } = 0;
}

public class UpdateSyncFieldMappingRequest
{
    public string? DisplayName { get; set; }
    public string? EventsAirFieldGuid { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
}

public class UpdateValueSelectionRequest
{
    public List<Guid> SelectedValueIds { get; set; } = new();
}
