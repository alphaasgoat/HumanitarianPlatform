using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Notifications;

public record OutboundMessage(Guid DeliveryId, string Destination, string Subject, string Body, string Language);

public record ChannelSendResult(bool Success, string Provider, string? Error = null)
{
    /// <summary>An error starting with this text will never succeed (for example an invalid address), so it is not retried.</summary>
    public const string PermanentPrefix = "[permanent] ";
}

/// <summary>
/// Strategy: one implementation per way of reaching a person (e-mail, SMS ...). Adding a channel means adding a class;
/// nothing else changes. Implementations live in the infrastructure layer.
/// </summary>
public interface INotificationChannel
{
    NotificationChannel Channel { get; }
    Task<ChannelSendResult> SendAsync(OutboundMessage message, CancellationToken ct = default);
}

/// <summary>Factory: picks the implementation for a channel.</summary>
public interface INotificationChannelFactory
{
    INotificationChannel? Resolve(NotificationChannel channel);
}

public class NotificationChannelFactory(IEnumerable<INotificationChannel> channels) : INotificationChannelFactory
{
    private readonly Dictionary<NotificationChannel, INotificationChannel> _byChannel =
        channels.GroupBy(c => c.Channel).ToDictionary(g => g.Key, g => g.First());

    public INotificationChannel? Resolve(NotificationChannel channel) =>
        _byChannel.TryGetValue(channel, out var impl) ? impl : null;
}

// ---- DTOs ----
public record NotificationDto(
    Guid Id, string Type, string Title, string Body, DateTime CreatedAtUtc, DateTime? ReadAtUtc,
    string? RelatedEntityType, string? RelatedEntityId);

public record NotificationPage(IReadOnlyList<NotificationDto> Items, int Total, int UnreadCount, int Page, int PageSize);

public record AnnouncementDto(Guid? CrisisEventId, string Title, string Message);

public record AnnouncementResultDto(Guid AnnouncementId, int Recipients, int Queued);

public record DeliveryDto(
    Guid Id, Guid NotificationId, string Channel, string Destination, string Status, int Attempts,
    DateTime NextAttemptUtc, DateTime? SentAtUtc, string? Provider, string? LastError, DateTime CreatedAtUtc);

public record DispatchSummary(int Claimed, int Sent, int Retried, int Failed);
