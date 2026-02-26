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
}
