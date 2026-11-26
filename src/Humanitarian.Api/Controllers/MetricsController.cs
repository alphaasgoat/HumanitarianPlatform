using Humanitarian.Application.Metrics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Humanitarian.Api.Controllers;

[ApiController]
[Route("api/metrics")]
[Authorize(Roles = MetricsRoles.View)]
public class MetricsController(IMetricsService service) : ControllerBase
{
    /// <summary>Unresolved and overdue cases, response times, AI override rate, resource utilisation, shelter occupancy, coverage.</summary>
    [HttpGet("operational")]
    public async Task<OperationalMetricsDto> Operational([FromQuery] Guid? crisisEventId, CancellationToken ct) =>
        await service.GetOperationalAsync(crisisEventId, ct);
}
