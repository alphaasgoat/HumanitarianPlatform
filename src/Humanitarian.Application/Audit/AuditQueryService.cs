using System.Linq.Expressions;
using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;

namespace Humanitarian.Application.Audit;

public record AuditEventDto(Guid Id, DateTime TimestampUtc, Guid? ActorUserId, string? ActorEmail,
    string Action, string EntityType, string? EntityId, string? Details, string? IpAddress);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public interface IAuditQueryService
{
    Task<PagedResult<AuditEventDto>> QueryAsync(string? action, string? entityType, int page, int pageSize, CancellationToken ct = default);
}

public class AuditQueryService(IUnitOfWork uow) : IAuditQueryService
{
    public async Task<PagedResult<AuditEventDto>> QueryAsync(string? action, string? entityType, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var repo = uow.Repository<AuditEvent>();
        Expression<Func<AuditEvent, bool>> filter = e =>
            (action == null || e.Action == action) && (entityType == null || e.EntityType == entityType);
        var total = await repo.CountAsync(filter, ct);
        var items = await repo.ListAsync(filter, q => q.OrderByDescending(e => e.CreatedAtUtc),
            (page - 1) * pageSize, pageSize, ct);
        return new PagedResult<AuditEventDto>(
            items.Select(e => new AuditEventDto(e.Id, e.CreatedAtUtc, e.ActorUserId, e.ActorEmail, e.Action,
                e.EntityType, e.EntityId, e.Details, e.IpAddress)).ToList(), total, page, pageSize);
    }
}
