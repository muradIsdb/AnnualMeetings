using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

public class StaffUser : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    /// <summary>
    /// Legacy single-role field kept for backward compatibility.
    /// Use Roles navigation property for multi-role access checks.
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Airport;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }

    /// <summary>Many-to-many roles via join table.</summary>
    public ICollection<StaffUserRole> Roles { get; set; } = new List<StaffUserRole>();
}
