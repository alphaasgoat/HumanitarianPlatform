using Humanitarian.Application.Common;
using Humanitarian.Application.Users;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterCitizenAsync(RegisterCitizenRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public class AuthService(
    IUnitOfWork uow, IPasswordHasher hasher, ITokenService tokens,
    IAuditService audit, TimeProvider time) : IAuthService
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<AuthResponse> RegisterCitizenAsync(RegisterCitizenRequest r, CancellationToken ct = default)
    {
        var email = r.Email.Trim().ToLowerInvariant();
        var users = uow.Repository<User>();
        if (await users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account with this email already exists.");

        // Self-registration can ONLY create Citizens. Staff are created by an administrator.
        var user = new User
        {
            Email = email,
            FullName = r.FullName.Trim(),
            Phone = r.Phone,
            PreferredLanguage = string.IsNullOrWhiteSpace(r.PreferredLanguage) ? "en" : r.PreferredLanguage!,
            Role = UserRole.Citizen,
            PasswordHash = hasher.Hash(r.Password)
        };
        await users.AddAsync(user, ct);
        await audit.RecordAsync("UserRegistered", nameof(User), user.Id.ToString(),
            actorId: user.Id, actorEmail: user.Email, ct: ct);
        await uow.SaveChangesAsync(ct);
        return Build(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest r, CancellationToken ct = default)
    {
        var email = r.Email.Trim().ToLowerInvariant();
        var now = time.GetUtcNow().UtcDateTime;
        var users = uow.Repository<User>();
        var user = await users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Same generic message for unknown user / wrong password: avoids account enumeration.
        const string generic = "Invalid email or password.";

        if (user is null || !user.IsActive)
        {
            await audit.RecordAsync("LoginFailed", nameof(User), null, $"Unknown or inactive account: {email}", ct: ct);
            await uow.SaveChangesAsync(ct);
            throw new AuthenticationFailedException(generic);
        }

        if (user.IsLockedOut(now))
        {
            await audit.RecordAsync("LoginBlockedLockout", nameof(User), user.Id.ToString(),
                actorId: user.Id, actorEmail: user.Email, ct: ct);
            await uow.SaveChangesAsync(ct);
            throw new AuthenticationFailedException("Account temporarily locked. Try again later.");
        }

        if (!hasher.Verify(r.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutEndUtc = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }
            users.Update(user);
            await audit.RecordAsync("LoginFailed", nameof(User), user.Id.ToString(),
                actorId: user.Id, actorEmail: user.Email, ct: ct);
            await uow.SaveChangesAsync(ct);
            throw new AuthenticationFailedException(generic);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        users.Update(user);
        await audit.RecordAsync("LoginSucceeded", nameof(User), user.Id.ToString(),
            actorId: user.Id, actorEmail: user.Email, ct: ct);
        await uow.SaveChangesAsync(ct);
        return Build(user);
    }

    private AuthResponse Build(User u)
    {
        var t = tokens.CreateToken(u);
        return new AuthResponse(t.Token, t.ExpiresAtUtc, UserService.ToDto(u));
    }
}
