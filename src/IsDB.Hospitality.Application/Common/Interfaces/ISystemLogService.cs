using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Application.Common.Interfaces;

public interface ISystemLogService
{
    /// <summary>
    /// Asynchronously writes a log entry to the SystemLogs table and Serilog.
    /// This method catches and swallows its own exceptions to prevent cascading failures.
    /// </summary>
    Task LogAsync(
        LogSeverity severity,
        string module,
        string title,
        string? detail = null,
        string? requestPath = null,
        Guid? staffUserId = null,
        string? staffName = null,
        string? correlationId = null);
}
