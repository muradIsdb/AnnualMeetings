using System.Text.Json;
using IsDB.Hospitality.Application.Common.Helpers;
using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace IsDB.Hospitality.Infrastructure.ExternalClients.FlightTracker;

public class AviationstackOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "http://api.aviationstack.com/v1";
    public int SyncIntervalMinutes { get; set; } = 5;
    public int TrackingWindowHours { get; set; } = 12;
    /// <summary>Maximum day difference allowed between AviationStack result and DB flight date. Default 1.</summary>
    public int DateGuardDays { get; set; } = 1;
}

public class AviationstackClient : IFlightTrackerClient
{
    private readonly HttpClient _httpClient;
    private readonly AviationstackOptions _options;
    private readonly ILogger<AviationstackClient> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public AviationstackClient(
        HttpClient httpClient,
        IOptions<AviationstackOptions> options,
        ILogger<AviationstackClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => (int)r.StatusCode >= 500)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (outcome, timespan, retryCount, _) =>
                {
                    _logger.LogWarning("Aviationstack API retry {RetryCount} after {Delay}s",
                        retryCount, timespan.TotalSeconds);
                });
    }

    // Normalisation is delegated to the shared FlightNumberHelper in the Application layer.

    public async Task<FlightStatusDto?> GetFlightStatusAsync(
        string flightIata,
        CancellationToken cancellationToken = default,
        string? apiKeyOverride = null)
    {
        try
        {
            var normalised = FlightNumberHelper.Normalise(flightIata);
            if (normalised != flightIata)
                _logger.LogDebug("Normalised flight number {Original} → {Normalised}", flightIata, normalised);

            // Use the override key (from DB) if provided; fall back to IOptions (appsettings/env)
            var effectiveKey = !string.IsNullOrWhiteSpace(apiKeyOverride) ? apiKeyOverride : _options.ApiKey;
            var url = $"{_options.BaseUrl}/flights?access_key={effectiveKey}&flight_iata={Uri.EscapeDataString(normalised)}&limit=1";
            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync(url, cancellationToken));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Aviationstack returned {StatusCode} for flight {FlightIata}", response.StatusCode, flightIata);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseFlightResponse(json, flightIata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching flight status for {FlightIata}", flightIata);
            return null;
        }
    }

    private static FlightStatusDto? ParseFlightResponse(string json, string flightIata)
    {
        try
        {
            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            var data = doc.GetProperty("data");
            if (data.GetArrayLength() == 0) return null;

            var flight = data[0];
            var dto = new FlightStatusDto
            {
                FlightNumber = flightIata,
                Status = flight.TryGetProperty("flight_status", out var status) ? status.GetString() : null,
            };

            if (flight.TryGetProperty("airline", out var airline))
                dto.Airline = airline.TryGetProperty("name", out var airlineName) ? airlineName.GetString() : null;

            if (flight.TryGetProperty("departure", out var dep))
            {
                dto.DepartureAirport = dep.TryGetProperty("iata", out var depIata) ? depIata.GetString() : null;
                if (dep.TryGetProperty("scheduled", out var schedDep) && DateTimeOffset.TryParse(schedDep.GetString(), out var sd))
                    dto.ScheduledDeparture = sd.UtcDateTime;
                if (dep.TryGetProperty("actual", out var actDep) && actDep.ValueKind != JsonValueKind.Null && DateTimeOffset.TryParse(actDep.GetString(), out var ad))
                    dto.ActualDeparture = ad.UtcDateTime;
            }

            if (flight.TryGetProperty("arrival", out var arr))
            {
                dto.ArrivalAirport = arr.TryGetProperty("iata", out var arrIata) ? arrIata.GetString() : null;
                if (arr.TryGetProperty("scheduled", out var schedArr) && DateTimeOffset.TryParse(schedArr.GetString(), out var sa))
                    dto.ScheduledArrival = sa.UtcDateTime;
                if (arr.TryGetProperty("actual", out var actArr) && actArr.ValueKind != JsonValueKind.Null && DateTimeOffset.TryParse(actArr.GetString(), out var aa))
                    dto.ActualArrival = aa.UtcDateTime;
                dto.Terminal = arr.TryGetProperty("terminal", out var term) ? term.GetString() : null;
                dto.Gate = arr.TryGetProperty("gate", out var gate) ? gate.GetString() : null;
                if (arr.TryGetProperty("delay", out var delay) && delay.ValueKind != JsonValueKind.Null)
                    dto.DelayMinutes = delay.GetInt32();
            }

            return dto;
        }
        catch
        {
            return null;
        }
    }
}
