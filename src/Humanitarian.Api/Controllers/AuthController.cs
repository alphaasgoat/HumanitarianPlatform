using Humanitarian.Application.Auth;
using Humanitarian.Application.Users;
using Humanitarian.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Humanitarian.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(IAuthService auth, IUserService users, ICurrentUser current) : ControllerBase
{
    /// <summary>Citizen self-registration (staff accounts are created by an administrator).</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), 201)]
    public async Task<IActionResult> Register(RegisterCitizenRequest request, CancellationToken ct)
    {
        var res = await auth.RegisterCitizenAsync(request, ct);
        return StatusCode(201, res);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) =>
        await auth.LoginAsync(request, ct);

    [Authorize]
    [HttpGet("me")]
    public async Task<UserDto> Me(CancellationToken ct) =>
        await users.GetAsync(current.UserId!.Value, ct);
}
