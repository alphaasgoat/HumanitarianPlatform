using Humanitarian.Application.Common;
using Humanitarian.Application.Notifications;
using Humanitarian.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Humanitarian.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(INotificationService service) : ControllerBase
{
    /// <summary>My notifications (newest first) with the unread count.</summary>
    [HttpGet("mine")]
    public async Task<NotificationPage> Mine([FromQuery] bool unreadOnly = false, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await service.ListMineAsync(unreadOnly, page, pageSize, ct);

    [HttpGet("unread-count")]
    public async Task<object> UnreadCount(CancellationToken ct) => new { count = await service.UnreadCountAsync(ct) };

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await service.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<object> MarkAllRead(CancellationToken ct) => new { marked = await service.MarkAllReadAsync(ct) };

    /// <summary>Emergency announcement to everyone who asked for help in a crisis (or to all citizens).</summary>
    [HttpPost("announcements")]
    [Authorize(Roles = Roles.CoordinatorOrAdmin)]
    public async Task<AnnouncementResultDto> Announce(AnnouncementDto dto, CancellationToken ct) =>
        await service.AnnounceAsync(dto, ct);

    /// <summary>The outbound message queue (e-mail / SMS), for monitoring failures.</summary>
    [HttpGet("deliveries")]
    [Authorize(Roles = Roles.CoordinatorOrAdmin)]
    public async Task<IReadOnlyList<DeliveryDto>> Deliveries([FromQuery] DeliveryStatus? status, [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        await service.ListDeliveriesAsync(status, take, ct);

    /// <summary>Put a failed message back in the queue.</summary>
    [HttpPost("deliveries/{id:guid}/retry")]
    [Authorize(Roles = Roles.CoordinatorOrAdmin)]
    public async Task<DeliveryDto> Retry(Guid id, CancellationToken ct) => await service.RetryDeliveryAsync(id, ct);
}
