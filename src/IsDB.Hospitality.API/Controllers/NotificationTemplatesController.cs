using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[Authorize]
[Route("api/notification-templates")]
public class NotificationTemplatesController : ApiControllerBase
{
    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/notification-templates
    // Returns all notification templates. Visible to all authenticated users.
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromServices] AppDbContext db,
        CancellationToken ct = default)
    {
        var templates = await db.NotificationTemplates
            .OrderBy(t => t.EventKey)
            .Select(t => new NotificationTemplateDto
            {
                Id              = t.Id,
                EventKey        = t.EventKey,
                EventLabel      = t.EventLabel,
                MessageTemplate = t.MessageTemplate,
                TargetRoles     = t.TargetRoles,
                Priority        = t.Priority,
                PriorityLabel   = t.Priority.ToString(),
                Description     = t.Description,
                UpdatedAt       = DateTime.SpecifyKind(t.UpdatedAt, DateTimeKind.Utc)
            })
            .ToListAsync(ct);

        return Ok(templates);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT /api/notification-templates/{id}
    // Admin only — update message template and/or priority.
    // TargetRoles is NOT editable (system-defined).
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateNotificationTemplateDto request,
        [FromServices] AppDbContext db,
        CancellationToken ct = default)
    {
        var template = await db.NotificationTemplates.FindAsync(new object[] { id }, ct);
        if (template == null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.MessageTemplate))
            return BadRequest("MessageTemplate cannot be empty.");

        template.MessageTemplate = request.MessageTemplate.Trim();
        template.Priority        = request.Priority;

        await db.SaveChangesAsync(ct);

        return Ok(new NotificationTemplateDto
        {
            Id              = template.Id,
            EventKey        = template.EventKey,
            EventLabel      = template.EventLabel,
            MessageTemplate = template.MessageTemplate,
            TargetRoles     = template.TargetRoles,
            Priority        = template.Priority,
            PriorityLabel   = template.Priority.ToString(),
            Description     = template.Description,
            UpdatedAt       = DateTime.SpecifyKind(template.UpdatedAt, DateTimeKind.Utc)
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/notification-templates/reset/{eventKey}
    // Admin only — reset a single template back to its default values.
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("reset/{eventKey}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetToDefault(
        string eventKey,
        [FromServices] AppDbContext db,
        CancellationToken ct = default)
    {
        var template = await db.NotificationTemplates
            .FirstOrDefaultAsync(t => t.EventKey == eventKey, ct);
        if (template == null) return NotFound();

        var defaultTemplate = Services.NotificationTemplateService.DefaultTemplates
            .FirstOrDefault(t => t.EventKey == eventKey);
        if (defaultTemplate == null) return NotFound("No default found for this event key.");

        template.MessageTemplate = defaultTemplate.MessageTemplate;
        template.Priority        = defaultTemplate.Priority;

        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Reset to default.", eventKey });
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// DTOs
// ──────────────────────────────────────────────────────────────────────────────
public record NotificationTemplateDto
{
    public Guid Id { get; init; }
    public string EventKey { get; init; } = string.Empty;
    public string EventLabel { get; init; } = string.Empty;
    public string MessageTemplate { get; init; } = string.Empty;
    public string TargetRoles { get; init; } = string.Empty;
    public AlertSeverity Priority { get; init; }
    public string PriorityLabel { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
}

public record UpdateNotificationTemplateDto
{
    public string MessageTemplate { get; init; } = string.Empty;
    public AlertSeverity Priority { get; init; }
}
