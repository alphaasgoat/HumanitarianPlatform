using Humanitarian.Application.Common;
using Humanitarian.Application.Volunteers;
using Humanitarian.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Humanitarian.Api.Controllers;

[ApiController]
[Route("api/volunteers")]
[Authorize]
public class VolunteersController(IVolunteerService service) : ControllerBase
{
    // ----- the volunteer's own profile (a citizen account) -----

    /// <summary>Register the signed-in citizen as a volunteer. A coordinator must verify the profile.</summary>
    [HttpPost("me")]
    [Authorize(Roles = Roles.Citizen)]
    [ProducesResponseType(typeof(VolunteerDto), 201)]
    public async Task<IActionResult> Register(RegisterVolunteerDto dto, CancellationToken ct)
    {
        var volunteer = await service.RegisterMeAsync(dto, ct);
        return CreatedAtAction(nameof(Me), volunteer);
    }

    [HttpGet("me")]
    [Authorize(Roles = Roles.Citizen)]
    public async Task<VolunteerDto> Me(CancellationToken ct) => await service.GetMeAsync(ct);

    /// <summary>Changing skills resets verification, because new skill claims must be checked again.</summary>
    [HttpPut("me")]
    [Authorize(Roles = Roles.Citizen)]
    public async Task<VolunteerDto> UpdateMe(RegisterVolunteerDto dto, CancellationToken ct) =>
        await service.UpdateMeAsync(dto, ct);

    [HttpPost("me/availability")]
    [Authorize(Roles = Roles.Citizen)]
    public async Task<AvailabilitySlotDto> AddAvailability(AvailabilityDto dto, CancellationToken ct) =>
        await service.AddAvailabilityAsync(dto, ct);

    [HttpGet("me/availability")]
    [Authorize(Roles = Roles.Citizen)]
    public async Task<IReadOnlyList<AvailabilitySlotDto>> MyAvailability(CancellationToken ct) =>
        await service.ListMyAvailabilityAsync(ct);

    // ----- coordinators -----

    [HttpGet]
    [Authorize(Roles = VolunteerRoles.View)]
    public async Task<IReadOnlyList<VolunteerDto>> List(
        [FromQuery] VolunteerSkill? skill, [FromQuery] bool? verified, CancellationToken ct) =>
        await service.ListAsync(skill, verified, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Roles = VolunteerRoles.View)]
    public async Task<VolunteerDto> Get(Guid id, CancellationToken ct) => await service.GetAsync(id, ct);

    /// <summary>Verify a volunteer. Only verified volunteers can be assigned to tasks.</summary>
    [HttpPost("{id:guid}/verify")]
    [Authorize(Roles = VolunteerRoles.Coordinate)]
    public async Task<VolunteerDto> Verify(Guid id, CancellationToken ct) => await service.VerifyAsync(id, ct);

    [HttpPut("{id:guid}/active/{active:bool}")]
    [Authorize(Roles = VolunteerRoles.Coordinate)]
    public async Task<VolunteerDto> SetActive(Guid id, bool active, CancellationToken ct) =>
        await service.SetActiveAsync(id, active, ct);
}
