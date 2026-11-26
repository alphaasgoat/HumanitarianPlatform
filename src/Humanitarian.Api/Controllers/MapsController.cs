using Humanitarian.Application.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Humanitarian.Api.Controllers;

/// <summary>
/// Location services backed by an external mapping provider. They never fail because the provider is down: the response
/// says so (and offers a fallback) instead. A stricter rate limit protects the provider from abuse.
/// </summary>
[ApiController]
[Route("api/maps")]
[Authorize]
[EnableRateLimiting("maps")]
public class MapsController(IMappingService service) : ControllerBase
{
    /// <summary>Address to coordinates.</summary>
    [HttpGet("geocode")]
    public async Task<GeocodeResultDto> Geocode([FromQuery] string address, CancellationToken ct) =>
        await service.GeocodeAsync(address, ct);

    /// <summary>Coordinates to address.</summary>
    [HttpGet("reverse")]
    public async Task<GeocodeResultDto> Reverse([FromQuery] double lat, [FromQuery] double lng, CancellationToken ct) =>
        await service.ReverseGeocodeAsync(lat, lng, ct);

    /// <summary>Driving distance and time between two points (an estimate if the routing service is unavailable).</summary>
    [HttpGet("route")]
    public async Task<RouteResultDto> Route([FromQuery] double fromLat, [FromQuery] double fromLng,
        [FromQuery] double toLat, [FromQuery] double toLng, CancellationToken ct) =>
        await service.GetRouteAsync(fromLat, fromLng, toLat, toLng, ct);
}
