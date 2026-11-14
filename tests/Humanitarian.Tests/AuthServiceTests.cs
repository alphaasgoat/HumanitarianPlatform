using Xunit;
using FluentAssertions;
using Humanitarian.Application.Auth;
using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Moq;

namespace Humanitarian.Tests;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRepository<User>> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _uow.Setup(u => u.Repository<User>()).Returns(_users.Object);
        _tokens.Setup(t => t.CreateToken(It.IsAny<User>()))
            .Returns(new TokenResult("jwt", DateTime.UtcNow.AddHours(1)));
        _sut = new AuthService(_uow.Object, _hasher.Object, _tokens.Object, _audit.Object, TimeProvider.System);
    }

    private static User ExistingUser() => new()
    {
        Email = "a@b.org", FullName = "A", Role = UserRole.Citizen, PasswordHash = "hash"
    };

    private void GivenUser(User? u) =>
        _users.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(u);

    [Fact]
    public async Task Register_CreatesCitizen_WithHashedPassword()
    {
        _users.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _hasher.Setup(h => h.Hash("Password123")).Returns("HASHED");
        User? saved = null;
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => saved = u).Returns(Task.CompletedTask);

        var res = await _sut.RegisterCitizenAsync(
            new RegisterCitizenRequest("New@Mail.org ", "Password123", "New User", null, null));

        saved!.Role.Should().Be(UserRole.Citizen);
        saved.PasswordHash.Should().Be("HASHED").And.NotBe("Password123");
        saved.Email.Should().Be("new@mail.org");
        res.AccessToken.Should().Be("jwt");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Throws409()
    {
        _users.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _sut.RegisterCitizenAsync(new("a@b.org", "Password123", "X Y", null, null));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Login_WrongPassword_IncrementsFailedCount_AndAudits()
    {
        var u = ExistingUser();
        GivenUser(u);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), "hash")).Returns(false);

        var act = () => _sut.LoginAsync(new LoginRequest("a@b.org", "bad"));

        await act.Should().ThrowAsync<AuthenticationFailedException>();
        u.FailedLoginCount.Should().Be(1);
        _audit.Verify(a => a.RecordAsync("LoginFailed", It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_FifthFailure_LocksAccount()
    {
        var u = ExistingUser();
        u.FailedLoginCount = AuthService.MaxFailedAttempts - 1;
        GivenUser(u);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), "hash")).Returns(false);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _sut.LoginAsync(new("a@b.org", "bad")));

        u.IsLockedOut(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task Login_LockedAccount_RejectedEvenWithCorrectPassword()
    {
        var u = ExistingUser();
        u.LockoutEndUtc = DateTime.UtcNow.AddMinutes(10);
        GivenUser(u);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), "hash")).Returns(true);

        var act = () => _sut.LoginAsync(new("a@b.org", "right"));

        await act.Should().ThrowAsync<AuthenticationFailedException>().WithMessage("*locked*");
        _tokens.Verify(t => t.CreateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Login_Success_ResetsCounterAndReturnsToken()
    {
        var u = ExistingUser();
        u.FailedLoginCount = 3;
        GivenUser(u);
        _hasher.Setup(h => h.Verify("right", "hash")).Returns(true);

        var res = await _sut.LoginAsync(new("a@b.org", "right"));

        res.AccessToken.Should().Be("jwt");
        u.FailedLoginCount.Should().Be(0);
    }
}

