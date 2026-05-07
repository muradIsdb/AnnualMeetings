namespace IsDB.Hospitality.Domain.Entities;

/// <summary>Tracks which staff users have read a given notification.</summary>
public class NotificationRead
{
    public Guid NotificationId { get; set; }
    public Notification? Notification { get; set; }

    public Guid StaffUserId { get; set; }
    public StaffUser? StaffUser { get; set; }

    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}
