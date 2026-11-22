using FluentAssertions;
using Humanitarian.Application.Common;
using Humanitarian.Application.Volunteers;
using Humanitarian.Domain.Enums;
using Humanitarian.Tests.Support;
using Xunit;

namespace Humanitarian.Tests;

public class VolunteerServiceTests
{
    private readonly InMemoryUnitOfWork _uow = new();
    private readonly FakeCurrentUser _user = new();
    private readonly RecordingAuditService _audit = new();
    private readonly VolunteerService _sut;

    public VolunteerServiceTests()
    {
        _sut = new VolunteerService(_uow, _audit, _user, TimeProvider.System);
        AsCitizen(Guid.NewGuid());
    }

    private void AsCitizen(Guid id) => _user.Become(id, "citizen@demo.org", Roles.Citizen);
    private void AsVolunteerCoordinator() => _user.Become(Guid.NewGuid(), "volunteers@demo.org", Roles.VolunteerCoordinator);

    private static RegisterVolunteerDto Profile(string name = "Nimal Perera", params VolunteerSkill[] skills) =>
        new(name, "0771234567", (skills.Length == 0 ? new[] { VolunteerSkill.FirstAid, VolunteerSkill.Driving } : skills).ToList(), 6.95, 79.92);

    [Fact]
    public async Task Register_CreatesAnUnverifiedProfile_AndIsAudited()
    {
        var dto = await _sut.RegisterMeAsync(Profile());

        dto.IsVerified.Should().BeFalse();
        dto.IsActive.Should().BeTrue();
        dto.Skills.Should().BeEquivalentTo(new[] { "FirstAid", "Driving" });
        _audit.Entries.Should().Contain(e => e.Action == "VolunteerRegistered");
    }

    [Fact]
    public async Task Register_Twice_IsRejected()
    {
        await _sut.RegisterMeAsync(Profile());

        await Assert.ThrowsAsync<ConflictException>(() => _sut.RegisterMeAsync(Profile()));
    }

    [Fact]
    public async Task Register_AsStaff_IsForbidden()
    {
        AsVolunteerCoordinator();

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.RegisterMeAsync(Profile()));
    }

    [Fact]
    public async Task GetMe_BeforeRegistering_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetMeAsync());
    }

    [Fact]
    public async Task Verify_IsACoordinatorDecision_AndCanOnlyHappenOnce()
    {
        var volunteer = await _sut.RegisterMeAsync(Profile());

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.VerifyAsync(volunteer.Id));   // the citizen cannot verify themselves

        AsVolunteerCoordinator();
        var verified = await _sut.VerifyAsync(volunteer.Id);
        verified.IsVerified.Should().BeTrue();
        _audit.Entries.Should().Contain(e => e.Action == "VolunteerVerified");

        await Assert.ThrowsAsync<ConflictException>(() => _sut.VerifyAsync(volunteer.Id));
    }

    [Fact]
    public async Task UpdateMe_ChangingSkills_ResetsVerification()
    {
        var citizenId = Guid.NewGuid();
        AsCitizen(citizenId);
        var volunteer = await _sut.RegisterMeAsync(Profile());
        AsVolunteerCoordinator();
        await _sut.VerifyAsync(volunteer.Id);

        AsCitizen(citizenId);
        var updated = await _sut.UpdateMeAsync(Profile("Nimal Perera", VolunteerSkill.Medical));

        updated.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMe_ChangingOnlyThePhone_KeepsVerification()
    {
        var citizenId = Guid.NewGuid();
        AsCitizen(citizenId);
        var volunteer = await _sut.RegisterMeAsync(Profile());
        AsVolunteerCoordinator();
        await _sut.VerifyAsync(volunteer.Id);

        AsCitizen(citizenId);
        var updated = await _sut.UpdateMeAsync(new RegisterVolunteerDto(
            "Nimal Perera", "0719999999", new List<VolunteerSkill> { VolunteerSkill.FirstAid, VolunteerSkill.Driving }, 6.95, 79.92));

        updated.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Availability_MustBeValid_AndCannotOverlap()
    {
        await _sut.RegisterMeAsync(Profile());
        var start = DateTime.UtcNow.AddDays(2);

        await Assert.ThrowsAsync<AppValidationException>(() => _sut.AddAvailabilityAsync(new AvailabilityDto(start, start.AddHours(-1))));
        await Assert.ThrowsAsync<AppValidationException>(() =>
            _sut.AddAvailabilityAsync(new AvailabilityDto(DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(-2))));

        var slot = await _sut.AddAvailabilityAsync(new AvailabilityDto(start, start.AddHours(8)));
        slot.EndUtc.Should().BeAfter(slot.StartUtc);
        await Assert.ThrowsAsync<ConflictException>(() => _sut.AddAvailabilityAsync(new AvailabilityDto(start.AddHours(4), start.AddHours(10))));

        (await _sut.ListMyAvailabilityAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task List_CanFilterBySkillAndVerification()
    {
        AsCitizen(Guid.NewGuid());
        var first = await _sut.RegisterMeAsync(Profile("First Aider", VolunteerSkill.FirstAid));
        AsCitizen(Guid.NewGuid());
        await _sut.RegisterMeAsync(Profile("Driver", VolunteerSkill.Driving));

        AsVolunteerCoordinator();
        await _sut.VerifyAsync(first.Id);

        (await _sut.ListAsync(null, null)).Should().HaveCount(2);
        (await _sut.ListAsync(null, true)).Should().ContainSingle().Which.FullName.Should().Be("First Aider");
        (await _sut.ListAsync(VolunteerSkill.Driving, null)).Should().ContainSingle().Which.FullName.Should().Be("Driver");
    }
}
