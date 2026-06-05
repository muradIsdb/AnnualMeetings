using IsDB.Hospitality.Application.DTOs.Auth;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

/// <summary>
/// Staff user management — Admin only.
/// </summary>
[Authorize(Roles = "Admin")]
public class StaffController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public StaffController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/staff
    [HttpGet]
    public async Task<ActionResult<List<StaffUserDto>>> GetAll()
    {
        var users = await _db.StaffUsers
            .Include(u => u.Roles)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        return Ok(users.Select(MapToDto).ToList());
    }

    // GET /api/staff/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffUserDto>> GetById(Guid id)
    {
        var user = await _db.StaffUsers.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();
        return Ok(MapToDto(user));
    }

    // POST /api/staff
    [HttpPost]
    public async Task<ActionResult<StaffUserDto>> Create([FromBody] CreateStaffUserDto dto)
    {
        if (await _db.StaffUsers.AnyAsync(u => u.Email == dto.Email))
            return Conflict(new { message = "Email already in use." });

        var parsedRoles = ParseRoles(dto.Roles);
        if (!parsedRoles.Any())
            return BadRequest(new { message = "At least one valid role is required." });

        var user = new StaffUser
        {
            Id = Guid.NewGuid(),
            Email = dto.Email.Trim().ToLower(),
            FullName = dto.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = parsedRoles.First(),
            IsActive = true,
        };

        user.Roles = parsedRoles.Select(r => new StaffUserRole
        {
            StaffUserId = user.Id,
            Role = r,
            AssignedAt = DateTime.UtcNow
        }).ToList();

        _db.StaffUsers.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, MapToDto(user));
    }

    // PUT /api/staff/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStaffUserDto dto)
    {
        var user = await _db.StaffUsers.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var parsedRoles = ParseRoles(dto.Roles);
        if (!parsedRoles.Any())
            return BadRequest(new { message = "At least one valid role is required." });

        user.FullName = dto.FullName.Trim();
        user.IsActive = dto.IsActive;
        user.Role = parsedRoles.First();

        if (!string.IsNullOrWhiteSpace(dto.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        // Fix 2: If deactivating, immediately invalidate the refresh token so the
        // user cannot obtain a new access token even if they still hold a refresh token.
        if (!dto.IsActive)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiresAt = DateTime.UtcNow.AddYears(-1);
        }

        // Replace roles
        _db.StaffUserRoles.RemoveRange(user.Roles);
        user.Roles = parsedRoles.Select(r => new StaffUserRole
        {
            StaffUserId = user.Id,
            Role = r,
            AssignedAt = DateTime.UtcNow
        }).ToList();

        await _db.SaveChangesAsync();
        return Ok(MapToDto(user));
    }

    // PUT /api/staff/{id}/reset-password
    [HttpPut("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 4)
            return BadRequest(new { message = "New password must be at least 4 characters." });

        var user = await _db.StaffUsers.FindAsync(id);
        if (user == null) return NotFound();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Password reset successfully." });
    }

    // DELETE /api/staff/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _db.StaffUsers.FindAsync(id);
        if (user == null) return NotFound();

        // Soft delete — deactivate and immediately invalidate refresh token (Fix 2)
        user.IsActive = false;
        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddYears(-1);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static StaffUserDto MapToDto(StaffUser user)
    {
        var roleNames = user.Roles.Select(r => r.Role.ToString()).ToList();
        if (!roleNames.Any()) roleNames.Add(user.Role.ToString());

        return new StaffUserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = roleNames.First(),
            Roles = roleNames,
            IsActive = user.IsActive
        };
    }

    // ─── DTOs ─────────────────────────────────────────────────────────────────

    public record ResetPasswordDto(string NewPassword);

    private static List<UserRole> ParseRoles(List<string> roleStrings)
    {
        var result = new List<UserRole>();
        foreach (var r in roleStrings)
        {
            if (Enum.TryParse<UserRole>(r, true, out var parsed))
                result.Add(parsed);
        }
        return result.Distinct().ToList();
    }
}
