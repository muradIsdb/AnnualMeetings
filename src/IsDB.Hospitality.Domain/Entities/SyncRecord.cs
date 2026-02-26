using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

public class SyncRecord : BaseEntity
{
    public string ServiceName { get; set; } = string.Empty;
    public SyncStatus Status { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsUpdated { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Details { get; set; }
}
