namespace IsDB.Hospitality.Domain.Entities;

public class HotelOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool ShowInDepartureForm { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Room allocation fields — editable by Hotel role
    public int ContractedRoomsIsDB { get; set; } = 0;
    public int ContractedRoomsGuest { get; set; } = 0;
    public int ActualOccupiedIsDB { get; set; } = 0;
    public int ActualOccupiedGuest { get; set; } = 0;
}
