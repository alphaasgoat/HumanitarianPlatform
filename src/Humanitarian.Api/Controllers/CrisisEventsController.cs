using Humanitarian.Application.Common;
using Humanitarian.Application.Crisis;
using Humanitarian.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Humanitarian.Api.Controllers;

[ApiController]
[Route("api/crisis-events")]
[Authorize]
public class CrisisEventsController(ICrisisService crises) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<CrisisDto>> List([FromQuery] CrisisStatus? status, CancellationToken ct) =>
        await crises.ListAsync(status, ct);

    [HttpGet("{id:guid}")]
    public async Task<CrisisDto> Get(Guid id, CancellationToken ct) => await crises.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Roles = Roles.CoordinatorOrAdmin)]
    [ProducesResponseType(typeof(CrisisDto), 201)]
    public async Task<IActionResult> Create(CreateCrisisRequest request, CancellationToken ct)
    {
        var c = await crises.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = c.Id }, c);
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = Roles.CoordinatorOrAdmin)]
    public async Task<CrisisDto> ChangeStatus(Guid id, ChangeCrisisStatusRequest request, CancellationToken ct) =>
        await crises.ChangeStatusAsync(id, request.Status, ct);

    [HttpPost("{id:guid}/areas")]
    [Authorize(Roles = Roles.CoordinatorOrAdmin)]
    public async Task<CrisisDto> AddArea(Guid id, AddAreaRequest request, CancellationToken ct) =>
        await crises.AddAreaAsync(id, request, ct);
}
