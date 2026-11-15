using Humanitarian.Application.Audit;
using Humanitarian.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Humanitarian.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = Roles.SystemAdministrator)]
public class AuditController(IAuditQueryService audit) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<AuditEventDto>> Query(
        [FromQuery] string? action, [FromQuery] string? entityType,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        await audit.QueryAsync(action, entityType, page, pageSize, ct);
}
