using Humanitarian.Application.Auth;
using Humanitarian.Application.Common;
using Humanitarian.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Humanitarian.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.SystemAdministrator)]
public class UsersController(IUserService users) : ControllerBase
{
    [HttpGet] public async Task<IReadOnlyList<UserDto>> List(CancellationToken ct) => await users.ListAsync(ct);

    [HttpPost("staff")]
    [ProducesResponseType(typeof(UserDto), 201)]
    public async Task<IActionResult> CreateStaff(CreateStaffRequest request, CancellationToken ct)
    {
        var u = await users.CreateStaffAsync(request, ct);
        return CreatedAtAction(nameof(List), new { id = u.Id }, u);
    }

    [HttpPut("{id:guid}/active/{active:bool}")]
    public async Task<IActionResult> SetActive(Guid id, bool active, CancellationToken ct)
    {
        await users.SetActiveAsync(id, active, ct);
        return NoContent();
    }
}
