using FluentAssertions;
using Humanitarian.Application.Volunteers;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Xunit;

namespace Humanitarian.Tests;

public class VolunteerMatcherTests
{
    private readonly VolunteerMatcher _sut = new();
    private static readonly DateTime Start = new(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc);

    private static VolunteerTask MakeTask(VolunteerSkill skill = VolunteerSkill.Driving) => new()
    {
        RequiredSkill = skill, StartUtc = Start, EndUtc = Start.AddHours(4), Latitude = 6.95, Longitude = 79.92
    };

    private static VolunteerCandidate Cand(
        string name, string skills = "Driving", bool verified = true, bool active = true, bool wholeShift = true,
        double lat = 6.95, double lon = 79.92, int otherTasks = 0, bool clash = false)
    {
        var v = new Volunteer { FullName = name, Skills = skills, IsVerified = verified, IsActive = active, Latitude = lat, Longitude = lon };
        var slot = new VolunteerAvailability
        {
            VolunteerId = v.Id, StartUtc = Start.AddHours(-1), EndUtc = wholeShift ? Start.AddHours(5) : Start.AddHours(2)
        };
        var busy = new List<BusyWindow>();
        for (var i = 0; i < otherTasks; i++) busy.Add(new BusyWindow(Start.AddDays(i + 1), Start.AddDays(i + 1).AddHours(4)));
        if (clash) busy.Add(new BusyWindow(Start.AddHours(2), Start.AddHours(6)));
        return new VolunteerCandidate(v, new[] { slot }, busy);
    }

    [Fact]
    public void HardConstraints_ExcludeUnqualifiedVolunteers()
    {
        var candidates = new[]
        {
            Cand("Unverified", verified: false),
            Cand("Inactive", active: false),
            Cand("Wrong skill", skills: "Cooking"),
            Cand("Only part of the shift", wholeShift: false),
            Cand("Clashing task", clash: true)
        };

        _sut.Rank(MakeTask(), candidates).Should().BeEmpty();
    }

    [Fact]
    public void QualifiedVolunteer_IsRanked_WithAnExplanation()
    {
        var ranked = _sut.Rank(MakeTask(), new[] { Cand("Nimal", skills: "Driving,FirstAid") });

        ranked.Should().ContainSingle();
        ranked[0].Score.Should().BeInRange(0, 100);
        ranked[0].Rationale.Should().Contain("free for the whole shift");
    }

    [Fact]
    public void WorkloadFairness_CanOutweighASmallDistanceAdvantage()
    {
        var busyButNear = Cand("Busy but near", otherTasks: 4);
        var freshAndFarther = Cand("Fresh", lat: 7.40, lon: 79.92);   // about 50 km away

        var ranked = _sut.Rank(MakeTask(), new[] { busyButNear, freshAndFarther });

        ranked[0].Volunteer.FullName.Should().Be("Fresh");
    }

    [Fact]
    public void GeneralTask_AcceptsAnyVerifiedVolunteer()
    {
        var ranked = _sut.Rank(MakeTask(VolunteerSkill.General), new[] { Cand("No listed skills", skills: "") });

        ranked.Should().ContainSingle();
    }

    [Fact]
    public void Rationale_MentionsTheNumberOfOtherActiveTasks()
    {
        var ranked = _sut.Rank(MakeTask(), new[] { Cand("Nimal", otherTasks: 2) });

        ranked[0].Rationale.Should().Contain("2 other active task(s)");
    }

    [Fact]
    public void SkillSet_IgnoresUnknownValues_AndFormatSortsAndRemovesDuplicates()
    {
        SkillSet.Parse("FirstAid, Driving ,Unknown").Should().ContainInOrder(VolunteerSkill.FirstAid, VolunteerSkill.Driving);
        SkillSet.Format(new[] { VolunteerSkill.Driving, VolunteerSkill.FirstAid, VolunteerSkill.Driving })
            .Should().Be("FirstAid,Driving");
    }
}
