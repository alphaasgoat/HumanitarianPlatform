using System.Linq.Expressions;
using Humanitarian.Domain.Common;
using Humanitarian.Domain.Entities;

namespace Humanitarian.Application.Common;

/// <summary>Repository pattern: hides EF Core from the business layer and makes services unit-testable.</summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int? skip = null, int? take = null,
        CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

/// <summary>Unit of Work: one SaveChanges / one transaction across several repositories.</summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public record TokenResult(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    TokenResult CreateToken(User user);
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    string? IpAddress { get; }
}

public interface IAuditService
{
    /// <summary>Stages an audit record in the current unit of work. The caller commits with SaveChangesAsync,
    /// so the audit row is saved atomically with the business change it describes.</summary>
    Task RecordAsync(string action, string entityType, string? entityId = null, string? details = null,
        Guid? actorId = null, string? actorEmail = null, CancellationToken ct = default);
}
