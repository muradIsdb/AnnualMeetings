using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

/// <summary>
/// Manages configurable EventsAir custom field sync filters (e.g. "Rank").
/// Each SyncFieldMapping represents one custom contact field in EventsAir,
/// identified by its GUID, and holds a set of manually-defined values to filter on during sync.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("api/sync-field-mappings")]
public class SyncFieldMappingsController : ApiControllerBase
{
    private readonly IAppDbContext _context;

    public SyncFieldMappingsController(IAppDbContext context)
    {
        _context = context;
    }

    // ── Field Mapping CRUD ────────────────────────────────────────────────────

    /// <summary>Get all field mappings with their values</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var mappings = await _context.SyncFieldMappings
            .Include(m => m.SelectedValues)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.DisplayName)
            .Select(m => new
            {
                m.Id,
                m.DisplayName,
                m.EventsAirFieldGuid,
                m.Description,
                m.SortOrder,
                m.CreatedAt,
                m.UpdatedAt,
                SelectedValues = m.SelectedValues
                    .OrderBy(v => v.Value)
                    .Select(v => new
                    {
                        v.Id,
                        v.Value,
                        v.IsSelectedForSync,
                        v.CreatedAt,
                        v.UpdatedAt
                    })
            })
            .ToListAsync();

        return Ok(mappings);
    }

    /// <summary>Create a new field mapping</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSyncFieldMappingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest(new { message = "DisplayName is required." });

        if (string.IsNullOrWhiteSpace(request.EventsAirFieldGuid))
            return BadRequest(new { message = "EventsAirFieldGuid is required." });

        var exists = await _context.SyncFieldMappings
            .AnyAsync(m => m.EventsAirFieldGuid == request.EventsAirFieldGuid.Trim());
        if (exists)
            return Conflict(new { message = $"A field mapping for GUID '{request.EventsAirFieldGuid}' already exists." });

        var mapping = new SyncFieldMapping
        {
            Id = Guid.NewGuid(),
            DisplayName = request.DisplayName.Trim(),
            EventsAirFieldGuid = request.EventsAirFieldGuid.Trim().ToLower(),
            Description = request.Description?.Trim(),
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.SyncFieldMappings.Add(mapping);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = mapping.Id }, mapping);
    }

    /// <summary>Update a field mapping's display name or description</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSyncFieldMappingRequest request)
    {
        var mapping = await _context.SyncFieldMappings.FindAsync(id);
        if (mapping == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            mapping.DisplayName = request.DisplayName.Trim();
        if (request.Description != null)
            mapping.Description = request.Description.Trim();
        if (!string.IsNullOrWhiteSpace(request.EventsAirFieldGuid))
            mapping.EventsAirFieldGuid = request.EventsAirFieldGuid.Trim().ToLower();
        if (request.SortOrder.HasValue)
            mapping.SortOrder = request.SortOrder.Value;
        mapping.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(mapping);
    }

    /// <summary>Delete a field mapping and all its values</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var mapping = await _context.SyncFieldMappings
            .Include(m => m.SelectedValues)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (mapping == null) return NotFound();

        _context.SyncFieldValues.RemoveRange(mapping.SelectedValues);
        _context.SyncFieldMappings.Remove(mapping);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── Manual Value Management ───────────────────────────────────────────────

    /// <summary>
    /// Manually add a known value to a field mapping (e.g. "VVIP", "VIP", "Official").
    /// </summary>
    [HttpPost("{id}/values")]
    public async Task<IActionResult> AddValue(Guid id, [FromBody] AddSyncFieldValueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Value))
            return BadRequest(new { message = "Value is required." });

        var mapping = await _context.SyncFieldMappings
            .Include(m => m.SelectedValues)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (mapping == null) return NotFound();

        var trimmed = request.Value.Trim();

        // Prevent duplicates (case-insensitive)
        if (mapping.SelectedValues.Any(v => string.Equals(v.Value, trimmed, StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { message = $"Value '{trimmed}' already exists for this field mapping." });

        var value = new SyncFieldValue
        {
            Id = Guid.NewGuid(),
            SyncFieldMappingId = mapping.Id,
            Value = trimmed,
            IsSelectedForSync = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.SyncFieldValues.Add(value);
        await _context.SaveChangesAsync();

        return Ok(new { value.Id, value.Value, value.IsSelectedForSync, value.CreatedAt });
    }

    /// <summary>
    /// Delete a specific value from a field mapping.
    /// </summary>
    [HttpDelete("{id}/values/{valueId}")]
    public async Task<IActionResult> DeleteValue(Guid id, Guid valueId)
    {
        var value = await _context.SyncFieldValues
            .FirstOrDefaultAsync(v => v.Id == valueId && v.SyncFieldMappingId == id);
        if (value == null) return NotFound();

        _context.SyncFieldValues.Remove(value);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── Value Selection ───────────────────────────────────────────────────────

    /// <summary>
    /// Bulk update which values are selected for sync for a given field mapping.
    /// Pass the array of SyncFieldValue IDs that should be selected.
    /// </summary>
    [HttpPost("{id}/value-selection")]
    public async Task<IActionResult> UpdateValueSelection(Guid id, [FromBody] UpdateValueSelectionRequest request)
    {
        var mapping = await _context.SyncFieldMappings
            .Include(m => m.SelectedValues)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (mapping == null) return NotFound();

        foreach (var val in mapping.SelectedValues)
        {
            val.IsSelectedForSync = request.SelectedValueIds.Contains(val.Id);
            val.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var selectedCount = mapping.SelectedValues.Count(v => v.IsSelectedForSync);
        return Ok(new
        {
            message = $"Value selection updated. {selectedCount} value(s) selected for sync.",
            selectedCount
        });
    }
}

public class CreateSyncFieldMappingRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string EventsAirFieldGuid { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; } = 0;
}

public class UpdateSyncFieldMappingRequest
{
    public string? DisplayName { get; set; }
    public string? EventsAirFieldGuid { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
}

public class AddSyncFieldValueRequest
{
    public string Value { get; set; } = string.Empty;
}

public class UpdateValueSelectionRequest
{
    public List<Guid> SelectedValueIds { get; set; } = new();
}
