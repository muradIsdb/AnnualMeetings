using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Join table for many-to-many relationship between StaffUser and UserRole.
/// A staff member can hold multiple roles simultaneously.
/// </summary>
public class StaffUserRole
{
    public Guid StaffUserId { get; set; }
    public StaffUser StaffUser { get; set; } = null!;

    public UserRole Role { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
