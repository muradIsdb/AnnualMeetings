using IsDB.Hospitality.Infrastructure.ExternalClients.FlightTracker;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IsDB.Hospitality.API.HealthChecks;

/// <summary>
/// Startup health check that validates the AviationStack API key is configured.
/// Logs a prominent warning if the key is missing or still the placeholder value.
/// </summary>
public class AviationstackHealthCheck : IHealthCheck
{
    private static readonly string[] PlaceholderValues =
    [
        "REPLACE_WITH_AVIATIONSTACK_API_KEY",
        "your_api_key",
        "YOUR_API_KEY",
        ""
    ];

    private readonly AviationstackOptions _options;
    private readonly ILogger<AviationstackHealthCheck> _logger;

    public AviationstackHealthCheck(
        IOptions<AviationstackOptions> options,
        ILogger<AviationstackHealthCheck> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var key = _options.ApiKey?.Trim() ?? string.Empty;

        if (PlaceholderValues.Contains(key))
        {
            _logger.LogWarning(
                "⚠️  AVIATIONSTACK API KEY IS NOT CONFIGURED. " +
                "Set the 'Aviationstack__ApiKey' environment variable in Railway. " +
                "Flight tracking will be disabled until this is resolved.");

            return Task.FromResult(HealthCheckResult.Degraded(
                "AviationStack API key is not configured. " +
                "Set Aviationstack__ApiKey in the Railway environment variables. " +
                "Flight status updates will not work until this is resolved."));
        }

        _logger.LogInformation("AviationStack API key is configured. Flight tracking is enabled.");
        return Task.FromResult(HealthCheckResult.Healthy("AviationStack API key is configured."));
    }
}
