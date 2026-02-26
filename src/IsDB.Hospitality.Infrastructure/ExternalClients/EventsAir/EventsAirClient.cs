using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.Common.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace IsDB.Hospitality.Infrastructure.ExternalClients.EventsAir;

public class EventsAirOptions
{
    public string BaseUrl { get; set; } = "https://api.eventsair.com";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string EventCode { get; set; } = string.Empty;
    // EventsAir uses Microsoft Azure AD for authentication
    // Correct endpoint: https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token
    public string TokenUrl { get; set; } = "https://login.microsoftonline.com/dff76352-1ded-46e8-96a4-1a83718b2d3a/oauth2/v2.0/token";
}

public class EventsAirClient : IEventsAirClient
{
    private readonly HttpClient _httpClient;
    private readonly EventsAirOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EventsAirClient> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private const string TokenCacheKey = "eventsair_access_token";

    public EventsAirClient(
        HttpClient httpClient,
        IOptions<EventsAirOptions> options,
        IMemoryCache cache,
        ILogger<EventsAirClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
        _logger = logger;

        _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => !r.IsSuccessStatusCode && r.StatusCode != System.Net.HttpStatusCode.NotFound)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("EventsAir API retry {RetryCount} after {Delay}s. Reason: {Reason}",
                        retryCount, timespan.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });
    }

    private async Task<string> GetAccessTokenAsync()
    {
        if (_cache.TryGetValue(TokenCacheKey, out string? cachedToken) && cachedToken != null)
            return cachedToken;

        _logger.LogInformation("Acquiring new EventsAir access token...");

        var tokenRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", _options.ClientId),
            new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
            // EventsAir requires the Azure AD scope, not "api"
            new KeyValuePair<string, string>("scope", "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default")
        });

        var response = await _httpClient.PostAsync(_options.TokenUrl, tokenRequest);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<JsonElement>(content);

        var token = tokenData.GetProperty("access_token").GetString()!;
        var expiresIn = tokenData.GetProperty("expires_in").GetInt32();

        _cache.Set(TokenCacheKey, token, TimeSpan.FromSeconds(expiresIn - 60));
        return token;
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        var token = await GetAccessTokenAsync();
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content != null) request.Content = content;
        return request;
    }

    public async Task<List<EventsAirContactDto>> GetAllContactsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching all contacts from EventsAir for event: {EventCode}", _options.EventCode);

        var query = $$"""
        {
          "query": "query GetContacts($eventCode: String!) { contacts(eventCode: $eventCode) { contactId firstName lastName title organization designation nationality passportNumber photoUrl mobileNumber email customFields { fieldName value } } }",
          "variables": { "eventCode": "{{_options.EventCode}}" }
        }
        """;

        var request = await CreateAuthorizedRequestAsync(
            HttpMethod.Post,
            $"{_options.BaseUrl}/graphql",
            new StringContent(query, Encoding.UTF8, "application/json"));

        var response = await _retryPolicy.ExecuteAsync(() => _httpClient.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/graphql")
            {
                Headers = { Authorization = request.Headers.Authorization },
                Content = new StringContent(query, Encoding.UTF8, "application/json")
            }, cancellationToken));

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("EventsAir API returned {StatusCode}", response.StatusCode);
            return new List<EventsAirContactDto>();
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseContactsResponse(json);
    }

    public async Task<EventsAirContactDto?> GetContactByIdAsync(string contactId, CancellationToken cancellationToken = default)
    {
        var query = $$"""
        {
          "query": "query GetContact($eventCode: String!, $contactId: String!) { contact(eventCode: $eventCode, contactId: $contactId) { contactId firstName lastName title organization designation nationality passportNumber photoUrl mobileNumber email customFields { fieldName value } } }",
          "variables": { "eventCode": "{{_options.EventCode}}", "contactId": "{{contactId}}" }
        }
        """;

        var token = await GetAccessTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/graphql")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = new StringContent(query, Encoding.UTF8, "application/json")
        };

        var response = await _retryPolicy.ExecuteAsync(() => _httpClient.SendAsync(request, cancellationToken));
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var contacts = ParseContactsResponse(json);
        return contacts.FirstOrDefault();
    }

    public async Task<bool> UploadContactPhotoAsync(string contactId, byte[] photoData, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Get pre-signed upload URL
            var token = await GetAccessTokenAsync();
            var uploadUrlRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/api/contacts/{contactId}/photo-upload-url")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
                Content = new StringContent(JsonSerializer.Serialize(new { fileName }), Encoding.UTF8, "application/json")
            };

            var urlResponse = await _httpClient.SendAsync(uploadUrlRequest, cancellationToken);
            if (!urlResponse.IsSuccessStatusCode) return false;

            var urlJson = await urlResponse.Content.ReadAsStringAsync(cancellationToken);
            var urlData = JsonSerializer.Deserialize<JsonElement>(urlJson);
            var uploadUrl = urlData.GetProperty("uploadUrl").GetString()!;

            // Step 2: Upload to pre-signed URL
            var uploadContent = new ByteArrayContent(photoData);
            uploadContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            var uploadResponse = await _httpClient.PutAsync(uploadUrl, uploadContent, cancellationToken);

            return uploadResponse.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload photo for contact {ContactId}", contactId);
            return false;
        }
    }

    public async Task<List<EventsAirRegistrationTypeDto>> GetRegistrationTypesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching registration types from EventsAir for event: {EventCode}", _options.EventCode);

        if (string.IsNullOrWhiteSpace(_options.EventCode))
        {
            _logger.LogWarning("EventsAir EventCode is not configured. Cannot fetch registration types.");
            return new List<EventsAirRegistrationTypeDto>();
        }

        // EventsAir GraphQL: query event by ID (eventCode) → registrationSetup → registrationTypes
        var query = $$"""
        {
          "query": "query GetRegistrationTypes($eventId: ID!) { event(id: $eventId) { registrationSetup { registrationTypes(limit: 2000) { id name uniqueCode } } } }",
          "variables": { "eventId": "{{_options.EventCode}}" }
        }
        """;

        try
        {
            var token = await GetAccessTokenAsync();
            var response = await _retryPolicy.ExecuteAsync(() =>
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/graphql")
                {
                    Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) },
                    Content = new StringContent(query, Encoding.UTF8, "application/json")
                };
                return _httpClient.SendAsync(req, cancellationToken);
            });

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("EventsAir registration types API returned {StatusCode}", response.StatusCode);
                return new List<EventsAirRegistrationTypeDto>();
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseRegistrationTypesResponse(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch registration types from EventsAir");
            return new List<EventsAirRegistrationTypeDto>();
        }
    }

    private static List<EventsAirRegistrationTypeDto> ParseRegistrationTypesResponse(string json)
    {
        var result = new List<EventsAirRegistrationTypeDto>();
        try
        {
            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            // Check for GraphQL errors
            if (doc.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
                return result;

            var registrationTypes = doc
                .GetProperty("data")
                .GetProperty("event")
                .GetProperty("registrationSetup")
                .GetProperty("registrationTypes");

            foreach (var rt in registrationTypes.EnumerateArray())
            {
                result.Add(new EventsAirRegistrationTypeDto
                {
                    Id = rt.GetProperty("id").GetString() ?? string.Empty,
                    Name = rt.GetProperty("name").GetString() ?? string.Empty,
                    UniqueCode = rt.TryGetProperty("uniqueCode", out var uc) ? uc.GetString() : null
                });
            }
        }
        catch { /* Return empty list on parse error */ }
        return result;
    }

    private static List<EventsAirContactDto> ParseContactsResponse(string json)
    {
        var result = new List<EventsAirContactDto>();
        try
        {
            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            var contacts = doc.GetProperty("data").GetProperty("contacts");

            foreach (var c in contacts.EnumerateArray())
            {
                var dto = new EventsAirContactDto
                {
                    ContactId = c.GetProperty("contactId").GetString() ?? string.Empty,
                    FirstName = c.GetProperty("firstName").GetString() ?? string.Empty,
                    LastName = c.GetProperty("lastName").GetString() ?? string.Empty,
                    Title = c.TryGetProperty("title", out var t) ? t.GetString() : null,
                    Organization = c.TryGetProperty("organization", out var o) ? o.GetString() : null,
                    Designation = c.TryGetProperty("designation", out var d) ? d.GetString() : null,
                    Nationality = c.TryGetProperty("nationality", out var n) ? n.GetString() : null,
                    PassportNumber = c.TryGetProperty("passportNumber", out var p) ? p.GetString() : null,
                    PhotoUrl = c.TryGetProperty("photoUrl", out var ph) ? ph.GetString() : null,
                    MobileNumber = c.TryGetProperty("mobileNumber", out var m) ? m.GetString() : null,
                    Email = c.TryGetProperty("email", out var e) ? e.GetString() : null,
                };

                // Parse custom fields (DOCUMENT type workaround for IsCritical, RequiresAccessibility, flights)
                if (c.TryGetProperty("customFields", out var customFields))
                {
                    foreach (var field in customFields.EnumerateArray())
                    {
                        var fieldName = field.GetProperty("fieldName").GetString();
                        var value = field.TryGetProperty("value", out var v) ? v.GetString() : null;

                        switch (fieldName)
                        {
                            case "IsCritical": dto.IsCritical = value == "true"; break;
                            case "RequiresAccessibility": dto.RequiresAccessibility = value == "true"; break;
                            case "GroupCode": dto.GroupCode = value; break;
                            case "HotelName": dto.HotelName = value; break;
                            case "RoomNumber": dto.RoomNumber = value; break;
                            case "SpecialRequirements": dto.SpecialRequirements = value; break;
                            case "ArrivalFlightNumber": dto.ArrivalFlightNumber = value; break;
                            case "ArrivalFlightDate":
                                if (DateTime.TryParse(value, out var arrDate)) dto.ArrivalFlightDate = arrDate;
                                break;
                            case "DepartureFlightNumber": dto.DepartureFlightNumber = value; break;
                            case "DepartureFlightDate":
                                if (DateTime.TryParse(value, out var depDate)) dto.DepartureFlightDate = depDate;
                                break;
                        }
                    }
                }

                result.Add(dto);
            }
        }
        catch { /* Return empty list on parse error */ }
        return result;
    }

    /// <summary>
    /// Fetches all registrations for the configured event, filtered to only those whose
    /// registration type ID is in <paramref name="registrationTypeIds"/>.
    /// When the list is empty, ALL registrations are returned.
    /// Uses event.registrations[].contact + type fields.
    /// </summary>
    public async Task<List<EventsAirRegistrationDto>> GetRegistrationsByTypeAsync(
        IEnumerable<string> registrationTypeIds,
        CancellationToken cancellationToken = default)
    {
        var allowedIds = new HashSet<string>(registrationTypeIds, StringComparer.OrdinalIgnoreCase);
        _logger.LogInformation(
            "Fetching registrations from EventsAir for event {EventCode}. Filtering by {Count} registration type(s).",
            _options.EventCode, allowedIds.Count);

        var allRegistrations = new List<EventsAirRegistrationDto>();
        int offset = 0;
        const int pageSize = 2000;

        while (true)
        {
            var queryBody = JsonSerializer.Serialize(new
            {
                query = "query GetRegs($eventId: ID!, $limit: Int!, $offset: Int!) { event(id: $eventId) { registrations(limit: $limit, offset: $offset) { id type { id name uniqueCode } contact { id firstName lastName title jobTitle organizationName primaryEmail } } } }",
                variables = new { eventId = _options.EventCode, limit = pageSize, offset }
            });

            HttpResponseMessage response;
            try
            {
                var token = await GetAccessTokenAsync();
                response = await _retryPolicy.ExecuteAsync(() =>
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/graphql")
                    {
                        Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
                        Content = new StringContent(queryBody, Encoding.UTF8, "application/json")
                    };
                    return _httpClient.SendAsync(req, cancellationToken);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch registrations page (offset={Offset}) from EventsAir", offset);
                break;
            }

            if (!response.IsSuccessStatusCode) break;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var page = ParseRegistrationsResponse(json, allowedIds);

            allRegistrations.AddRange(page);

            // If we got fewer than a full page, we've reached the end
            if (page.Count < pageSize) break;
            offset += pageSize;
        }

        _logger.LogInformation("Fetched {Count} registrations from EventsAir after type filtering.", allRegistrations.Count);
        return allRegistrations;
    }

    private static List<EventsAirRegistrationDto> ParseRegistrationsResponse(
        string json,
        HashSet<string> allowedTypeIds)
    {
        var result = new List<EventsAirRegistrationDto>();
        try
        {
            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            if (doc.TryGetProperty("errors", out var errs) && errs.GetArrayLength() > 0)
                return result;

            var registrations = doc
                .GetProperty("data")
                .GetProperty("event")
                .GetProperty("registrations");

            foreach (var reg in registrations.EnumerateArray())
            {
                if (!reg.TryGetProperty("type", out var typeEl) || typeEl.ValueKind == JsonValueKind.Null)
                    continue;

                var typeId = typeEl.TryGetProperty("id", out var tidEl) ? tidEl.GetString() ?? string.Empty : string.Empty;

                // Filter: skip if we have a whitelist and this type is not in it
                if (allowedTypeIds.Count > 0 && !allowedTypeIds.Contains(typeId))
                    continue;

                if (!reg.TryGetProperty("contact", out var contactEl) || contactEl.ValueKind == JsonValueKind.Null)
                    continue;

                var contactId = contactEl.TryGetProperty("id", out var cidEl) ? cidEl.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrEmpty(contactId)) continue;

                result.Add(new EventsAirRegistrationDto
                {
                    RegistrationId = reg.TryGetProperty("id", out var ridEl) ? ridEl.GetString() ?? string.Empty : string.Empty,
                    ContactId = contactId,
                    FirstName = contactEl.TryGetProperty("firstName", out var fnEl) && fnEl.ValueKind != JsonValueKind.Null ? fnEl.GetString() ?? string.Empty : string.Empty,
                    LastName = contactEl.TryGetProperty("lastName", out var lnEl) && lnEl.ValueKind != JsonValueKind.Null ? lnEl.GetString() ?? string.Empty : string.Empty,
                    Title = contactEl.TryGetProperty("title", out var titEl) && titEl.ValueKind != JsonValueKind.Null ? titEl.GetString() : null,
                    JobTitle = contactEl.TryGetProperty("jobTitle", out var jtEl) && jtEl.ValueKind != JsonValueKind.Null ? jtEl.GetString() : null,
                    OrganizationName = contactEl.TryGetProperty("organizationName", out var orgEl) && orgEl.ValueKind != JsonValueKind.Null ? orgEl.GetString() : null,
                    PrimaryEmail = contactEl.TryGetProperty("primaryEmail", out var emailEl) && emailEl.ValueKind != JsonValueKind.Null ? emailEl.GetString() : null,
                    RegistrationTypeId = typeId,
                    RegistrationTypeName = typeEl.TryGetProperty("name", out var tnEl) && tnEl.ValueKind != JsonValueKind.Null ? tnEl.GetString() ?? string.Empty : string.Empty,
                    RegistrationTypeCode = typeEl.TryGetProperty("uniqueCode", out var tcEl) && tcEl.ValueKind != JsonValueKind.Null ? tcEl.GetString() : null,
                });
            }
        }
        catch { /* Return empty list on parse error */ }
        return result;
    }
}
