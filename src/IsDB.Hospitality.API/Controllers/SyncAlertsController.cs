using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

/// <summary>
/// Manages SyncAlerts — issues detected during EventsAir synchronisation
/// that require manual review by Admin or Transport staff.
/// </summary>
[ApiController]
[Route("api/transport-actions")]
[Authorize]
public class SyncAlertsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SyncAlertsController> _logger;

    public SyncAlertsController(AppDbContext db, ILogger<SyncAlertsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── GET /api/sync-alerts ─────────────────────────────────────────────────
    /// <summary>
    /// Returns a paginated list of sync alerts with optional filters.
    /// Accessible to Admin and Transport roles.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? alertType,
        [FromQuery] string? syncSource,
        [FromQuery] bool? isResolved,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var role = User.FindFirst("role")?.Value ?? "";
        if (role != "Admin" && role != "Transport")
            return Forbid();

        var q = _db.SyncAlerts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(alertType) && Enum.TryParse<SyncAlertType>(alertType, true, out var at))
            q = q.Where(a => a.AlertType == at);

        if (!string.IsNullOrWhiteSpace(syncSource) && Enum.TryParse<SyncAlertSource>(syncSource, true, out var ss))
            q = q.Where(a => a.SyncSource == ss);

        if (isResolved.HasValue)
            q = q.Where(a => a.IsResolved == isResolved.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(a =>
                a.GuestName.ToLower().Contains(s) ||
                (a.VehiclePlate != null && a.VehiclePlate.ToLower().Contains(s)) ||
                (a.CarClassName != null && a.CarClassName.ToLower().Contains(s)) ||
                (a.EventsAirContactId != null && a.EventsAirContactId.ToLower().Contains(s)));
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(a => a.DetectedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                alertType = a.AlertType.ToString(),
                a.GuestId,
                a.GuestName,
                a.EventsAirContactId,
                a.VehicleId,
                a.VehiclePlate,
                a.CarClassName,
                a.OldValue,
                a.NewValue,
                syncSource = a.SyncSource.ToString(),
                a.DetectedAt,
                a.IsResolved,
                a.ResolvedAt,
                a.ResolvedByUserName,
                a.Notes
            })
            .ToListAsync();

        return Ok(new
        {
            items,
            totalCount = total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
            page,
            pageSize
        });
    }

    // ─── GET /api/sync-alerts/summary ─────────────────────────────────────────
    /// <summary>Returns counts per alert type for the summary cards.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var role = User.FindFirst("role")?.Value ?? "";
        if (role != "Admin" && role != "Transport")
            return Forbid();

        var counts = await _db.SyncAlerts
            .GroupBy(a => new { a.AlertType, a.IsResolved })
            .Select(g => new { g.Key.AlertType, g.Key.IsResolved, Count = g.Count() })
            .ToListAsync();

        return Ok(new
        {
            guestRemoved     = counts.Where(c => c.AlertType == SyncAlertType.GuestRemoved && !c.IsResolved).Sum(c => c.Count),
            carClassMismatch = counts.Where(c => c.AlertType == SyncAlertType.CarClassMismatch && !c.IsResolved).Sum(c => c.Count),
            regTypeChanged   = counts.Where(c => c.AlertType == SyncAlertType.RegTypeChanged && !c.IsResolved).Sum(c => c.Count),
            resolved         = counts.Where(c => c.IsResolved).Sum(c => c.Count),
            totalOpen        = counts.Where(c => !c.IsResolved).Sum(c => c.Count)
        });
    }

    // ─── POST /api/sync-alerts/{id}/resolve ───────────────────────────────────
    /// <summary>Marks a single alert as resolved.</summary>
    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveRequest? req)
    {
        var role = User.FindFirst("role")?.Value ?? "";
        if (role != "Admin")
            return Forbid();

        var alert = await _db.SyncAlerts.FindAsync(id);
        if (alert == null) return NotFound();

        var userName = User.FindFirst("name")?.Value
                    ?? User.FindFirst("sub")?.Value
                    ?? "Unknown";

        alert.IsResolved = true;
        alert.ResolvedAt = DateTime.UtcNow;
        alert.ResolvedByUserName = userName;
        if (!string.IsNullOrWhiteSpace(req?.Notes))
            alert.Notes = req.Notes;

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ─── POST /api/sync-alerts/resolve-all ────────────────────────────────────
    /// <summary>Marks all open alerts as resolved (bulk action).</summary>
    [HttpPost("resolve-all")]
    public async Task<IActionResult> ResolveAll([FromQuery] string? alertType)
    {
        var role = User.FindFirst("role")?.Value ?? "";
        if (role != "Admin")
            return Forbid();

        var userName = User.FindFirst("name")?.Value
                    ?? User.FindFirst("sub")?.Value
                    ?? "Unknown";

        var q = _db.SyncAlerts.Where(a => !a.IsResolved);

        if (!string.IsNullOrWhiteSpace(alertType) && Enum.TryParse<SyncAlertType>(alertType, true, out var at))
            q = q.Where(a => a.AlertType == at);

        var alerts = await q.ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var a in alerts)
        {
            a.IsResolved = true;
            a.ResolvedAt = now;
            a.ResolvedByUserName = userName;
        }

        await _db.SaveChangesAsync();
        return Ok(new { resolved = alerts.Count });
    }

    // ─── POST /api/transport-actions/seed-test ────────────────────────────────
    /// <summary>Inserts one test SyncAlert of each type for UI verification. Admin only.</summary>
    [HttpPost("seed-test")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SeedTestAlerts()
    {
        // Remove any previously seeded test alerts to avoid duplicates
        var existing = await _db.SyncAlerts
            .Where(a => a.Notes == "[TEST]")
            .ToListAsync();
        _db.SyncAlerts.RemoveRange(existing);

        // Pick a real guest and vehicle from the DB for realistic data
        var guest = await _db.Guests.Where(g => g.DeservedCarClassId != null).FirstOrDefaultAsync()
                 ?? await _db.Guests.FirstOrDefaultAsync();
        var vehicle = await _db.Vehicles.Include(v => v.CarClass).FirstOrDefaultAsync();
        var now = DateTime.UtcNow;

        _db.SyncAlerts.AddRange(
            new SyncAlert
            {
                AlertType          = SyncAlertType.GuestRemoved,
                GuestId            = guest?.Id,
                GuestName          = guest != null ? $"{guest.FirstName} {guest.LastName}".Trim() : "Test Guest",
                EventsAirContactId = guest?.EventsAirContactId,
                OldValue           = "Active",
                NewValue           = "Removed from EventsAir",
                SyncSource         = SyncAlertSource.ManualSync,
                DetectedAt         = now,
                Notes              = "[TEST]"
            },
            new SyncAlert
            {
                AlertType          = SyncAlertType.CarClassMismatch,
                GuestId            = guest?.Id,
                GuestName          = guest != null ? $"{guest.FirstName} {guest.LastName}".Trim() : "Test Guest",
                EventsAirContactId = guest?.EventsAirContactId,
                VehicleId          = vehicle?.Id,
                VehiclePlate       = vehicle?.LicensePlate,
                CarClassName       = vehicle?.CarClass?.Name,
                OldValue           = "Hyundai Sonata",
                NewValue           = "Tayota Camry",
                SyncSource         = SyncAlertSource.ManualSync,
                DetectedAt         = now.AddSeconds(-5),
                Notes              = "[TEST]"
            },
            new SyncAlert
            {
                AlertType          = SyncAlertType.RegTypeChanged,
                GuestId            = guest?.Id,
                GuestName          = guest != null ? $"{guest.FirstName} {guest.LastName}".Trim() : "Test Guest",
                EventsAirContactId = guest?.EventsAirContactId,
                OldValue           = "Governor",
                NewValue           = "Observer",
                SyncSource         = SyncAlertSource.AutoSync,
                DetectedAt         = now.AddSeconds(-10),
                Notes              = "[TEST]"
            }
        );

        await _db.SaveChangesAsync();
        return Ok(new { seeded = 3, message = "3 test alerts inserted (GuestRemoved, CarClassMismatch, RegTypeChanged)." });
    }

    // ─── DELETE /api/sync-alerts/{id} ─────────────────────────────────────────
    /// <summary>Permanently deletes a resolved alert (Admin only).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var role = User.FindFirst("role")?.Value ?? "";
        if (role != "Admin") return Forbid();

        var alert = await _db.SyncAlerts.FindAsync(id);
        if (alert == null) return NotFound();

        _db.SyncAlerts.Remove(alert);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record ResolveRequest(string? Notes);


