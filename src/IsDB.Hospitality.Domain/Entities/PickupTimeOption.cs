namespace IsDB.Hospitality.Domain.Entities;

public class PickupDayOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = string.Empty;   // e.g. "Monday, 25 Feb"
    public string Value { get; set; } = string.Empty;   // e.g. "2026-02-25"
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PickupHourOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = string.Empty;   // e.g. "09:00 AM"
    public string Value { get; set; } = string.Empty;   // e.g. "09:00"
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
