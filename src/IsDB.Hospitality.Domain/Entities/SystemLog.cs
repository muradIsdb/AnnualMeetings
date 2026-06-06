using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

public class SystemLog : BaseEntity
{
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public LogSeverity Severity { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? RequestPath { get; set; }
    public Guid? StaffUserId { get; set; }
    public StaffUser? StaffUser { get; set; }
    public string? StaffName { get; set; }
    public string? CorrelationId { get; set; }
}
