using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using QuanLyCafe.Application.Staff;

namespace QuanLyCafe.Api.Controllers.Staff;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<LoginResponse> Login(LoginRequest req, CancellationToken ct) => auth.LoginAsync(req, ct);

    [HttpPost("change-password")]
    [Authorize] // mọi vai trò
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value), req, ct);
        return NoContent();
    }
}
