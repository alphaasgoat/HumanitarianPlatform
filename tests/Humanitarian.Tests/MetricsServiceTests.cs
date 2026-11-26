using FluentAssertions;
using Humanitarian.Application.Common;
using Humanitarian.Application.Metrics;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Humanitarian.Tests.Support;
using Xunit;

namespace Humanitarian.Tests;

public class MetricsServiceTests
{
    private readonly InMemoryUnitOfWork _uow = new();
    private readonly FakeCurrentUser _user = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));
    private readonly MetricsService _sut;
    private readonly CrisisEvent _crisis = new() { Name = "Kelani Flood" };
    private readonly CrisisEvent _otherCrisis = new() { Name = "Landslide" };
    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public MetricsServiceTests()
    {
        _sut = new MetricsService(_uow, _user, _time);
        _user.Become(Guid.NewGuid(), "coordinator@demo.org", Roles.EmergencyCoordinator);
        Seed();
    }

    private AssistanceRequest AddRequest(
        UrgencyLevel urgency, RequestStatus status, double hoursAgo, Guid? crisis = null, double? lat = null, double? lng = null,
        double? closedHoursAgo = null)
    {
        var r = new AssistanceRequest
        {
            CrisisEventId = crisis ?? _crisis.Id, RequesterId = Guid.NewGuid(), Category = RequestCategory.Food, Urgency = urgency,
            DeclaredUrgency = urgency, Status = status, CreatedAtUtc = Now.AddHours(-hoursAgo), Latitude = lat, Longitude = lng,
            ClosedAtUtc = closedHoursAgo is null ? null : Now.AddHours(-closedHoursAgo.Value)
        };
        _uow.Repo<AssistanceRequest>().Items.Add(r);
        return r;
    }

    private void AddRecommendation(
        AssistanceRequest r, ReviewAction? action, string? by, double minutesAfterCreation, double confidence = 0.8, bool fallback = false)
    {
        _uow.Repo<AiRecommendation>().Items.Add(new AiRecommendation
        {
            RequestId = r.Id, Confidence = confidence, UsedFallback = fallback, HumanAction = action, DecidedByEmail = by,
            DecidedAtUtc = action is null ? null : r.CreatedAtUtc.AddMinutes(minutesAfterCreation)
        });
    }

    private void Seed()
    {
        // r1: critical, waiting 3 hours, nobody helping -> unresolved AND overdue (critical SLA is 2 hours)
        var r1 = AddRequest(UrgencyLevel.Critical, RequestStatus.UnderReview, 3, lat: 6.95, lng: 79.92);
        // r2: high, triaged, stock already reserved -> open but not unresolved
        var r2 = AddRequest(UrgencyLevel.High, RequestStatus.Triaged, 1);
        _uow.Repo<ResourceAllocation>().Items.Add(new ResourceAllocation { RequestId = r2.Id, Status = AllocationStatus.Approved });
        // r3: medium, 2 hours, nobody helping -> unresolved but inside its 24 hour SLA; far from every shelter
        var r3 = AddRequest(UrgencyLevel.Medium, RequestStatus.Submitted, 2, lat: 8.5, lng: 80.5);
        // r4: closed 4 hours after it was created
        var r4 = AddRequest(UrgencyLevel.Low, RequestStatus.Closed, 5, closedHoursAgo: 1);
        // r5: escalated, within its 6 hour SLA, near a shelter
        AddRequest(UrgencyLevel.High, RequestStatus.Escalated, 1, lat: 6.96, lng: 79.93);
        // r6: another crisis
        AddRequest(UrgencyLevel.High, RequestStatus.UnderReview, 1, crisis: _otherCrisis.Id);

        AddRecommendation(r1, ReviewAction.Approve, "coordinator@demo.org", 60);
        AddRecommendation(r2, ReviewAction.Modify, "casemanager@demo.org", 30);
        AddRecommendation(r4, ReviewAction.Reject, "casemanager@demo.org", 60);
        AddRecommendation(r3, null, null, 0, confidence: 0.4, fallback: true);

        _uow.Repo<ResourceItem>().Items.AddRange([
            new ResourceItem { Name = "Rice", Category = ResourceCategory.Food, QuantityTotal = 100, QuantityAvailable = 20, ReorderLevel = 30 },
            new ResourceItem { Name = "Dhal", Category = ResourceCategory.Food, QuantityTotal = 50, QuantityAvailable = 50, ReorderLevel = 10 },
            new ResourceItem { Name = "Old stock", Category = ResourceCategory.Water, QuantityTotal = 10, QuantityAvailable = 10, IsActive = false }
        ]);

        _uow.Repo<Shelter>().Items.AddRange([
            new Shelter { Name = "Hall", Capacity = 100, Occupancy = 80, Status = ShelterStatus.Open, Latitude = 6.95, Longitude = 79.92 },
            new Shelter { Name = "School", Capacity = 20, Occupancy = 20, Status = ShelterStatus.Open },
            new Shelter { Name = "Closed", Capacity = 50, Occupancy = 0, Status = ShelterStatus.Closed }
        ]);

        _uow.Repo<Volunteer>().Items.AddRange([
            new Volunteer { FullName = "A", IsVerified = true }, new Volunteer { FullName = "B", IsVerified = false }
        ]);
        var open = new VolunteerTask { CrisisEventId = _crisis.Id, Status = VolunteerTaskStatus.Open };
        var filled = new VolunteerTask { CrisisEventId = _crisis.Id, Status = VolunteerTaskStatus.Filled };
        var done = new VolunteerTask { CrisisEventId = _crisis.Id, Status = VolunteerTaskStatus.Completed };
        _uow.Repo<VolunteerTask>().Items.AddRange([open, filled, done]);
        _uow.Repo<TaskAssignment>().Items.AddRange([
            new TaskAssignment { TaskId = filled.Id, Status = AssignmentStatus.Accepted },
            new TaskAssignment { TaskId = done.Id, Status = AssignmentStatus.Completed, HoursWorked = 3.5 },
            new TaskAssignment { TaskId = open.Id, Status = AssignmentStatus.Declined }
        ]);

        _uow.Repo<NotificationDelivery>().Items.AddRange([
            new NotificationDelivery { Status = DeliveryStatus.Pending }, new NotificationDelivery { Status = DeliveryStatus.Sent },
            new NotificationDelivery { Status = DeliveryStatus.Sent }, new NotificationDelivery { Status = DeliveryStatus.Failed }
        ]);
    }

    [Fact]
    public async Task Requests_CountOpenUnresolvedOverdueAndEscalated()
    {
        var m = (await _sut.GetOperationalAsync(_crisis.Id)).Requests;

        m.Total.Should().Be(5);
        m.Open.Should().Be(4);
        m.Unresolved.Should().Be(3);   // r1, r3, r5: open and nobody is helping yet
        m.Overdue.Should().Be(1);      // only r1 is past its SLA
        m.Escalated.Should().Be(1);
        m.Closed.Should().Be(1);
        m.ByStatus["Closed"].Should().Be(1);
        m.OpenByUrgency["Critical"].Should().Be(1);
        m.OpenByUrgency["High"].Should().Be(2);
    }

    [Fact]
    public async Task ARequestBecomesOverdue_OnceItsSlaHasPassed()
    {
        (await _sut.GetOperationalAsync(_crisis.Id)).Requests.Overdue.Should().Be(1);

        _time.Advance(TimeSpan.FromHours(5.5));   // r5 (high, 6 hour SLA) is now 6.5 hours old; r3 is still inside its 24 hours

        (await _sut.GetOperationalAsync(_crisis.Id)).Requests.Overdue.Should().Be(2);
    }

    [Fact]
    public async Task ResponseTimes_AreTheAverageTimeToTheFirstHumanDecision_AndToResolution()
    {
        var t = (await _sut.GetOperationalAsync(_crisis.Id)).ResponseTimes;

        t.RequestsWithDecision.Should().Be(3);
        t.AverageFirstDecisionMinutes.Should().Be(50);   // (60 + 30 + 60) / 3
        t.AverageResolutionHours.Should().Be(4);         // r4: created 5 hours ago, closed 1 hour ago
    }

    [Fact]
    public async Task AiOverrideFrequency_CountsModifiedAndRejectedDecisions()
    {
        var ai = (await _sut.GetOperationalAsync(_crisis.Id)).Ai;

        ai.Recommendations.Should().Be(4);
        ai.Decided.Should().Be(3);
        (ai.Approved, ai.Modified, ai.Rejected, ai.Deferred).Should().Be((1, 1, 1, 0));
        ai.OverrideRatePercent.Should().Be(66.7);
        ai.FallbackRatePercent.Should().Be(25);
        ai.AverageConfidence.Should().Be(0.7);
    }

    [Fact]
    public async Task StaffWorkload_ShowsDecisionsAndOverridesPerPerson()
    {
        var w = (await _sut.GetOperationalAsync(_crisis.Id)).Workload;

        w.First().Should().Be(new StaffWorkloadDto("casemanager@demo.org", 2, 2));
        w.Should().Contain(new StaffWorkloadDto("coordinator@demo.org", 1, 0));
    }

    [Fact]
    public async Task ResourceUtilisation_IsPerCategory_AndIgnoresInactiveItems()
    {
        var resources = (await _sut.GetOperationalAsync(_crisis.Id)).Resources;

        var food = resources.Should().ContainSingle().Which;
        food.Category.Should().Be("Food");
        (food.Items, food.QuantityTotal, food.QuantityAvailable).Should().Be((2, 150, 70));
        food.UtilisationPercent.Should().Be(53.3);
        food.LowStockItems.Should().Be(1);
    }

    [Fact]
    public async Task ShelterOccupancy_CountsOnlyOpenShelters()
    {
        var s = (await _sut.GetOperationalAsync(_crisis.Id)).Shelters;

        (s.Shelters, s.Open, s.Capacity, s.Occupancy, s.Full).Should().Be((3, 2, 120, 100, 1));
        s.OccupancyPercent.Should().Be(83.3);
    }

    [Fact]
    public async Task Volunteers_ShowAcceptanceRateAndHoursGiven()
    {
        var v = (await _sut.GetOperationalAsync(_crisis.Id)).Volunteers;

        (v.Total, v.Verified, v.OpenTasks, v.FilledTasks, v.CompletedTasks).Should().Be((2, 1, 1, 1, 1));
        v.AcceptanceRatePercent.Should().Be(66.7);   // 2 of the 3 who answered said yes
        v.HoursGiven.Should().Be(3.5);
    }

    [Fact]
    public async Task Coverage_IsTheShareOfOpenLocatedRequestsNearAnOpenShelter()
    {
        var c = (await _sut.GetOperationalAsync(_crisis.Id)).Coverage;

        c.OpenRequestsWithLocation.Should().Be(3);
        c.WithinRadius.Should().Be(2);   // r1 and r5 are close to the hall; r3 is far away
        c.CoveragePercent.Should().Be(66.7);
        c.RadiusKm.Should().Be(10);
    }

    [Fact]
    public async Task Deliveries_ShowTheHealthOfTheMessageQueue()
    {
        var d = (await _sut.GetOperationalAsync(_crisis.Id)).Deliveries;

        (d.Pending, d.Sent, d.Failed).Should().Be((1, 2, 1));
    }

    [Fact]
    public async Task WithoutACrisisFilter_EveryRequestIsCounted()
    {
        (await _sut.GetOperationalAsync(null)).Requests.Total.Should().Be(6);
    }

    [Fact]
    public async Task ACaseManagerMaySee_ButACitizenMayNot()
    {
        _user.Become(Guid.NewGuid(), "casemanager@demo.org", Roles.CaseManager);
        (await _sut.GetOperationalAsync(null)).Should().NotBeNull();

        _user.Become(Guid.NewGuid(), "citizen@demo.org", Roles.Citizen);
        Func<Task> act = () => _sut.GetOperationalAsync(null);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public void SlaPolicy_GivesMoreTimeToLessUrgentRequests()
    {
        SlaPolicy.For(UrgencyLevel.Critical).Should().BeLessThan(SlaPolicy.For(UrgencyLevel.High));
        SlaPolicy.For(UrgencyLevel.High).Should().BeLessThan(SlaPolicy.For(UrgencyLevel.Medium));
        SlaPolicy.For(UrgencyLevel.Medium).Should().BeLessThan(SlaPolicy.For(UrgencyLevel.Low));
    }
}
