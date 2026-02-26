using IsDB.Hospitality.Domain.Common;
using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Domain.Entities;

public class ChecklistItem : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }
    public ChecklistItemType Type { get; set; }
    public bool IsRequired { get; set; } = true;

    public ICollection<ChecklistCompletion> Completions { get; set; } = new List<ChecklistCompletion>();
}

public class ChecklistCompletion : BaseEntity
{
    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!;

    public Guid ChecklistItemId { get; set; }
    public ChecklistItem ChecklistItem { get; set; } = null!;

    public Guid CompletedByStaffId { get; set; }
    public StaffUser CompletedByStaff { get; set; } = null!;

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
