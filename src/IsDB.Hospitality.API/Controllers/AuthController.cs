using IsDB.Hospitality.Application.DTOs.Auth;
using IsDB.Hospitality.Application.Features.Auth.Commands;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

public class AuthController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var result = await Mediator.Send(new LoginCommand(request.Email, request.Password));
        if (result == null) return Unauthorized(new { message = "Invalid email or password." });
        return Ok(result);
    }

    // PUT /api/auth/change-password
    // Any authenticated user can change their own password.
    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
            return BadRequest(new { message = "Current password is required." });

        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 4)
            return BadRequest(new { message = "New password must be at least 4 characters." });

        if (dto.NewPassword != dto.ConfirmPassword)
            return BadRequest(new { message = "New password and confirmation do not match." });

        var userId = CurrentUserId;
        var user = await _db.StaffUsers.FindAsync(userId);
        if (user == null) return NotFound(new { message = "User not found." });

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect." });

        if (BCrypt.Net.BCrypt.Verify(dto.NewPassword, user.PasswordHash))
            return BadRequest(new { message = "New password must be different from the current password." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Password updated successfully." });
    }
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record ChangePasswordDto(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword
);
