using IsDB.Hospitality.Application.DTOs.Alerts;
using IsDB.Hospitality.Application.Features.Alerts.Commands;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[Authorize]
public class AlertsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AlertDto>>> GetAlerts(
        [FromServices] AppDbContext db,
        [FromQuery] bool includeResolved = false,
        CancellationToken ct = default)
    {
        var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                     ?? User.FindFirst("role")?.Value;
        UserRole? callerRole = Enum.TryParse<UserRole>(roleClaim, out var r) ? r : null;

        var query = db.Alerts
            .Include(a => a.Guest)
            .AsQueryable();

        if (!includeResolved)
            query = query.Where(a => !a.IsResolved);

        // Filter by role: show alerts targeted at this role OR untagged (null)
        if (callerRole.HasValue && callerRole.Value != UserRole.Admin)
            query = query.Where(a => a.TargetRole == null || a.TargetRole == callerRole.Value);

        var alerts = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .Select(a => new AlertDto
            {
                Id = a.Id,
                GuestId = a.GuestId,
                GuestName = a.Guest != null ? $"{a.Guest.FirstName} {a.Guest.LastName}" : null,
                Title = a.Title,
                Message = a.Message,
                Severity = a.Severity,
                SeverityLabel = a.Severity.ToString(),
                IsResolved = a.IsResolved,
                CreatedAt = a.CreatedAt,
                ResolvedAt = a.ResolvedAt,
                TargetRole = a.TargetRole,
                IsRead = a.IsRead
            })
            .ToListAsync(ct);

        return Ok(alerts);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(
        [FromServices] AppDbContext db,
        CancellationToken ct = default)
    {
        var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                     ?? User.FindFirst("role")?.Value;
        UserRole? callerRole = Enum.TryParse<UserRole>(roleClaim, out var r) ? r : null;

        var query = db.Alerts
            .Where(a => !a.IsResolved && !a.IsRead);

        if (callerRole.HasValue && callerRole.Value != UserRole.Admin)
            query = query.Where(a => a.TargetRole == null || a.TargetRole == callerRole.Value);

        return Ok(await query.CountAsync(ct));
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateAlert([FromBody] CreateAlertDto request)
    {
        var id = await Mediator.Send(new CreateAlertCommand(
            request.GuestId, request.Title, request.Message, request.Severity));
        return CreatedAtAction(nameof(CreateAlert), new { id }, new { id });
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> ResolveAlert(Guid id, [FromBody] ResolveAlertDto? request = null)
    {
        var success = await Mediator.Send(new ResolveAlertCommand(id, CurrentUserId, request?.ResolutionNotes));
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:guid}/mark-read")]
    public async Task<IActionResult> MarkRead(
        Guid id,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var alert = await db.Alerts.FindAsync(new object[] { id }, ct);
        if (alert == null) return NotFound();
        alert.IsRead = true;
        alert.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead(
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                     ?? User.FindFirst("role")?.Value;
        UserRole? callerRole = Enum.TryParse<UserRole>(roleClaim, out var r) ? r : null;

        var query = db.Alerts.Where(a => !a.IsRead && !a.IsResolved);
        if (callerRole.HasValue && callerRole.Value != UserRole.Admin)
            query = query.Where(a => a.TargetRole == null || a.TargetRole == callerRole.Value);

        await query.ExecuteUpdateAsync(s => s
            .SetProperty(a => a.IsRead, true)
            .SetProperty(a => a.UpdatedAt, DateTime.UtcNow), ct);

        return NoContent();
    }
}
