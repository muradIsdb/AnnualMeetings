namespace IsDB.Hospitality.Application.DTOs.EventsAir;

public class EventsAirEventDto
{
    public string UniqueCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}
