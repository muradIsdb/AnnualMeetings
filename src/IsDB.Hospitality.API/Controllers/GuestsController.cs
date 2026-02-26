using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Application.DTOs.Guests;
using IsDB.Hospitality.Application.Features.Guests.Commands;
using IsDB.Hospitality.Application.Features.Guests.Queries;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IsDB.Hospitality.API.Controllers;

[Authorize]
public class GuestsController : ApiControllerBase
{
    // In-memory store for background sync job status
    private static readonly ConcurrentDictionary<string, SyncJobStatus> _syncJobs = new();

    [HttpGet]
    public async Task<ActionResult<List<GuestSummaryDto>>> GetGuests(
        [FromQuery] GuestStatus? status = null,
        [FromQuery] bool? isCritical = null)
    {
        var result = await Mediator.Send(new GetGuestsQuery(status, isCritical));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GuestDto>> GetGuest(Guid id)
    {
        var result = await Mediator.Send(new GetGuestByIdQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        var success = await Mediator.Send(new UpdateGuestStatusCommand(id, request.Status, request.Notes));
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:guid}/checklist/{checklistItemId:guid}")]
    public async Task<IActionResult> CompleteChecklistItem(Guid id, Guid checklistItemId, [FromBody] CompleteChecklistRequest? request = null)
    {
        var success = await Mediator.Send(new CompleteChecklistItemCommand(id, checklistItemId, CurrentUserId, request?.Notes));
        if (!success) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// GET /api/guests/inactive
    /// Returns all guests marked as IsActive = false.
    /// </summary>
    [HttpGet("inactive")]
    public async Task<IActionResult> GetInactiveGuests([FromServices] AppDbContext db, CancellationToken ct)
    {
        var guests = await db.Guests
            .Where(g => !g.IsActive)
            .OrderBy(g => g.LastName).ThenBy(g => g.FirstName)
            .Select(g => new
            {
                g.Id,
                g.FirstName,
                g.LastName,
                g.RegistrationTypeName,
                g.Organization,
                g.Email,
                g.LastSyncedAt
            })
            .ToListAsync(ct);
        return Ok(guests);
    }

    /// <summary>
    /// DELETE /api/guests/inactive/all
    /// Permanently deletes all inactive guests.
    /// </summary>
    [HttpDelete("inactive/all")]
    public async Task<IActionResult> DeleteAllInactiveGuests([FromServices] AppDbContext db, CancellationToken ct)
    {
        var inactive = await db.Guests.Where(g => !g.IsActive).ToListAsync(ct);
        var count = inactive.Count;
        db.Guests.RemoveRange(inactive);
        await db.SaveChangesAsync(ct);
        return Ok(new { message = $"{count} inactive participant(s) permanently deleted.", deleted = count });
    }

    /// <summary>
    /// DELETE /api/guests/{id}
    /// Permanently deletes a single guest.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteGuest(Guid id, [FromServices] AppDbContext db, CancellationToken ct)
    {
        var guest = await db.Guests.FindAsync(new object[] { id }, ct);
        if (guest == null) return NotFound();
        db.Guests.Remove(guest);
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Participant permanently deleted." });
    }

    /// <summary>
    /// POST /api/guests/sync-from-eventsair
    /// Starts a background sync of participants from EventsAir.
    /// Fetches participants whose Registration Type is selected for sync OR whose custom field
    /// values (e.g. Rank) match the selected values — whichever applies (OR logic).
    /// Returns immediately with a jobId. Poll GET /api/guests/sync-status/{jobId} for progress.
    /// </summary>
    [HttpPost("sync-from-eventsair")]
    public async Task<IActionResult> SyncFromEventsAir(
        [FromServices] AppDbContext db,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] IMemoryCache cache,
        [FromServices] IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        // 1. Validate config
        var config = await db.EventsAirConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config == null || !config.IsActive || string.IsNullOrWhiteSpace(config.ClientId))
        {
            return BadRequest(new
            {
                message = "EventsAir integration is not configured or inactive. Please configure it first."
            });
        }

        // 2. Get selected registration type EventsAir IDs
        var selectedTypeIds = await db.RegistrationTypes
            .Where(rt => rt.IsSelectedForSync && rt.EventsAirId != null)
            .Select(rt => rt.EventsAirId!)
            .ToListAsync(cancellationToken);

        // 3. Get selected custom field values per field mapping (e.g. Rank)
        var fieldFilters = await db.SyncFieldMappings
            .Include(m => m.SelectedValues)
            .Where(m => m.SelectedValues.Any(v => v.IsSelectedForSync))
            .Select(m => new FieldFilter
            {
                FieldGuid = m.EventsAirFieldGuid,
                SelectedValues = m.SelectedValues
                    .Where(v => v.IsSelectedForSync)
                    .Select(v => v.Value)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        if (selectedTypeIds.Count == 0 && fieldFilters.Count == 0)
        {
            return BadRequest(new
            {
                message = "No registration types or custom field values are selected for sync. Please select at least one filter in the Registration Types page."
            });
        }

        // 4. Create job and start background sync
        var jobId = Guid.NewGuid().ToString("N");
        var job = new SyncJobStatus { JobId = jobId, State = "running", StartedAt = DateTime.UtcNow };
        _syncJobs[jobId] = job;

        // Snapshot config values for background thread
        var clientId = config.ClientId;
        var clientSecret = config.ClientSecret;
        var eventCode = config.EventCode;
        var apiBaseUrl = config.ApiBaseUrl;
        var typeIds = selectedTypeIds.ToList();
        var filters = fieldFilters.ToList();

        _ = Task.Run(async () =>
        {
            try
            {
                // Get token
                var token = await GetEventsAirTokenAsync(clientId, clientSecret, httpClientFactory, cache);

                // Fetch all registrations with OR filter
                var allowedTypeIds = new HashSet<string>(typeIds, StringComparer.OrdinalIgnoreCase);
                var registrations = await FetchRegistrationsFromEventsAirAsync(
                    apiBaseUrl, eventCode, token, allowedTypeIds, filters, httpClientFactory, CancellationToken.None);

                job.TotalFetched = registrations.Count;

                if (registrations.Count == 0)
                {
                    job.State = "done";
                    job.Message = "No participants found in EventsAir matching the selected filters.";
                    job.Added = 0;
                    job.Updated = 0;
                    return;
                }

                // Upsert in a new scope (background thread needs its own DbContext)
                using var scope = scopeFactory.CreateScope();
                var bgDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                int added = 0, updated = 0;
                foreach (var reg in registrations)
                {
                    if (string.IsNullOrEmpty(reg.ContactId)) continue;

                    var existing = await bgDb.Guests
                        .FirstOrDefaultAsync(g => g.EventsAirContactId == reg.ContactId);

                    if (existing == null)
                    {
                        bgDb.Guests.Add(new Guest
                        {
                            EventsAirContactId = reg.ContactId,
                            FirstName = reg.FirstName,
                            LastName = reg.LastName,
                            Title = reg.Title,
                            Designation = reg.JobTitle,
                            Organization = reg.OrganizationName,
                            Email = reg.PrimaryEmail,
                            RegistrationTypeId = reg.RegistrationTypeId,
                            RegistrationTypeName = reg.RegistrationTypeName,
                            IsActive = true,
                            Status = GuestStatus.Expected,
                            LastSyncedAt = DateTime.UtcNow
                        });
                        added++;
                    }
                    else
                    {
                        bool changed = false;
                        if (existing.FirstName != reg.FirstName) { existing.FirstName = reg.FirstName; changed = true; }
                        if (existing.LastName != reg.LastName) { existing.LastName = reg.LastName; changed = true; }
                        if (existing.Designation != reg.JobTitle) { existing.Designation = reg.JobTitle; changed = true; }
                        if (existing.Organization != reg.OrganizationName) { existing.Organization = reg.OrganizationName; changed = true; }
                        if (existing.RegistrationTypeName != reg.RegistrationTypeName) { existing.RegistrationTypeName = reg.RegistrationTypeName; changed = true; }
                        if (existing.RegistrationTypeId != reg.RegistrationTypeId) { existing.RegistrationTypeId = reg.RegistrationTypeId; changed = true; }
                        // Reactivate if previously deactivated
                        if (!existing.IsActive) { existing.IsActive = true; changed = true; }
                        if (changed) { existing.LastSyncedAt = DateTime.UtcNow; updated++; }
                    }
                }

                await bgDb.SaveChangesAsync();

                job.Added = added;
                job.Updated = updated;
                job.State = "done";
                job.Message = $"Sync complete. {added} new participant(s) added, {updated} updated.";
            }
            catch (Exception ex)
            {
                job.State = "error";
                job.Message = $"Sync failed: {ex.Message}";
            }
            finally
            {
                job.FinishedAt = DateTime.UtcNow;
            }
        });

        return Accepted(new
        {
            jobId,
            message = "Sync started in background. Poll /api/guests/sync-status/{jobId} for progress.",
            selectedTypeCount = typeIds.Count,
            fieldFilterCount = filters.Count
        });
    }

    /// <summary>
    /// GET /api/guests/sync-status/{jobId}
    /// Returns the current status of a background sync job.
    /// </summary>
    [HttpGet("sync-status/{jobId}")]
    public IActionResult GetSyncStatus(string jobId)
    {
        if (!_syncJobs.TryGetValue(jobId, out var job))
            return NotFound(new { message = "Job not found." });

        return Ok(new
        {
            job.JobId,
            job.State,
            job.Message,
            job.Added,
            job.Updated,
            job.TotalFetched,
            job.StartedAt,
            job.FinishedAt
        });
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static async Task<string> GetEventsAirTokenAsync(
        string clientId,
        string clientSecret,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache)
    {
        var cacheKey = $"eventsair_token_{clientId}";
        if (cache.TryGetValue(cacheKey, out string? cached) && cached != null)
            return cached;

        const string azureAdTenantId = "dff76352-1ded-46e8-96a4-1a83718b2d3a";
        const string eventsAirScope = "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";
        var tokenEndpoint = $"https://login.microsoftonline.com/{azureAdTenantId}/oauth2/v2.0/token";

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

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
            throw new InvalidOperationException(
                $"HTTP {(int)response.StatusCode}: {body[..Math.Min(200, body.Length)]}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        var token = doc.GetProperty("access_token").GetString()!;
        var expiresIn = doc.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        cache.Set(cacheKey, token, TimeSpan.FromSeconds(expiresIn - 60));
        return token;
    }

    /// <summary>
    /// Fetches registrations from EventsAir applying OR logic:
    /// Include a registration if its type ID is in allowedTypeIds
    /// OR if any of its contact's custom field values match a configured filter.
    /// </summary>
    private static async Task<List<EventsAirRegistrationRaw>> FetchRegistrationsFromEventsAirAsync(
        string baseUrl,
        string eventCode,
        string accessToken,
        HashSet<string> allowedTypeIds,
        List<FieldFilter> fieldFilters,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var result = new List<EventsAirRegistrationRaw>();
        var seenContactIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        int offset = 0;
        const int pageSize = 200;
        bool hasFieldFilters = fieldFilters.Count > 0;

        while (true)
        {
            // Build query — include customFields only if we have field filters to apply
            string customFieldsFragment = hasFieldFilters
                ? "customFields { field { id } value }"
                : string.Empty;

            var queryBody = JsonSerializer.Serialize(new
            {
                query = $"{{ event(id: \"{eventCode}\") {{ registrations(limit: {pageSize}, offset: {offset}) {{ id type {{ id name uniqueCode }} contact {{ id firstName lastName title jobTitle organizationName primaryEmail {customFieldsFragment} }} }} }} }}"
            });

            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/graphql")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
                Content = new StringContent(queryBody, Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(req, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {json[..Math.Min(300, json.Length)]}");

            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            if (doc.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            {
                var firstError = errors[0].TryGetProperty("message", out var msg)
                    ? msg.GetString()
                    : "Unknown GraphQL error";
                throw new InvalidOperationException($"EventsAir GraphQL error: {firstError}");
            }

            var registrations = doc
                .GetProperty("data")
                .GetProperty("event")
                .GetProperty("registrations");

            int pageCount = 0;
            foreach (var reg in registrations.EnumerateArray())
            {
                pageCount++;

                if (!reg.TryGetProperty("contact", out var contactEl) || contactEl.ValueKind == JsonValueKind.Null)
                    continue;

                var contactId = contactEl.TryGetProperty("id", out var cidEl) ? cidEl.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrEmpty(contactId) || !seenContactIds.Add(contactId))
                    continue;

                // Get registration type
                string typeId = string.Empty;
                string typeName = string.Empty;
                if (reg.TryGetProperty("type", out var typeEl) && typeEl.ValueKind != JsonValueKind.Null)
                {
                    typeId = typeEl.TryGetProperty("id", out var tidEl) ? tidEl.GetString() ?? string.Empty : string.Empty;
                    typeName = typeEl.TryGetProperty("name", out var tn) ? tn.GetString() ?? string.Empty : string.Empty;
                }

                // OR filter: include if type matches OR if any custom field value matches
                bool matchesType = allowedTypeIds.Count > 0 && allowedTypeIds.Contains(typeId);
                bool matchesField = false;

                if (!matchesType && hasFieldFilters)
                {
                    // Check custom fields
                    if (contactEl.TryGetProperty("customFields", out var customFields) && customFields.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var cf in customFields.EnumerateArray())
                        {
                            if (!cf.TryGetProperty("field", out var field) || field.ValueKind == JsonValueKind.Null) continue;
                            var fieldId = field.TryGetProperty("id", out var fid) ? fid.GetString() ?? "" : "";
                            var value = cf.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;
                            if (string.IsNullOrWhiteSpace(value)) continue;

                            foreach (var filter in fieldFilters)
                            {
                                if (string.Equals(fieldId, filter.FieldGuid, StringComparison.OrdinalIgnoreCase) &&
                                    filter.SelectedValues.Any(sv => string.Equals(sv, value, StringComparison.OrdinalIgnoreCase)))
                                {
                                    matchesField = true;
                                    break;
                                }
                            }
                            if (matchesField) break;
                        }
                    }
                }

                if (!matchesType && !matchesField) continue;

                result.Add(new EventsAirRegistrationRaw(
                    ContactId: contactId,
                    FirstName: contactEl.TryGetProperty("firstName", out var fn) ? fn.GetString() ?? string.Empty : string.Empty,
                    LastName: contactEl.TryGetProperty("lastName", out var ln) ? ln.GetString() ?? string.Empty : string.Empty,
                    Title: contactEl.TryGetProperty("title", out var t) && t.ValueKind != JsonValueKind.Null ? t.GetString() : null,
                    JobTitle: contactEl.TryGetProperty("jobTitle", out var jt) && jt.ValueKind != JsonValueKind.Null ? jt.GetString() : null,
                    OrganizationName: contactEl.TryGetProperty("organizationName", out var org) && org.ValueKind != JsonValueKind.Null ? org.GetString() : null,
                    PrimaryEmail: contactEl.TryGetProperty("primaryEmail", out var em) && em.ValueKind != JsonValueKind.Null ? em.GetString() : null,
                    RegistrationTypeId: typeId,
                    RegistrationTypeName: typeName
                ));
            }

            if (pageCount < pageSize) break;
            offset += pageSize;
        }

        return result;
    }

    private record EventsAirRegistrationRaw(
        string ContactId,
        string FirstName,
        string LastName,
        string? Title,
        string? JobTitle,
        string? OrganizationName,
        string? PrimaryEmail,
        string RegistrationTypeId,
        string RegistrationTypeName
    );

    private class SyncJobStatus
    {
        public string JobId { get; set; } = string.Empty;
        public string State { get; set; } = "pending"; // running | done | error
        public string Message { get; set; } = string.Empty;
        public int Added { get; set; }
        public int Updated { get; set; }
        public int TotalFetched { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
    }

    private class FieldFilter
    {
        public string FieldGuid { get; set; } = string.Empty;
        public List<string> SelectedValues { get; set; } = new();
    }
}

public record UpdateStatusRequest(GuestStatus Status, string? Notes = null);
public record CompleteChecklistRequest(string? Notes = null);
