using IsDB.Hospitality.Infrastructure.ExternalClients.FlightTracker;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IsDB.Hospitality.API.HealthChecks;

/// <summary>
/// Health check that validates the AviationStack API key is configured.
/// Checks DB first (admin-configured), then falls back to appsettings/env vars.
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
    private readonly IServiceProvider _serviceProvider;

    public AviationstackHealthCheck(
        IOptions<AviationstackOptions> options,
        ILogger<AviationstackHealthCheck> logger,
        IServiceProvider serviceProvider)
    {
        _options = options.Value;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // DB key takes precedence over appsettings/env
        string? key = null;
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = await db.AppConfigs.FindAsync(new object[] { 1 }, cancellationToken);
            if (!string.IsNullOrWhiteSpace(config?.AviationstackApiKey))
                key = config.AviationstackApiKey.Trim();
        }
        catch
        {
            // DB not yet available — fall through to appsettings
        }

        if (string.IsNullOrWhiteSpace(key))
            key = _options.ApiKey?.Trim() ?? string.Empty;

        if (PlaceholderValues.Contains(key))
        {
            _logger.LogWarning(
                "⚠️  AVIATIONSTACK API KEY IS NOT CONFIGURED. " +
                "Set it in Platform Settings → Flight Tracking, or via the " +
                "'Aviationstack__ApiKey' environment variable in Railway. " +
                "Flight tracking will be disabled until this is resolved.");

            return HealthCheckResult.Degraded(
                "AviationStack API key is not configured. " +
                "Set it in Platform Settings → Flight Tracking or via Railway env vars. " +
                "Flight status updates will not work until this is resolved.");
        }

        _logger.LogInformation("AviationStack API key is configured. Flight tracking is enabled.");
        return HealthCheckResult.Healthy("AviationStack API key is configured.");
    }
}
