using Humanitarian.Application.Common;
using Humanitarian.Application.Requests;
using Humanitarian.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Humanitarian.Api.Controllers;

[ApiController]
[Route("api/requests")]
[Authorize]
public class AssistanceRequestsController(IAssistanceRequestService service) : ControllerBase
{
    /// <summary>Submit an assistance request (affected person). Send an idempotencyKey to make retries safe.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Citizen)]
    [ProducesResponseType(typeof(RequestDetailDto), 201)]
    [ProducesResponseType(typeof(RequestDetailDto), 200)]
    public async Task<IActionResult> Submit(SubmitRequestDto dto, CancellationToken ct)
    {
        var result = await service.SubmitAsync(dto, ct);
        if (result.WasExisting) return Ok(result.Detail);
        return CreatedAtAction(nameof(Get), new { id = result.Detail.Request.Id }, result.Detail);
    }

    /// <summary>The caller's own requests.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = Roles.Citizen)]
    public async Task<RequestPage> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await service.GetMineAsync(page, pageSize, ct);

    /// <summary>Coordinator queue, highest priority first.</summary>
    [HttpGet]
    [Authorize(Roles = RequestRoles.Staff)]
    public async Task<RequestPage> List(
        [FromQuery] Guid? crisisEventId, [FromQuery] RequestStatus? status,
        [FromQuery] UrgencyLevel? urgency, [FromQuery] RequestCategory? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await service.ListAsync(new RequestFilter(crisisEventId, status, urgency, category), page, pageSize, ct);

    /// <summary>Request detail. Staff also see the AI recommendation, possible duplicates and the full timeline.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Citizen + "," + RequestRoles.Staff)]
    public async Task<RequestDetailDto> Get(Guid id, CancellationToken ct) => await service.GetAsync(id, ct);

    /// <summary>Generate a fresh AI recommendation (the previous pending one is superseded).</summary>
    [HttpPost("{id:guid}/analyze")]
    [Authorize(Roles = RequestRoles.Staff)]
    public async Task<RequestDetailDto> Analyze(Guid id, CancellationToken ct) => await service.AnalyzeAsync(id, ct);

    /// <summary>Approve, modify, reject or defer the AI recommendation. Modify and Reject need a note.</summary>
    [HttpPost("{id:guid}/review")]
    [Authorize(Roles = RequestRoles.Staff)]
    public async Task<RequestDetailDto> Review(Guid id, ReviewRequestDto dto, CancellationToken ct) =>
        await service.ReviewAsync(id, dto, ct);

    [HttpPost("{id:guid}/escalate")]
    [Authorize(Roles = RequestRoles.Staff)]
    public async Task<RequestDetailDto> Escalate(Guid id, NoteRequestDto dto, CancellationToken ct) =>
        await service.EscalateAsync(id, dto.Note, ct);

    /// <summary>Merge this request into another (confirmed duplicate). Nothing is deleted.</summary>
    [HttpPost("{id:guid}/merge")]
    [Authorize(Roles = RequestRoles.Staff)]
    public async Task<RequestDetailDto> Merge(Guid id, MergeRequestDto dto, CancellationToken ct) =>
        await service.MergeAsync(id, dto, ct);

    [HttpPost("{id:guid}/duplicates/{linkId:guid}/dismiss")]
    [Authorize(Roles = RequestRoles.Staff)]
    public async Task<RequestDetailDto> DismissDuplicate(Guid id, Guid linkId, CancellationToken ct) =>
        await service.DismissDuplicateAsync(id, linkId, ct);

    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = RequestRoles.Staff)]
    public async Task<RequestDetailDto> Close(Guid id, NoteRequestDto dto, CancellationToken ct) =>
        await service.CloseAsync(id, dto.Note, ct);
}
