using Humanitarian.Application.Notifications;

namespace Humanitarian.Api.Workers;

/// <summary>
/// Background worker (the consumer side of the notification outbox). Every few seconds it sends the messages that are
/// due. A failed cycle is logged and retried on the next cycle; the API keeps serving requests regardless.
/// </summary>
public class NotificationWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<NotificationWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(config.GetValue<int?>("Notifications:PollSeconds") ?? 10, 1, 300));
        log.LogInformation("Notification worker started (polling every {Seconds}s).", interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatchService>();
                var summary = await dispatcher.ProcessDueAsync(stoppingToken);
                if (summary.Claimed > 0)
                    log.LogInformation("Notifications: {Claimed} processed, {Sent} sent, {Retried} to retry, {Failed} failed.",
                        summary.Claimed, summary.Sent, summary.Retried, summary.Failed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Notification dispatch cycle failed; it will be tried again.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
