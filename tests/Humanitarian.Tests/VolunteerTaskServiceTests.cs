using FluentAssertions;
using Humanitarian.Application.Common;
using Humanitarian.Application.Volunteers;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Humanitarian.Tests.Support;
using Xunit;

namespace Humanitarian.Tests;

public class VolunteerTaskServiceTests
{
    private readonly InMemoryUnitOfWork _uow = new();
    private readonly FakeCurrentUser _user = new();
    private readonly RecordingAuditService _audit = new();
    private readonly VolunteerTaskService _sut;
    private readonly CrisisEvent _crisis = new() { Name = "Kelani Flood", Country = "LK" };
    private static readonly DateTime Start = DateTime.UtcNow.Date.AddDays(3).AddHours(8);

    public VolunteerTaskServiceTests()
    {
        _uow.Repo<CrisisEvent>().Items.Add(_crisis);
        _sut = new VolunteerTaskService(_uow, _audit, _user, TimeProvider.System, new VolunteerMatcher());
        AsCoordinator();
    }

    // ---------- helpers ----------
    private void AsCoordinator() => _user.Become(Guid.NewGuid(), "volunteers@demo.org", Roles.VolunteerCoordinator);
    private void AsVolunteer(Volunteer v) => _user.Become(v.UserId, "volunteer@mail.org", Roles.Citizen);

    private Volunteer AddVolunteer(
        string name = "Nimal", string skills = "Driving", bool verified = true, bool active = true,
        bool withAvailability = true, double lat = 6.95, double lon = 79.92)
    {
        var v = new Volunteer
        {
            UserId = Guid.NewGuid(), FullName = name, Skills = skills, IsVerified = verified, IsActive = active,
            Latitude = lat, Longitude = lon
        };
        _uow.Repo<Volunteer>().Items.Add(v);
        if (withAvailability)
            _uow.Repo<VolunteerAvailability>().Items.Add(new VolunteerAvailability
            {
                VolunteerId = v.Id, StartUtc = Start.AddHours(-1), EndUtc = Start.AddHours(6)
            });
        return v;
    }

    private async Task<TaskDto> CreateTask(int needed = 1, VolunteerSkill skill = VolunteerSkill.Driving, int startOffsetHours = 0)
    {
        AsCoordinator();
        return await _sut.CreateAsync(new CreateTaskDto(
            _crisis.Id, null, "Distribute water", "Deliver bottles to the camp", skill, needed, 6.95, 79.92,
            Start.AddHours(startOffsetHours), Start.AddHours(startOffsetHours + 4)));
    }

    private async Task<AssignmentDto> Assign(TaskDto task, Volunteer v)
    {
        AsCoordinator();
        return await _sut.AssignAsync(task.Id, new AssignDto(v.Id, null));
    }

    // ---------- tasks ----------
    [Fact]
    public async Task Create_WithEndBeforeStart_IsRejected()
    {
        await Assert.ThrowsAsync<AppValidationException>(() => _sut.CreateAsync(new CreateTaskDto(
            _crisis.Id, null, "Bad shift", null, VolunteerSkill.General, 1, null, null, Start, Start.AddHours(-1))));
    }

    [Fact]
    public async Task Create_ForAClosedCrisis_IsRejected()
    {
        _crisis.Status = CrisisStatus.Closed;

        await Assert.ThrowsAsync<ConflictException>(() => CreateTask());
    }

    [Fact]
    public async Task Create_AsCitizen_IsForbidden()
    {
        _user.Become(Guid.NewGuid(), "citizen@demo.org", Roles.Citizen);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.CreateAsync(new CreateTaskDto(
            _crisis.Id, null, "Distribute water", null, VolunteerSkill.General, 1, null, null, Start, Start.AddHours(4))));
    }

    // ---------- recommendation ----------
    [Fact]
    public async Task Recommend_ReturnsOnlyQualifiedAvailableVolunteers_BestFirst()
    {
        AddVolunteer("Far", lat: 7.40, lon: 79.92);
        AddVolunteer("Near");
        AddVolunteer("Unverified", verified: false);
        AddVolunteer("Wrong skill", skills: "Cooking");
        AddVolunteer("No availability", withAvailability: false);
        var task = await CreateTask();

        var list = await _sut.RecommendAsync(task.Id);

        list.Select(m => m.FullName).Should().ContainInOrder("Near", "Far");
        list.Should().HaveCount(2);
    }

    // ---------- assignment ----------
    [Fact]
    public async Task Assign_FillsTheTask_AndAuditsTheHumanChoice()
    {
        var volunteer = AddVolunteer();
        var task = await CreateTask(needed: 1);

        var assignment = await Assign(task, volunteer);

        assignment.Status.Should().Be("Assigned");
        (await _sut.GetAsync(task.Id)).Status.Should().Be("Filled");
        _audit.Entries.Single(e => e.Action == "VolunteerAssigned").Details.Should().Contain("\"humanChoiceRank\":1");
    }

    [Fact]
    public async Task Assign_UnverifiedVolunteer_IsRejected()
    {
        var volunteer = AddVolunteer(verified: false);
        var task = await CreateTask();

        await Assert.ThrowsAsync<ConflictException>(() => Assign(task, volunteer));
    }

    [Fact]
    public async Task Assign_WithoutTheRequiredSkill_IsRejected()
    {
        var volunteer = AddVolunteer(skills: "Cooking");
        var task = await CreateTask(skill: VolunteerSkill.Driving);

        await Assert.ThrowsAsync<ConflictException>(() => Assign(task, volunteer));
    }

    [Fact]
    public async Task Assign_WhenNotAvailableForTheWholeShift_IsRejected()
    {
        var volunteer = AddVolunteer(withAvailability: false);
        var task = await CreateTask();

        await Assert.ThrowsAsync<ConflictException>(() => Assign(task, volunteer));
    }

    [Fact]
    public async Task Assign_DoubleBooking_IsRejected()
    {
        var volunteer = AddVolunteer();
        var morning = await CreateTask(startOffsetHours: 0);     // 08:00 - 12:00
        var overlapping = await CreateTask(startOffsetHours: 2); // 10:00 - 14:00
        await Assign(morning, volunteer);

        await Assert.ThrowsAsync<ConflictException>(() => Assign(overlapping, volunteer));
    }

    [Fact]
    public async Task Assign_MoreVolunteersThanNeeded_IsRejected()
    {
        var first = AddVolunteer("First");
        var second = AddVolunteer("Second");
        var task = await CreateTask(needed: 1);
        await Assign(task, first);

        await Assert.ThrowsAsync<ConflictException>(() => Assign(task, second));
    }

    [Fact]
    public async Task Assign_TheSameVolunteerTwice_IsRejected()
    {
        var volunteer = AddVolunteer();
        var task = await CreateTask(needed: 2);
        await Assign(task, volunteer);

        await Assert.ThrowsAsync<ConflictException>(() => Assign(task, volunteer));
    }

    // ---------- the volunteer answers ----------
    [Fact]
    public async Task Accept_ByTheAssignedVolunteer_Works_ButNotByAnotherVolunteer()
    {
        var volunteer = AddVolunteer("Owner");
        var other = AddVolunteer("Other");
        var task = await CreateTask();
        var assignment = await Assign(task, volunteer);

        AsVolunteer(other);
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AcceptAsync(assignment.Id));

        AsVolunteer(volunteer);
        var accepted = await _sut.AcceptAsync(assignment.Id);
        accepted.Status.Should().Be("Accepted");
    }

    [Fact]
    public async Task Decline_ReopensAFilledTask()
    {
        var volunteer = AddVolunteer();
        var task = await CreateTask(needed: 1);
        var assignment = await Assign(task, volunteer);
        (await _sut.GetAsync(task.Id)).Status.Should().Be("Filled");

        AsVolunteer(volunteer);
        await _sut.DeclineAsync(assignment.Id, "Family emergency");

        AsCoordinator();
        (await _sut.GetAsync(task.Id)).Status.Should().Be("Open");
    }

    [Fact]
    public async Task MyAssignments_ShowsOnlyTheVolunteersOwn_WithTheTaskTitle()
    {
        var mine = AddVolunteer("Mine");
        var theirs = AddVolunteer("Theirs");
        var task = await CreateTask(needed: 2);
        await Assign(task, mine);
        await Assign(task, theirs);

        AsVolunteer(mine);
        var list = await _sut.MyAssignmentsAsync();

        list.Should().ContainSingle();
        list[0].TaskTitle.Should().Be("Distribute water");
    }

    // ---------- finishing ----------
    [Fact]
    public async Task Complete_NeedsAnAcceptedAssignment_AndFinishesTheTask()
    {
        var volunteer = AddVolunteer();
        var task = await CreateTask(needed: 1);
        var assignment = await Assign(task, volunteer);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.CompleteAsync(assignment.Id, new CompleteAssignmentDto(3.5, null)));

        AsVolunteer(volunteer);
        await _sut.AcceptAsync(assignment.Id);

        AsCoordinator();
        var done = await _sut.CompleteAsync(assignment.Id, new CompleteAssignmentDto(3.5, "Delivered everything"));

        done.Status.Should().Be("Completed");
        done.HoursWorked.Should().Be(3.5);
        (await _sut.GetAsync(task.Id)).Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Complete_ByTheVolunteer_IsForbidden()
    {
        var volunteer = AddVolunteer();
        var task = await CreateTask();
        var assignment = await Assign(task, volunteer);
        AsVolunteer(volunteer);
        await _sut.AcceptAsync(assignment.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.CompleteAsync(assignment.Id, new CompleteAssignmentDto(2, null)));
    }

    [Fact]
    public async Task CancelAssignment_FreesTheSlot()
    {
        var volunteer = AddVolunteer();
        var task = await CreateTask(needed: 1);
        var assignment = await Assign(task, volunteer);

        AsCoordinator();
        await _sut.CancelAssignmentAsync(assignment.Id, "Volunteer fell ill");

        (await _sut.GetAsync(task.Id)).Status.Should().Be("Open");
    }

    [Fact]
    public async Task CancelTask_CancelsEveryActiveAssignment()
    {
        var volunteer = AddVolunteer();
        var task = await CreateTask();
        await Assign(task, volunteer);

        AsCoordinator();
        var cancelled = await _sut.CancelTaskAsync(task.Id, "No longer needed");

        cancelled.Status.Should().Be("Cancelled");
        _uow.Repo<TaskAssignment>().Items.Should().OnlyContain(a => a.Status == AssignmentStatus.Cancelled);
    }
}
