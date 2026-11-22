using Humanitarian.Application.Common;
using Humanitarian.Application.Volunteers;
using Humanitarian.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Humanitarian.Api.Controllers;

[ApiController]
[Route("api/volunteer-tasks")]
[Authorize]
public class VolunteerTasksController(IVolunteerTaskService service) : ControllerBase
{
    // ----- coordinators -----

    [HttpPost]
    [Authorize(Roles = VolunteerRoles.Coordinate)]
    [ProducesResponseType(typeof(TaskDto), 201)]
    public async Task<IActionResult> Create(CreateTaskDto dto, CancellationToken ct)
    {
        var task = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = task.Id }, task);
    }

    [HttpGet]
    [Authorize(Roles = VolunteerRoles.View)]
    public async Task<IReadOnlyList<TaskDto>> List(
        [FromQuery] Guid? crisisEventId, [FromQuery] VolunteerTaskStatus? status, CancellationToken ct) =>
        await service.ListAsync(crisisEventId, status, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Roles = VolunteerRoles.View)]
    public async Task<TaskDto> Get(Guid id, CancellationToken ct) => await service.GetAsync(id, ct);

    /// <summary>Ranked volunteers for a task (hard and soft constraints). A coordinator makes the decision.</summary>
    [HttpGet("{id:guid}/recommend")]
    [Authorize(Roles = VolunteerRoles.Coordinate)]
    public async Task<IReadOnlyList<VolunteerMatchDto>> Recommend(Guid id, CancellationToken ct) =>
        await service.RecommendAsync(id, ct);

    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = VolunteerRoles.Coordinate)]
    [ProducesResponseType(typeof(AssignmentDto), 201)]
    public async Task<IActionResult> Assign(Guid id, AssignDto dto, CancellationToken ct)
    {
        var assignment = await service.AssignAsync(id, dto, ct);
        return CreatedAtAction(nameof(ListAssignments), new { id }, assignment);
    }

    [HttpGet("{id:guid}/assignments")]
    [Authorize(Roles = VolunteerRoles.View)]
    public async Task<IReadOnlyList<AssignmentDto>> ListAssignments(Guid id, CancellationToken ct) =>
        await service.ListAssignmentsAsync(id, ct);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = VolunteerRoles.Coordinate)]
    public async Task<TaskDto> CancelTask(Guid id, ReasonDto dto, CancellationToken ct) =>
        await service.CancelTaskAsync(id, dto.Reason, ct);

    [HttpPost("assignments/{assignmentId:guid}/complete")]
    [Authorize(Roles = VolunteerRoles.Coordinate)]
    public async Task<AssignmentDto> Complete(Guid assignmentId, CompleteAssignmentDto dto, CancellationToken ct) =>
        await service.CompleteAsync(assignmentId, dto, ct);

    [HttpPost("assignments/{assignmentId:guid}/cancel")]
    [Authorize(Roles = VolunteerRoles.Coordinate)]
    public async Task<AssignmentDto> CancelAssignment(Guid assignmentId, ReasonDto dto, CancellationToken ct) =>
        await service.CancelAssignmentAsync(assignmentId, dto.Reason, ct);

    // ----- the volunteer (a citizen account) answers their own assignments -----

    [HttpGet("my-assignments")]
    [Authorize(Roles = Roles.Citizen)]
    public async Task<IReadOnlyList<AssignmentDto>> MyAssignments(CancellationToken ct) =>
        await service.MyAssignmentsAsync(ct);

    [HttpPost("assignments/{assignmentId:guid}/accept")]
    [Authorize(Roles = Roles.Citizen)]
    public async Task<AssignmentDto> Accept(Guid assignmentId, CancellationToken ct) =>
        await service.AcceptAsync(assignmentId, ct);

    [HttpPost("assignments/{assignmentId:guid}/decline")]
    [Authorize(Roles = Roles.Citizen)]
    public async Task<AssignmentDto> Decline(Guid assignmentId, ReasonDto dto, CancellationToken ct) =>
        await service.DeclineAsync(assignmentId, dto.Reason, ct);
}
