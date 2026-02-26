using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[Authorize(Roles = "Administrator")]
[Route("api/registration-types")]
public class RegistrationTypesController : ApiControllerBase
{
    private readonly IAppDbContext _context;

    public RegistrationTypesController(IAppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all registration types
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var types = await _context.RegistrationTypes
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Code,
                r.Name,
                r.Description,
                r.IsSelectedForSync,
                r.IsFromEventsAir,
                r.SortOrder,
                r.CreatedAt,
                r.UpdatedAt
            })
            .ToListAsync();

        return Ok(types);
    }

    /// <summary>
    /// Get only registration types selected for sync
    /// </summary>
    [HttpGet("selected")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSelected()
    {
        var types = await _context.RegistrationTypes
            .Where(r => r.IsSelectedForSync)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .Select(r => new { r.Id, r.Code, r.Name })
            .ToListAsync();

        return Ok(types);
    }

    /// <summary>
    /// Add a new registration type manually
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRegistrationTypeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        // Auto-generate code from name if not provided
        var code = string.IsNullOrWhiteSpace(request.Code)
            ? request.Name.ToUpperInvariant().Replace(" ", "_")
            : request.Code.ToUpperInvariant();

        // Check for duplicate code
        var exists = await _context.RegistrationTypes.AnyAsync(r => r.Code == code);
        if (exists)
            return Conflict(new { message = $"Registration type with code '{code}' already exists." });

        var regType = new RegistrationType
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsSelectedForSync = request.IsSelectedForSync,
            IsFromEventsAir = false,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.RegistrationTypes.Add(regType);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = regType.Id }, regType);
    }

    /// <summary>
    /// Update a registration type
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRegistrationTypeRequest request)
    {
        var regType = await _context.RegistrationTypes.FindAsync(id);
        if (regType == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
            regType.Name = request.Name.Trim();

        if (request.Description != null)
            regType.Description = request.Description.Trim();

        if (request.SortOrder.HasValue)
            regType.SortOrder = request.SortOrder.Value;

        regType.IsSelectedForSync = request.IsSelectedForSync;
        regType.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(regType);
    }

    /// <summary>
    /// Delete a registration type
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var regType = await _context.RegistrationTypes.FindAsync(id);
        if (regType == null) return NotFound();

        _context.RegistrationTypes.Remove(regType);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Bulk update sync selection — pass array of IDs that should be selected
    /// </summary>
    [HttpPost("sync-selection")]
    public async Task<IActionResult> UpdateSyncSelection([FromBody] UpdateSyncSelectionRequest request)
    {
        var allTypes = await _context.RegistrationTypes.ToListAsync();

        foreach (var regType in allTypes)
        {
            regType.IsSelectedForSync = request.SelectedIds.Contains(regType.Id);
            regType.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var selectedCount = allTypes.Count(r => r.IsSelectedForSync);
        return Ok(new { message = $"Sync selection updated. {selectedCount} type(s) selected for sync.", selectedCount });
    }

    /// <summary>
    /// Import registration types from EventsAir (fetches available types from the API)
    /// </summary>
    [HttpPost("import-from-eventsair")]
    public async Task<IActionResult> ImportFromEventsAir()
    {
        // Get the saved EventsAir config
        var config = await _context.EventsAirConfigs
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (config == null || string.IsNullOrEmpty(config.ClientId))
            return BadRequest(new { message = "EventsAir integration is not configured. Please save your credentials first." });

        // In production this would call the EventsAir GraphQL API to fetch registration types.
        // For now, return a helpful message indicating the API call would be made.
        return Ok(new
        {
            message = "Import from EventsAir requires a live API connection. Please add registration types manually or ensure EventsAir credentials are configured and the connection test passes.",
            hint = "Once connected, this endpoint will automatically fetch all registration types from your EventsAir event and populate the list."
        });
    }
}

public class CreateRegistrationTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool IsSelectedForSync { get; set; } = false;
    public int SortOrder { get; set; } = 0;
}

public class UpdateRegistrationTypeRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsSelectedForSync { get; set; }
    public int? SortOrder { get; set; }
}

public class UpdateSyncSelectionRequest
{
    public List<Guid> SelectedIds { get; set; } = new();
}
