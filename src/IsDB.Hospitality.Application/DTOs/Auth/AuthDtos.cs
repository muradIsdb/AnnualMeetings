namespace IsDB.Hospitality.Application.DTOs.Auth;

public class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public StaffUserDto User { get; set; } = null!;
}

public class StaffUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    /// <summary>Primary role (legacy, kept for compatibility).</summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>All roles assigned to this user.</summary>
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; }
}

public class RefreshTokenRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class CreateStaffUserDto
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

public class UpdateStaffUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Password { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; } = true;
}
