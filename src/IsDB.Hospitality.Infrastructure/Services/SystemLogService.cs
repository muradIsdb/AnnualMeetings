using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IsDB.Hospitality.Infrastructure.Services;

public class SystemLogService : ISystemLogService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SystemLogService> _logger;

    public SystemLogService(IServiceScopeFactory scopeFactory, ILogger<SystemLogService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task LogAsync(
        LogSeverity severity,
        string module,
        string title,
        string? detail = null,
        string? requestPath = null,
        Guid? staffUserId = null,
        string? staffName = null,
        string? correlationId = null)
    {
        // 1. Always log to Serilog first (so it appears in Railway console)
        switch (severity)
        {
            case LogSeverity.Information:
                _logger.LogInformation("[{Module}] {Title}", module, title);
                break;
            case LogSeverity.Warning:
                _logger.LogWarning("[{Module}] {Title} - {Detail}", module, title, detail);
                break;
            case LogSeverity.Error:
                _logger.LogError("[{Module}] {Title} - {Detail}", module, title, detail);
                break;
            case LogSeverity.Critical:
                _logger.LogCritical("[{Module}] {Title} - {Detail}", module, title, detail);
                break;
        }

        // 2. Write to database, swallowing exceptions
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var log = new SystemLog
            {
                Severity = severity,
                Module = module,
                Title = title,
                Detail = detail,
                RequestPath = requestPath,
                StaffUserId = staffUserId,
                StaffName = staffName,
                CorrelationId = correlationId,
                OccurredAt = DateTime.UtcNow
            };

            db.SystemLogs.Add(log);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Silently swallow DB logging errors so we don't crash the calling code
            _logger.LogError(ex, "Failed to write SystemLog to database.");
        }
    }
}
