using Humanitarian.Application.Resources;
using Humanitarian.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Humanitarian.Api.Controllers;

[ApiController]
[Route("api/resources")]
[Authorize]
public class ResourcesController(IResourceService service) : ControllerBase
{
    /// <summary>Inventory. Use lowStockOnly=true to see items that need replenishment.</summary>
    [HttpGet]
    [Authorize(Roles = ResourceRoles.View)]
    public async Task<IReadOnlyList<ResourceDto>> List(
        [FromQuery] ResourceCategory? category, [FromQuery] bool lowStockOnly = false, CancellationToken ct = default) =>
        await service.ListAsync(category, lowStockOnly, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Roles = ResourceRoles.View)]
    public async Task<ResourceDto> Get(Guid id, CancellationToken ct) => await service.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Roles = ResourceRoles.Manage)]
    [ProducesResponseType(typeof(ResourceDto), 201)]
    public async Task<IActionResult> Create(CreateResourceDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Add stock (replenishment).</summary>
    [HttpPost("{id:guid}/restock")]
    [Authorize(Roles = ResourceRoles.Manage)]
    public async Task<ResourceDto> Restock(Guid id, RestockDto dto, CancellationToken ct) =>
        await service.RestockAsync(id, dto, ct);

    [HttpPut("{id:guid}/active/{active:bool}")]
    [Authorize(Roles = ResourceRoles.Manage)]
    public async Task<ResourceDto> SetActive(Guid id, bool active, CancellationToken ct) =>
        await service.SetActiveAsync(id, active, ct);
}
