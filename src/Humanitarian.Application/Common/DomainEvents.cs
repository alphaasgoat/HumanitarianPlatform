using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Common;

// ---------------------------------------------------------------------------------------------------------------
// Observer / publish-subscribe: a service announces WHAT happened; any number of handlers decide what to do about it.
// The request, shelter and volunteer services know nothing about notifications.
// ---------------------------------------------------------------------------------------------------------------

public interface IDomainEvent
{
    /// <summary>Stable identity of this occurrence. Handlers use it so the same event is never acted on twice.</summary>
    Guid EventId { get; }
}

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}

public interface IDomainEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default) where TEvent : IDomainEvent;
}

/// <summary>Used when nothing is subscribed (and by tests that do not care about events).</summary>
public sealed class NullDomainEventPublisher : IDomainEventPublisher
{
    public static readonly NullDomainEventPublisher Instance = new();

    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default) where TEvent : IDomainEvent =>
        Task.CompletedTask;
}

public record RequestStatusChanged(
    Guid EventId, Guid RequestId, Guid RequesterId, RequestStatus? From, RequestStatus To) : IDomainEvent;

public record ShelterPlacementMade(
    Guid EventId, Guid RequestId, Guid RequesterId, string ShelterName, string ShelterAddress, int People) : IDomainEvent;

public record VolunteerAssigned(
    Guid EventId, Guid VolunteerUserId, Guid TaskId, string TaskTitle, DateTime StartUtc, DateTime EndUtc) : IDomainEvent;
