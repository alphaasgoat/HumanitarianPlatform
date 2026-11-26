using Humanitarian.Application.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Humanitarian.Infrastructure.Notifications;

/// <summary>
/// In-process event bus. Handlers run inside the caller's unit of work, so what they stage (for example a notification
/// and its outbox rows) is saved atomically with the business change. A failing handler is logged and skipped: a broken
/// notification must never stop a humanitarian action such as approving a request.
/// </summary>
public class InProcessDomainEventPublisher(IServiceProvider services, ILogger<InProcessDomainEventPublisher> log)
    : IDomainEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default) where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
        {
            try
            {
                await handler.HandleAsync(domainEvent, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Handler {Handler} failed for event {Event} ({EventId}); continuing.",
                    handler.GetType().Name, typeof(TEvent).Name, domainEvent.EventId);
            }
        }
    }
}
