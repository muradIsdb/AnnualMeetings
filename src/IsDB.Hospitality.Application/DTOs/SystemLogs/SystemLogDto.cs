using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Application.DTOs.SystemLogs;

public class SystemLogDto
{
    public Guid Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public LogSeverity Severity { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? RequestPath { get; set; }
    public string? StaffName { get; set; }
    public string? CorrelationId { get; set; }
}
