using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

/// <summary>
/// Manages page-level access permissions per role.
/// Admin role always has implicit access to all pages.
/// </summary>
[Route("api/access-control")]
[Authorize]
public class AccessControlController : ApiControllerBase
{
    private readonly IAppDbContext _db;

    public AccessControlController(IAppDbContext db)
    {
        _db = db;
    }

    // ─── DTOs ─────────────────────────────────────────────────────────────────

    public record PagePermissionDto(string Role, string PageId, bool IsGranted);
    public record UpdatePermissionsRequest(List<PagePermissionDto> Permissions);
    public record MyPermissionsResponse(bool IsAdmin, List<string> GrantedPageIds);

    // ─── GET /api/access-control/permissions ──────────────────────────────────

    /// <summary>
    /// Returns all page permissions for all non-Admin roles.
    /// Admin-only endpoint.
    /// </summary>
    [HttpGet("permissions")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<PagePermissionDto>>> GetAllPermissions()
    {
        var perms = await _db.PagePermissions
            .OrderBy(p => p.Role)
            .ThenBy(p => p.PageId)
            .Select(p => new PagePermissionDto(p.Role.ToString(), p.PageId, p.IsGranted))
            .ToListAsync();

        return Ok(perms);
    }

    // ─── PUT /api/access-control/permissions ──────────────────────────────────

    /// <summary>
    /// Replaces all page permissions with the provided set.
    /// Admin-only endpoint.
    /// </summary>
    [HttpPut("permissions")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdatePermissions([FromBody] UpdatePermissionsRequest request)
    {
        if (request?.Permissions == null)
            return BadRequest("Permissions list is required.");

        // Remove all existing non-Admin permissions
        var existing = await _db.PagePermissions.ToListAsync();
        _db.PagePermissions.RemoveRange(existing);

        // Insert the new set (Admin role is never stored — always implicit)
        var newPerms = request.Permissions
            .Where(p => !string.Equals(p.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            .Select(p =>
            {
                if (!Enum.TryParse<UserRole>(p.Role, out var role))
                    throw new ArgumentException($"Unknown role: {p.Role}");

                return new PagePermission
                {
                    Role = role,
                    PageId = p.PageId,
                    IsGranted = p.IsGranted
                };
            }).ToList();

        await _db.PagePermissions.AddRangeAsync(newPerms);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ─── GET /api/access-control/my-permissions ───────────────────────────────

    /// <summary>
    /// Returns the list of page IDs the current user is permitted to access.
    /// If the user is Admin, returns IsAdmin=true with an empty list (frontend grants all).
    /// </summary>
    [HttpGet("my-permissions")]
    public async Task<ActionResult<MyPermissionsResponse>> GetMyPermissions()
    {
        var userId = CurrentUserId;
        if (userId == Guid.Empty)
            return Unauthorized();

        var user = await _db.StaffUsers
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return Unauthorized();

        // Collect all roles for this user
        var userRoles = user.Roles?.Select(r => r.Role).ToList()
            ?? new List<UserRole> { user.Role };

        // Admin always has full access
        if (userRoles.Contains(UserRole.Admin))
            return Ok(new MyPermissionsResponse(true, new List<string>()));

        // Fetch granted page IDs for all user roles
        var grantedPages = await _db.PagePermissions
            .Where(p => userRoles.Contains(p.Role) && p.IsGranted)
            .Select(p => p.PageId)
            .Distinct()
            .ToListAsync();

        return Ok(new MyPermissionsResponse(false, grantedPages));
    }
}
