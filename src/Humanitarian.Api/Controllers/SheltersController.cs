using Humanitarian.Application.Shelters;
using Humanitarian.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Humanitarian.Api.Controllers;

[ApiController]
[Route("api/shelters")]
[Authorize]
public class SheltersController(IShelterService service) : ControllerBase
{
    /// <summary>Verified, open shelters for affected people. Shows a coarse availability level only.</summary>
    [HttpGet("public")]
    public async Task<IReadOnlyList<PublicShelterDto>> ListPublic([FromQuery] Guid? crisisEventId, CancellationToken ct) =>
        await service.ListPublicAsync(crisisEventId, ct);

    /// <summary>All shelters with exact occupancy (staff).</summary>
    [HttpGet]
    [Authorize(Roles = ShelterRoles.View)]
    public async Task<IReadOnlyList<ShelterDto>> List([FromQuery] Guid? crisisEventId, CancellationToken ct) =>
        await service.ListAsync(crisisEventId, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Roles = ShelterRoles.View)]
    public async Task<ShelterDto> Get(Guid id, CancellationToken ct) => await service.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Roles = ShelterRoles.Manage)]
    [ProducesResponseType(typeof(ShelterDto), 201)]
    public async Task<IActionResult> Create(CreateShelterDto dto, CancellationToken ct)
    {
        var shelter = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = shelter.Id }, shelter);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ShelterRoles.Manage)]
    public async Task<ShelterDto> Update(Guid id, UpdateShelterDto dto, CancellationToken ct) =>
        await service.UpdateAsync(id, dto, ct);

    /// <summary>Open or close a shelter. A shelter with people placed in it cannot be closed.</summary>
    [HttpPut("{id:guid}/status/{status}")]
    [Authorize(Roles = ShelterRoles.Manage)]
    public async Task<ShelterDto> SetStatus(Guid id, ShelterStatus status, CancellationToken ct) =>
        await service.SetStatusAsync(id, status, ct);

    /// <summary>Ranked shelter candidates for a triaged request (hard and soft constraints, with warnings).</summary>
    [HttpGet("recommend/{requestId:guid}")]
    [Authorize(Roles = ShelterRoles.View)]
    public async Task<ShelterRecommendationDto> Recommend(Guid requestId, CancellationToken ct) =>
        await service.RecommendAsync(requestId, ct);

    /// <summary>Place a household in a shelter. Fails with 409 if capacity would be exceeded.</summary>
    [HttpPost("allocate")]
    [Authorize(Roles = ShelterRoles.Manage)]
    [ProducesResponseType(typeof(ShelterAllocationDto), 201)]
    public async Task<IActionResult> Allocate(AllocateShelterDto dto, CancellationToken ct)
    {
        var allocation = await service.AllocateAsync(dto, ct);
        return CreatedAtAction(nameof(ListAllocations), new { id = allocation.ShelterId }, allocation);
    }

    [HttpGet("{id:guid}/allocations")]
    [Authorize(Roles = ShelterRoles.View)]
    public async Task<IReadOnlyList<ShelterAllocationDto>> ListAllocations(
        Guid id, [FromQuery] bool activeOnly = true, CancellationToken ct = default) =>
        await service.ListAllocationsAsync(id, activeOnly, ct);

    /// <summary>Check a household out of the shelter and free the spaces.</summary>
    [HttpPost("allocations/{allocationId:guid}/release")]
    [Authorize(Roles = ShelterRoles.Manage)]
    public async Task<ShelterAllocationDto> Release(Guid allocationId, ReleaseShelterDto dto, CancellationToken ct) =>
        await service.ReleaseAsync(allocationId, dto, ct);

    /// <summary>Ask logistics for more supplies when stock at the shelter is not enough.</summary>
    [HttpPost("{id:guid}/supply-requests")]
    [Authorize(Roles = ShelterRoles.Manage)]
    public async Task<SupplyRequestDto> RequestSupplies(Guid id, CreateSupplyRequestDto dto, CancellationToken ct) =>
        await service.RequestSuppliesAsync(id, dto, ct);

    [HttpGet("supply-requests")]
    [Authorize(Roles = ShelterRoles.Supplies)]
    public async Task<IReadOnlyList<SupplyRequestDto>> ListSupplyRequests([FromQuery] SupplyRequestStatus? status, CancellationToken ct) =>
        await service.ListSupplyRequestsAsync(status, ct);

    [HttpPost("supply-requests/{requestId:guid}/resolve")]
    [Authorize(Roles = ShelterRoles.Supplies)]
    public async Task<SupplyRequestDto> ResolveSupplyRequest(Guid requestId, ResolveSupplyRequestDto dto, CancellationToken ct) =>
        await service.ResolveSupplyRequestAsync(requestId, dto, ct);
}
