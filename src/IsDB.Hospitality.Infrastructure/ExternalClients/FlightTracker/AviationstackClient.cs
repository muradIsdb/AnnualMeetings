using System.Text.Json;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Normalises an IATA flight number for AviationStack queries.
    /// Strips leading zeros from the numeric suffix: "TK0334" → "TK334", "EK0583" → "EK583".
    /// Also removes internal spaces: "TK 334" → "TK334".
    /// </summary>
    private static string NormaliseFlightNumber(string flightIata)
    {
        // Remove all spaces first
        var s = flightIata.Replace(" ", "").Trim();
        // Strip leading zeros from the numeric part: letters followed by digits
        return Regex.Replace(s, @"^([A-Za-z]{1,3})0+(\d+.*)$", "$1$2");
    }

    public async Task<FlightStatusDto?> GetFlightStatusAsync(string flightIata, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalised = NormaliseFlightNumber(flightIata);
            if (normalised != flightIata)
                _logger.LogDebug("Normalised flight number {Original} → {Normalised}", flightIata, normalised);

            var url = $"{_options.BaseUrl}/flights?access_key={_options.ApiKey}&flight_iata={Uri.EscapeDataString(normalised)}&limit=1";
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
                if (dep.TryGetProperty("scheduled", out var schedDep) && DateTime.TryParse(schedDep.GetString(), out var sd))
                    dto.ScheduledDeparture = sd;
                if (dep.TryGetProperty("actual", out var actDep) && actDep.ValueKind != JsonValueKind.Null && DateTime.TryParse(actDep.GetString(), out var ad))
                    dto.ActualDeparture = ad;
            }

            if (flight.TryGetProperty("arrival", out var arr))
            {
                dto.ArrivalAirport = arr.TryGetProperty("iata", out var arrIata) ? arrIata.GetString() : null;
                if (arr.TryGetProperty("scheduled", out var schedArr) && DateTime.TryParse(schedArr.GetString(), out var sa))
                    dto.ScheduledArrival = sa;
                if (arr.TryGetProperty("actual", out var actArr) && actArr.ValueKind != JsonValueKind.Null && DateTime.TryParse(actArr.GetString(), out var aa))
                    dto.ActualArrival = aa;
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
