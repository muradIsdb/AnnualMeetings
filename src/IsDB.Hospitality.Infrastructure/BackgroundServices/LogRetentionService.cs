using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IsDB.Hospitality.Infrastructure.BackgroundServices;

public class LogRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogRetentionService> _logger;

    public LogRetentionService(IServiceScopeFactory scopeFactory, ILogger<LogRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LogRetentionService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeOldLogsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during log retention purge.");
            }

            // Run once every 24 hours
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task PurgeOldLogsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await db.AppConfigs.FirstOrDefaultAsync(stoppingToken);
        int retentionDays = config?.LogRetentionDays ?? 90;

        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        int deletedCount = await db.SystemLogs
            .Where(l => l.OccurredAt < cutoffDate)
            .ExecuteDeleteAsync(stoppingToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Purged {DeletedCount} system logs older than {RetentionDays} days.", deletedCount, retentionDays);
        }
    }
}
