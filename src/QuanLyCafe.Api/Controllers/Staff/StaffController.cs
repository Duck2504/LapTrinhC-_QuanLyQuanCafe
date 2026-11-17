using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using QuanLyCafe.Application.Staff;

namespace QuanLyCafe.Api.Controllers.Staff;

[ApiController]
[Route("api")]
[Authorize(Roles = "Manager")]
public class StaffController(StaffService staff) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet("users")]
    public Task<List<UserDto>> GetUsers(CancellationToken ct) => staff.GetUsersAsync(ct);

    [HttpPost("users")]
    public async Task<IActionResult> Create(CreateUserRequest req, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await staff.CreateAsync(req, CurrentUserId, ct));

    [HttpPut("users/{id:guid}")]
    public Task<UserDto> Update(Guid id, UpdateUserRequest req, CancellationToken ct) =>
        staff.UpdateAsync(id, req, CurrentUserId, ct);

    [HttpPost("users/{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await staff.DeactivateAsync(id, CurrentUserId, ct);
        return NoContent();
    }

    [HttpPost("users/{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest req, CancellationToken ct)
    {
        await staff.ResetPasswordAsync(id, req, CurrentUserId, ct);
        return NoContent();
    }

    [HttpGet("roles")]
    public Task<List<RoleDto>> GetRoles(CancellationToken ct) => staff.GetRolesAsync(ct);
}
