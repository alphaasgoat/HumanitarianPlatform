using System.Linq.Expressions;
using Humanitarian.Application.Common;
using Humanitarian.Domain.Common;

namespace Humanitarian.Tests.Support;

/// <summary>Simple in-memory repository that honours predicates, ordering and paging.</summary>
public class InMemoryRepository<T> : IRepository<T> where T : BaseEntity
{
    public List<T> Items { get; } = new();

    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<T?>(Items.FirstOrDefault(e => e.Id == id));

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Task.FromResult<T?>(Items.AsQueryable().FirstOrDefault(predicate));

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Task.FromResult(Items.AsQueryable().Any(predicate));

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) =>
        Task.FromResult(predicate is null ? Items.Count : Items.AsQueryable().Count(predicate));

    public Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int? skip = null, int? take = null, CancellationToken ct = default)
    {
        IQueryable<T> q = Items.AsQueryable();
        if (predicate is not null) q = q.Where(predicate);
        if (orderBy is not null) q = orderBy(q);
        if (skip is not null) q = q.Skip(skip.Value);
        if (take is not null) q = q.Take(take.Value);
        return Task.FromResult<IReadOnlyList<T>>(q.ToList());
    }

    public Task AddAsync(T entity, CancellationToken ct = default)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(T entity) { }
    public void Remove(T entity) => Items.Remove(entity);
}

public class InMemoryUnitOfWork : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repos = new();
    public int SaveCount { get; private set; }

    public InMemoryRepository<T> Repo<T>() where T : BaseEntity
    {
        if (!_repos.TryGetValue(typeof(T), out var repo))
        {
            repo = new InMemoryRepository<T>();
            _repos[typeof(T)] = repo;
        }
        return (InMemoryRepository<T>)repo;
    }

    public IRepository<T> Repository<T>() where T : BaseEntity => Repo<T>();

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default) => await work();
}

public class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => UserId.HasValue;
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? IpAddress => "127.0.0.1";

    public void Become(Guid id, string email, string role)
    {
        UserId = id;
        Email = email;
        Role = role;
    }
}

public class RecordingAuditService : IAuditService
{
    public record Entry(string Action, string EntityType, string? EntityId, string? Details);

    public List<Entry> Entries { get; } = new();

    public Task RecordAsync(string action, string entityType, string? entityId = null, string? details = null,
        Guid? actorId = null, string? actorEmail = null, CancellationToken ct = default)
    {
        Entries.Add(new Entry(action, entityType, entityId, details));
        return Task.CompletedTask;
    }
}
