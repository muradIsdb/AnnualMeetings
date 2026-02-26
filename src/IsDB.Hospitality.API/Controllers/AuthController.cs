using IsDB.Hospitality.Application.DTOs.Auth;
using IsDB.Hospitality.Application.Features.Auth.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsDB.Hospitality.API.Controllers;

public class AuthController : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var result = await Mediator.Send(new LoginCommand(request.Email, request.Password));
        if (result == null) return Unauthorized(new { message = "Invalid email or password." });
        return Ok(result);
    }
}
