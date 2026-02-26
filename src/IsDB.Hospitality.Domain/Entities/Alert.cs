using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

public class Alert : BaseEntity
{
    public Guid? GuestId { get; set; }
    public Guest? Guest { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;
    public bool IsResolved { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByStaffId { get; set; }
    public StaffUser? ResolvedByStaff { get; set; }
    public string? ResolutionNotes { get; set; }
    public bool IsSystemGenerated { get; set; } = false;
}
