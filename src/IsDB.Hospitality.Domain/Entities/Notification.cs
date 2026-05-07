using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// A staff-to-staff broadcast notification. Any authenticated user can send;
/// recipients are filtered by TargetRoles. Per-user read state is tracked in NotificationRead.
/// </summary>
public class Notification : BaseEntity
{
    public string Message { get; set; } = string.Empty;

    /// <summary>Comma-separated role names, e.g. "Airport,Transport" or "All".</summary>
    public string TargetRoles { get; set; } = "All";

    public AlertSeverity Priority { get; set; } = AlertSeverity.Medium;

    public Guid CreatedByStaffId { get; set; }
    public StaffUser? CreatedByStaff { get; set; }

    public ICollection<NotificationRead> Reads { get; set; } = new List<NotificationRead>();
}
