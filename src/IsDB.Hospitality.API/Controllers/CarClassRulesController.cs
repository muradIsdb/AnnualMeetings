using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[ApiController]
[Route("api/car-class-rules")]
[Authorize]
public class CarClassRulesController : ControllerBase
{
    private readonly IAppDbContext _db;

    public CarClassRulesController(IAppDbContext db) => _db = db;

    // ─── DTOs ─────────────────────────────────────────────────────────────────

    public record CarClassRuleDto(
        Guid Id,
        string RegistrationTypeName,
        Guid CarClassId,
        string CarClassName,
        string? CarClassColor,
        int Priority,
        string? Notes
    );

    public record UpsertCarClassRuleRequest(
        string RegistrationTypeName,
        Guid CarClassId,
        int Priority,
        string? Notes
    );

    public record AutoAssignRequest(
        bool DryRun = false,
        bool OverwriteExisting = false
    );

    public record AutoAssignPreviewItem(
        Guid GuestId,
        string GuestName,
        string? RegistrationTypeName,
        string CarClassName,
        string? CarClassColor,
        bool WouldOverwrite
    );

    public record AutoAssignResult(
        bool DryRun,
        int Matched,
        int Skipped,
        int Overwritten,
        List<AutoAssignPreviewItem> Preview
    );

    // ─── GET all rules ────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var rules = await _db.CarClassRules
            .Include(r => r.CarClass)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.RegistrationTypeName)
            .Select(r => new CarClassRuleDto(
                r.Id,
                r.RegistrationTypeName,
                r.CarClassId,
                r.CarClass.Name,
                r.CarClass.Color,
                r.Priority,
                r.Notes))
            .ToListAsync(ct);

        return Ok(rules);
    }

    // ─── POST create rule ─────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertCarClassRuleRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RegistrationTypeName))
            return BadRequest("RegistrationTypeName is required.");

        var exists = await _db.CarClassRules
            .AnyAsync(r => r.RegistrationTypeName.ToLower() == req.RegistrationTypeName.ToLower(), ct);
        if (exists)
            return Conflict($"A rule for '{req.RegistrationTypeName}' already exists.");

        var carClass = await _db.CarClasses.FindAsync(new object[] { req.CarClassId }, ct);
        if (carClass == null) return NotFound("Car class not found.");

        var rule = new CarClassRule
        {
            Id = Guid.NewGuid(),
            RegistrationTypeName = req.RegistrationTypeName.Trim(),
            CarClassId = req.CarClassId,
            Priority = req.Priority,
            Notes = req.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.CarClassRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        var dto = new CarClassRuleDto(rule.Id, rule.RegistrationTypeName, rule.CarClassId,
            carClass.Name, carClass.Color, rule.Priority, rule.Notes);
        return CreatedAtAction(nameof(GetAll), dto);
    }

    // ─── PUT update rule ──────────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertCarClassRuleRequest req, CancellationToken ct)
    {
        var rule = await _db.CarClassRules.FindAsync(new object[] { id }, ct);
        if (rule == null) return NotFound();

        var carClass = await _db.CarClasses.FindAsync(new object[] { req.CarClassId }, ct);
        if (carClass == null) return NotFound("Car class not found.");

        // Check uniqueness only if name changed
        if (!string.Equals(rule.RegistrationTypeName, req.RegistrationTypeName, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _db.CarClassRules
                .AnyAsync(r => r.Id != id && r.RegistrationTypeName.ToLower() == req.RegistrationTypeName.ToLower(), ct);
            if (exists)
                return Conflict($"A rule for '{req.RegistrationTypeName}' already exists.");
        }

        rule.RegistrationTypeName = req.RegistrationTypeName.Trim();
        rule.CarClassId = req.CarClassId;
        rule.Priority = req.Priority;
        rule.Notes = req.Notes;
        rule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(new CarClassRuleDto(rule.Id, rule.RegistrationTypeName, rule.CarClassId,
            carClass.Name, carClass.Color, rule.Priority, rule.Notes));
    }

    // ─── DELETE rule ──────────────────────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var rule = await _db.CarClassRules.FindAsync(new object[] { id }, ct);
        if (rule == null) return NotFound();

        _db.CarClassRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ─── GET distinct registration types from guests ──────────────────────────

    [HttpGet("registration-types")]
    public async Task<IActionResult> GetRegistrationTypes(CancellationToken ct)
    {
        var types = await _db.Guests
            .Where(g => g.IsActive && g.RegistrationTypeName != null)
            .Select(g => g.RegistrationTypeName!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(ct);

        return Ok(types);
    }

    // ─── POST auto-assign ─────────────────────────────────────────────────────

    [HttpPost("auto-assign")]
    public async Task<IActionResult> AutoAssign([FromBody] AutoAssignRequest req, CancellationToken ct)
    {
        // Load all rules ordered by priority
        var rules = await _db.CarClassRules
            .Include(r => r.CarClass)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

        if (!rules.Any())
            return BadRequest("No mapping rules configured. Add rules first.");

        // Build a lookup: registration type name (lower) → rule
        var ruleLookup = rules
            .GroupBy(r => r.RegistrationTypeName.ToLower())
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Priority).First());

        // Load active guests
        var guests = await _db.Guests
            .Where(g => g.IsActive)
            .Include(g => g.DeservedCarClass)
            .ToListAsync(ct);

        var preview = new List<AutoAssignPreviewItem>();
        int matched = 0, skipped = 0, overwritten = 0;

        foreach (var guest in guests)
        {
            var regType = guest.RegistrationTypeName?.ToLower() ?? "";
            if (!ruleLookup.TryGetValue(regType, out var rule))
            {
                skipped++;
                continue;
            }

            bool hasExisting = guest.DeservedCarClassId.HasValue;

            if (hasExisting && !req.OverwriteExisting)
            {
                skipped++;
                continue;
            }

            preview.Add(new AutoAssignPreviewItem(
                guest.Id,
                guest.FullName,
                guest.RegistrationTypeName,
                rule.CarClass.Name,
                rule.CarClass.Color,
                hasExisting
            ));

            if (hasExisting) overwritten++;
            else matched++;

            if (!req.DryRun)
            {
                guest.DeservedCarClassId = rule.CarClassId;
            }
        }

        if (!req.DryRun && preview.Any())
        {
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new AutoAssignResult(
            req.DryRun,
            matched,
            skipped,
            overwritten,
            preview
        ));
    }
}
