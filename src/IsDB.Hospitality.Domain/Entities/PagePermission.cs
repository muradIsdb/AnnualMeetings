using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// Stores which pages/modules a given role is permitted to access.
/// Admin role always has implicit access to all pages (enforced in code, not in this table).
/// </summary>
public class PagePermission : BaseEntity
{
    /// <summary>The role this permission applies to.</summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Dot-separated page identifier, e.g. "hotel.dashboard", "airport.dashboard".
    /// Must match the pageId constants used in the frontend.
    /// </summary>
    public string PageId { get; set; } = string.Empty;

    /// <summary>True = access granted; false = access denied.</summary>
    public bool IsGranted { get; set; } = true;
}
