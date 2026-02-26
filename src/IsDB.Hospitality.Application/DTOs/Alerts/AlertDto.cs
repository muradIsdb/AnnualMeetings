using IsDB.Hospitality.Domain.Enums;

namespace IsDB.Hospitality.Application.DTOs.Alerts;

public class AlertDto
{
    public Guid Id { get; set; }
    public Guid? GuestId { get; set; }
    public string? GuestName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public string SeverityLabel { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class CreateAlertDto
{
    public Guid? GuestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;
}

public class ResolveAlertDto
{
    public string? ResolutionNotes { get; set; }
}
