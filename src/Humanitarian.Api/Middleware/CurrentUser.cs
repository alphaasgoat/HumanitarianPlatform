using Humanitarian.Application.Common;

namespace Humanitarian.Api.Middleware;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? P => accessor.HttpContext?.User;
    public bool IsAuthenticated => P?.Identity?.IsAuthenticated == true;
    public Guid? UserId => Guid.TryParse(P?.FindFirst("sub")?.Value, out var id) ? id : null;
    public string? Email => P?.FindFirst("email")?.Value;
    public string? Role => P?.FindFirst("role")?.Value;
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
