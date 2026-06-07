using System.Text;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[Authorize]
public class NotificationsController : ApiControllerBase
{
    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/notifications
    // Returns the last 50 notifications visible to the current user's role,
    // with a per-notification isRead flag for this user.
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> GetNotifications(
        [FromServices] AppDbContext db,
        CancellationToken ct = default)
    {
        var (callerRole, callerId) = GetCallerInfo();
        var activeEventCode = (await db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;

        var notifications = await db.Notifications
            .Include(n => n.CreatedByStaff)
            .Include(n => n.Reads)
            .Where(n => activeEventCode == null || n.EventCode == null || n.EventCode == activeEventCode)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var visible = notifications
            .Where(n => IsVisibleTo(n.TargetRoles, callerRole))
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Message = n.Message,
                TargetRoles = n.TargetRoles,
                Priority = n.Priority,
                PriorityLabel = n.Priority.ToString(),
                CreatedAt = DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc),
                SenderName = n.CreatedByStaff?.FullName ?? "System",
                SenderRole = n.CreatedByStaff?.Role.ToString() ?? "",
                IsRead = n.Reads.Any(r => r.StaffUserId == callerId)
            })
            .ToList();

        return Ok(visible);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/notifications/unread-count
    // Lightweight endpoint polled every 30 s for the bell badge.
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(
        [FromServices] AppDbContext db,
        CancellationToken ct = default)
    {
        var (callerRole, callerId) = GetCallerInfo();
        var activeEventCode2 = (await db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;

        var notifications = await db.Notifications
            .Include(n => n.Reads)
            .Where(n => activeEventCode2 == null || n.EventCode == null || n.EventCode == activeEventCode2)
            .ToListAsync(ct);

        var count = notifications
            .Where(n => IsVisibleTo(n.TargetRoles, callerRole))
            .Count(n => !n.Reads.Any(r => r.StaffUserId == callerId));

        return Ok(count);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/notifications/history
    // Admin-only: paginated, filtered, searchable full notification history.
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("history")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<NotificationHistoryResponse>> GetHistory(
        [FromServices] AppDbContext db,
        [FromQuery] string? search = null,
        [FromQuery] int? priority = null,
        [FromQuery] string? targetRole = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var activeEventCodeH = (await db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;

        var query = db.Notifications
            .Include(n => n.CreatedByStaff)
            .Include(n => n.Reads)
            .Where(n => activeEventCodeH == null || n.EventCode == null || n.EventCode == activeEventCodeH)
            .AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(n => n.Message.ToLower().Contains(s));
        }

        // Priority filter
        if (priority.HasValue && Enum.IsDefined(typeof(AlertSeverity), priority.Value))
        {
            var sev = (AlertSeverity)priority.Value;
            query = query.Where(n => n.Priority == sev);
        }

        // Target role filter (use ToLower() for EF Core / SQLite compatibility)
        if (!string.IsNullOrWhiteSpace(targetRole) && !targetRole.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var roleLower = targetRole.Trim().ToLower();
            query = query.Where(n =>
                n.TargetRoles.ToLower() == "all" ||
                n.TargetRoles.ToLower().Contains(roleLower));
        }

        // Date range
        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
            query = query.Where(n => n.CreatedAt >= fromUtc);
        }
        if (to.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(to.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(n => n.CreatedAt <= toUtc);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(n => new NotificationHistoryDto
        {
            Id = n.Id,
            Message = n.Message,
            TargetRoles = n.TargetRoles,
            Priority = n.Priority,
            PriorityLabel = n.Priority.ToString(),
            CreatedAt = DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc),
            SenderName = n.CreatedByStaff?.FullName ?? "System",
            SenderRole = n.CreatedByStaff?.Role.ToString() ?? "",
            ReadCount = n.Reads.Count
        }).ToList();

        return Ok(new NotificationHistoryResponse
        {
            Items = dtos,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/notifications/export
    // Admin-only: export filtered notifications as CSV.
    // Accepts same query params as /history (except page/pageSize).
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("export")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportCsv(
        [FromServices] AppDbContext db,
        [FromQuery] string? search = null,
        [FromQuery] int? priority = null,
        [FromQuery] string? targetRole = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var activeEventCodeE = (await db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;

        var query = db.Notifications
            .Include(n => n.CreatedByStaff)
            .Include(n => n.Reads)
            .Where(n => activeEventCodeE == null || n.EventCode == null || n.EventCode == activeEventCodeE)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(n => n.Message.ToLower().Contains(s));
        }
        if (priority.HasValue && Enum.IsDefined(typeof(AlertSeverity), priority.Value))
        {
            var sev = (AlertSeverity)priority.Value;
            query = query.Where(n => n.Priority == sev);
        }
        if (!string.IsNullOrWhiteSpace(targetRole) && !targetRole.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var roleLower = targetRole.Trim().ToLower();
            query = query.Where(n =>
                n.TargetRoles.ToLower() == "all" ||
                n.TargetRoles.ToLower().Contains(roleLower));
        }
        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
            query = query.Where(n => n.CreatedAt >= fromUtc);
        }
        if (to.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(to.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(n => n.CreatedAt <= toUtc);
        }

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Date,Message,Priority,TargetRoles,SentBy,ReadCount");
        foreach (var n in items)
        {
            var date = DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss");
            var msg = EscapeCsv(n.Message);
            var prio = n.Priority.ToString();
            var roles = EscapeCsv(n.TargetRoles);
            var sender = EscapeCsv(n.CreatedByStaff?.FullName ?? "System");
            var reads = n.Reads.Count.ToString();
            sb.AppendLine($"{date},{msg},{prio},{roles},{sender},{reads}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"notifications_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DELETE /api/notifications/bulk-delete
    // Admin-only: delete notifications by ID list OR by age (olderThanDays).
    // ──────────────────────────────────────────────────────────────────────────
    [HttpDelete("bulk-delete")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BulkDelete(
        [FromBody] BulkDeleteDto request,
        [FromServices] AppDbContext db,
        CancellationToken ct = default)
    {
        if ((request.Ids == null || request.Ids.Count == 0) && !request.OlderThanDays.HasValue)
            return BadRequest("Provide either 'ids' or 'olderThanDays'.");

        IQueryable<Notification> query = db.Notifications;

        if (request.Ids != null && request.Ids.Count > 0)
        {
            query = query.Where(n => request.Ids.Contains(n.Id));
        }
        else if (request.OlderThanDays.HasValue)
        {
            var cutoff = DateTime.UtcNow.AddDays(-request.OlderThanDays.Value);
            query = query.Where(n => n.CreatedAt < cutoff);
        }

        var toDelete = await query.ToListAsync(ct);
        if (toDelete.Count == 0)
            return Ok(new { deleted = 0 });

        db.Notifications.RemoveRange(toDelete);
        await db.SaveChangesAsync(ct);

        return Ok(new { deleted = toDelete.Count });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/notifications
    // Admin and ControlRoom can send any priority; other roles cannot send Critical.
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<Guid>> SendNotification(
        [FromBody] SendNotificationDto request,
        [FromServices] AppDbContext db,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message is required.");

        // Only Admin and ControlRoom may send Critical priority notifications
        var (callerRoleForSend, _) = GetCallerInfo();
        if (request.Priority == AlertSeverity.Critical
            && callerRoleForSend != UserRole.Admin
            && callerRoleForSend != UserRole.ControlRoom)
        {
            return BadRequest(new { message = "Only Admin and Control Room roles can send Critical priority notifications." });
        }

        var activeEventCodeN = (await db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;

        var notification = new Notification
        {
            Message = request.Message.Trim(),
            TargetRoles = string.IsNullOrWhiteSpace(request.TargetRoles) ? "All" : request.TargetRoles.Trim(),
            Priority = request.Priority,
            CreatedByStaffId = CurrentUserId,
            EventCode = activeEventCodeN
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetNotifications), new { id = notification.Id }, new { id = notification.Id });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT /api/notifications/{id}/read
    // Mark a single notification as read for the current user.
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid id,
        [FromServices] AppDbContext db,
        CancellationToken ct = default)
    {
        var (_, callerId) = GetCallerInfo();

        var exists = await db.Notifications.AnyAsync(n => n.Id == id, ct);
        if (!exists) return NotFound();

        var alreadyRead = await db.NotificationReads
            .AnyAsync(r => r.NotificationId == id && r.StaffUserId == callerId, ct);

        if (!alreadyRead)
        {
            db.NotificationReads.Add(new NotificationRead
            {
                NotificationId = id,
                StaffUserId = callerId,
                ReadAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT /api/notifications/read-all
    // Mark all visible notifications as read for the current user.
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead(
        [FromServices] AppDbContext db,
        CancellationToken ct = default)
    {
        var (callerRole, callerId) = GetCallerInfo();

        var notifications = await db.Notifications
            .Include(n => n.Reads)
            .ToListAsync(ct);

        var unread = notifications
            .Where(n => IsVisibleTo(n.TargetRoles, callerRole))
            .Where(n => !n.Reads.Any(r => r.StaffUserId == callerId))
            .ToList();

        foreach (var n in unread)
        {
            db.NotificationReads.Add(new NotificationRead
            {
                NotificationId = n.Id,
                StaffUserId = callerId,
                ReadAt = DateTime.UtcNow
            });
        }

        if (unread.Count > 0)
            await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────
    private (UserRole? role, Guid id) GetCallerInfo()
    {
        var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                     ?? User.FindFirst("role")?.Value;
        UserRole? role = Enum.TryParse<UserRole>(roleClaim, out var r) ? r : null;
        return (role, CurrentUserId);
    }

    private static bool IsVisibleTo(string targetRoles, UserRole? callerRole)
    {
        if (string.IsNullOrWhiteSpace(targetRoles) || targetRoles.Equals("All", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!callerRole.HasValue) return false;

        var targets = targetRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return targets.Any(t => t.Equals(callerRole.Value.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// DTOs
// ──────────────────────────────────────────────────────────────────────────────
public record NotificationDto
{
    public Guid Id { get; init; }
    public string Message { get; init; } = string.Empty;
    public string TargetRoles { get; init; } = "All";
    public AlertSeverity Priority { get; init; }
    public string PriorityLabel { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string SenderRole { get; init; } = string.Empty;
    public bool IsRead { get; init; }
}

public record NotificationHistoryDto
{
    public Guid Id { get; init; }
    public string Message { get; init; } = string.Empty;
    public string TargetRoles { get; init; } = "All";
    public AlertSeverity Priority { get; init; }
    public string PriorityLabel { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string SenderRole { get; init; } = string.Empty;
    public int ReadCount { get; init; }
}

public record NotificationHistoryResponse
{
    public List<NotificationHistoryDto> Items { get; init; } = new();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public record SendNotificationDto
{
    public string Message { get; init; } = string.Empty;
    /// <summary>Comma-separated role names or "All".</summary>
    public string TargetRoles { get; init; } = "All";
    public AlertSeverity Priority { get; init; } = AlertSeverity.Medium;
}

public record BulkDeleteDto
{
    public List<Guid>? Ids { get; init; }
    public int? OlderThanDays { get; init; }
}
