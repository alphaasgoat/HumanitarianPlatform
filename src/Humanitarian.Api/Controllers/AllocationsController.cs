using Humanitarian.Application.Common;
using Humanitarian.Application.Resources;
using Humanitarian.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Humanitarian.Api.Controllers;

[ApiController]
[Route("api/allocations")]
[Authorize]
public class AllocationsController(IAllocationService service) : ControllerBase
{
    /// <summary>Ranked resource candidates for a triaged request (preferred first, then alternatives).</summary>
    [HttpGet("recommend/{requestId:guid}")]
    [Authorize(Roles = ResourceRoles.View)]
    public async Task<MatchResultDto> Recommend(Guid requestId, CancellationToken ct) =>
        await service.RecommendAsync(requestId, ct);

    /// <summary>Approve an allocation (reserves the stock). Critical requests need an Emergency Coordinator.</summary>
    [HttpPost]
    [Authorize(Roles = ResourceRoles.Manage)]
    [ProducesResponseType(typeof(AllocationDto), 201)]
    public async Task<IActionResult> Allocate(AllocateDto dto, CancellationToken ct)
    {
        var allocation = await service.AllocateAsync(dto, ct);
        return CreatedAtAction(nameof(ListByRequest), new { requestId = allocation.RequestId }, allocation);
    }

    [HttpGet("request/{requestId:guid}")]
    [Authorize(Roles = ResourceRoles.View)]
    public async Task<IReadOnlyList<AllocationDto>> ListByRequest(Guid requestId, CancellationToken ct) =>
        await service.ListByRequestAsync(requestId, ct);

    [HttpGet("crisis/{crisisEventId:guid}")]
    [Authorize(Roles = ResourceRoles.View)]
    public async Task<IReadOnlyList<AllocationDto>> ListByCrisis(
        Guid crisisEventId, [FromQuery] AllocationStatus? status, CancellationToken ct) =>
        await service.ListByCrisisAsync(crisisEventId, status, ct);

    /// <summary>Shortage plan: how limited stock would be shared between competing requests. Changes nothing.</summary>
    [HttpGet("plan")]
    [Authorize(Roles = ResourceRoles.View)]
    public async Task<ShortagePlanDto> Plan(
        [FromQuery] Guid? crisisEventId, [FromQuery] ResourceCategory? category, CancellationToken ct)
    {
        if (crisisEventId is null || category is null)
            throw new AppValidationException("crisisEventId and category are required.");
        return await service.PlanAsync(crisisEventId.Value, category.Value, ct);
    }

    /// <summary>Apply the shortage plan (Emergency Coordinator only). Safe to call twice: nothing is duplicated.</summary>
    [HttpPost("plan/apply")]
    [Authorize(Roles = Roles.EmergencyCoordinator)]
    public async Task<ApplyPlanResultDto> ApplyPlan(
        [FromQuery] Guid? crisisEventId, [FromQuery] ResourceCategory? category, CancellationToken ct)
    {
        if (crisisEventId is null || category is null)
            throw new AppValidationException("crisisEventId and category are required.");
        return await service.ApplyPlanAsync(crisisEventId.Value, category.Value, ct);
    }

    [HttpPost("{id:guid}/dispatch")]
    [Authorize(Roles = ResourceRoles.Manage)]
    public async Task<AllocationDto> Dispatch(Guid id, CancellationToken ct) => await service.DispatchAsync(id, ct);

    [HttpPost("{id:guid}/deliver")]
    [Authorize(Roles = ResourceRoles.Manage)]
    public async Task<AllocationDto> Deliver(Guid id, CancellationToken ct) => await service.DeliverAsync(id, ct);

    /// <summary>Cancel an allocation and give the stock back.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = ResourceRoles.Manage)]
    public async Task<AllocationDto> Cancel(Guid id, CancelAllocationDto dto, CancellationToken ct) =>
        await service.CancelAsync(id, dto, ct);
}
