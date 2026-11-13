using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;

namespace Humanitarian.Infrastructure.Persistence;

public class AuditService(IUnitOfWork uow, ICurrentUser current) : IAuditService
{
    public async Task RecordAsync(string action, string entityType, string? entityId = null, string? details = null,
        Guid? actorId = null, string? actorEmail = null, CancellationToken ct = default)
    {
        await uow.Repository<AuditEvent>().AddAsync(new AuditEvent
        {
            ActorUserId = actorId ?? current.UserId,
            ActorEmail = actorEmail ?? current.Email,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            IpAddress = current.IpAddress
        }, ct);
    }
}
