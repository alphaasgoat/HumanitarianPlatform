using Humanitarian.Application.Auth;
using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;

namespace Humanitarian.Application.Users;

public interface IUserService
{
    Task<UserDto> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default);
    Task<UserDto> GetAsync(Guid id, CancellationToken ct = default);
    Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default);
}

public class UserService(IUnitOfWork uow, IPasswordHasher hasher, IAuditService audit) : IUserService
{
    public static UserDto ToDto(User u) =>
        new(u.Id, u.Email, u.FullName, u.Role.ToString(), u.OrganisationId, u.IsActive);

    public async Task<UserDto> CreateStaffAsync(CreateStaffRequest r, CancellationToken ct = default)
    {
        var email = r.Email.Trim().ToLowerInvariant();
        var users = uow.Repository<User>();
        if (await users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account with this email already exists.");
        if (r.OrganisationId is { } orgId &&
            !await uow.Repository<Organisation>().AnyAsync(o => o.Id == orgId, ct))
            throw new AppValidationException("Organisation does not exist.");

        var user = new User
        {
            Email = email, FullName = r.FullName.Trim(), Role = r.Role,
            OrganisationId = r.OrganisationId, PasswordHash = hasher.Hash(r.Password)
        };
        await users.AddAsync(user, ct);
        await audit.RecordAsync("StaffCreated", nameof(User), user.Id.ToString(), $"Role={r.Role}", ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(user);
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default)
    {
        var list = await uow.Repository<User>().ListAsync(orderBy: q => q.OrderBy(u => u.Email), ct: ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var u = await uow.Repository<User>().GetByIdAsync(id, ct) ?? throw new NotFoundException("User not found.");
        return ToDto(u);
    }

    public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var repo = uow.Repository<User>();
        var u = await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("User not found.");
        u.IsActive = active;
        u.UpdatedAtUtc = DateTime.UtcNow;
        repo.Update(u);
        await audit.RecordAsync(active ? "UserActivated" : "UserDeactivated", nameof(User), id.ToString(), ct: ct);
        await uow.SaveChangesAsync(ct);
    }
}
