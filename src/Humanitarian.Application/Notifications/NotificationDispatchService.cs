using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Notifications;

public class DispatchOptions
{
    public int MaxAttempts { get; init; } = 5;
    public int BatchSize { get; init; } = 20;
    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan LockDuration { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan BaseBackoff { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromMinutes(30);
}

public interface INotificationDispatchService
{
    /// <summary>Sends every message that is due. Safe to call repeatedly and from several workers.</summary>
    Task<DispatchSummary> ProcessDueAsync(CancellationToken ct = default);
}

/// <summary>
/// The asynchronous half of the notification workflow (outbox consumer). A business action only saves the message in
/// the outbox; this service sends it later, so a slow or broken e-mail server can never block or fail a humanitarian
/// action. Failures are retried with exponential back-off and finally parked as Failed for a person to retry.
/// Each message is claimed (status Processing + a lease) before it is sent, so two workers never send it twice;
/// a worker that crashes mid-send releases the message when the lease expires (at-least-once delivery).
/// </summary>
public class NotificationDispatchService(
    IUnitOfWork uow, INotificationChannelFactory channels, TimeProvider time, DispatchOptions? options = null)
    : INotificationDispatchService
{
    private readonly DispatchOptions _o = options ?? new DispatchOptions();
    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<DispatchSummary> ProcessDueAsync(CancellationToken ct = default)
    {
        var now = Now;
        var deliveries = uow.Repository<NotificationDelivery>();
        var due = await deliveries.ListAsync(
            d => (d.Status == DeliveryStatus.Pending && d.NextAttemptUtc <= now) ||
                 (d.Status == DeliveryStatus.Processing && d.LockedUntilUtc != null && d.LockedUntilUtc <= now),
            q => q.OrderBy(d => d.NextAttemptUtc), take: _o.BatchSize, ct: ct);

        int sent = 0, retried = 0, failed = 0;
        foreach (var delivery in due)
        {
            // 1. Claim it. If another worker claimed it first, SaveChanges throws ConflictException (the caller
            //    simply tries again on its next cycle).
            delivery.Status = DeliveryStatus.Processing;
            delivery.LockedUntilUtc = Now + _o.LockDuration;
            delivery.Attempts++;
            deliveries.Update(delivery);
            await uow.SaveChangesAsync(ct);

            // 2. Send.
            var result = await SendAsync(delivery, ct);

            // 3. Record the outcome straight away.
            var finishedAt = Now;
            delivery.LockedUntilUtc = null;
            delivery.UpdatedAtUtc = finishedAt;
            delivery.Provider = result.Provider;
            if (result.Success)
            {
                delivery.Status = DeliveryStatus.Sent;
                delivery.SentAtUtc = finishedAt;
                delivery.LastError = null;
                sent++;
            }
            else if (delivery.Attempts >= _o.MaxAttempts || IsPermanent(result))
            {
                delivery.Status = DeliveryStatus.Failed;
                delivery.LastError = Trim(result.Error);
                failed++;
            }
            else
            {
                delivery.Status = DeliveryStatus.Pending;
                delivery.NextAttemptUtc = finishedAt + Backoff(delivery.Attempts);
                delivery.LastError = Trim(result.Error);
                retried++;
            }
            deliveries.Update(delivery);
            await uow.SaveChangesAsync(ct);
        }
        return new DispatchSummary(due.Count, sent, retried, failed);
    }

    private async Task<ChannelSendResult> SendAsync(NotificationDelivery delivery, CancellationToken ct)
    {
        var notification = await uow.Repository<Notification>().GetByIdAsync(delivery.NotificationId, ct);
        var channel = channels.Resolve(delivery.Channel);
        if (notification is null) return new ChannelSendResult(false, "none", ChannelSendResult.PermanentPrefix + "the notification no longer exists");
        if (channel is null) return new ChannelSendResult(false, "none", ChannelSendResult.PermanentPrefix + $"no {delivery.Channel} channel is configured");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_o.SendTimeout);
            return await channel.SendAsync(
                new OutboundMessage(delivery.Id, delivery.Destination, notification.Title, notification.Body, notification.Language), cts.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // shutting down: the lease will expire and the message will be picked up again
        }
        catch (Exception ex)
        {
            return new ChannelSendResult(false, channel.Channel.ToString(), $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool IsPermanent(ChannelSendResult r) =>
        r.Error?.StartsWith(ChannelSendResult.PermanentPrefix, StringComparison.Ordinal) == true;
    private static string? Trim(string? text) => text is null ? null : text.Length <= 400 ? text : text[..400];

    private TimeSpan Backoff(int attempts)
    {
        var seconds = _o.BaseBackoff.TotalSeconds * Math.Pow(2, Math.Max(0, attempts - 1));
        return TimeSpan.FromSeconds(Math.Min(seconds, _o.MaxBackoff.TotalSeconds));
    }
}
