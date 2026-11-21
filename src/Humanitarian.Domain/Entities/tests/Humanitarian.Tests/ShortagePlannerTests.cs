using FluentAssertions;
using Humanitarian.Application.Resources;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Xunit;

namespace Humanitarian.Tests;

public class ShortagePlannerTests
{
    private readonly ShortagePlanner _sut = new(new ResourceMatcher());
    private static readonly IReadOnlyDictionary<Guid, int> NothingAllocated = new Dictionary<Guid, int>();

    // 3 people x 3 days = 9 units needed
    private static AssistanceRequest Req(UrgencyLevel urgency, int priority, int ageMinutes = 0) => new()
    {
        Category = RequestCategory.Food, PeopleAffected = 3, Urgency = urgency, PriorityScore = priority,
        Status = RequestStatus.Triaged, CreatedAtUtc = DateTime.UtcNow.AddMinutes(-ageMinutes)
    };

    private static ResourceItem Stock(int available, string name = "Rice") => new()
    {
        Name = name, Category = ResourceCategory.Food, QuantityTotal = available, QuantityAvailable = available
    };

    [Fact]
    public void EnoughStock_EveryoneIsServedInFull()
    {
        var plan = _sut.Plan(new[] { Req(UrgencyLevel.High, 70), Req(UrgencyLevel.Medium, 50) }, new[] { Stock(100) }, NothingAllocated);

        plan.HasShortage.Should().BeFalse();
        plan.Lines.Should().OnlyContain(l => l.Outcome == "Full");
    }

    [Fact]
    public void Shortage_ServesMostUrgentFirst_AndTheLastGetsTheRemainder()
    {
        var critical = Req(UrgencyLevel.Critical, 90);
        var high = Req(UrgencyLevel.High, 70);
        var medium = Req(UrgencyLevel.Medium, 50);

        // passed in the "wrong" order on purpose
        var plan = _sut.Plan(new[] { medium, high, critical }, new[] { Stock(20) }, NothingAllocated);

        plan.TotalNeeded.Should().Be(27);
        plan.TotalAvailable.Should().Be(20);
        plan.Shortfall.Should().Be(7);
        plan.HasShortage.Should().BeTrue();

        plan.Lines.Select(l => l.Request.Id).Should().ContainInOrder(critical.Id, high.Id, medium.Id);
        plan.Lines.Select(l => l.Outcome).Should().ContainInOrder("Full", "Full", "Partial");
        plan.Lines.Select(l => l.Proposed).Should().ContainInOrder(9, 9, 2);
        plan.Lines.Sum(l => l.Proposed).Should().BeLessOrEqualTo(20);   // never over-allocates
    }

    [Fact]
    public void SameUrgency_HigherPriorityScoreWins_ThenOlderRequest()
    {
        var low = Req(UrgencyLevel.High, 60);
        var high = Req(UrgencyLevel.High, 85);
        var oldA = Req(UrgencyLevel.High, 70, ageMinutes: 30);
        var newB = Req(UrgencyLevel.High, 70, ageMinutes: 5);

        var plan = _sut.Plan(new[] { low, newB, oldA, high }, new[] { Stock(1000) }, NothingAllocated);

        plan.Lines.Select(l => l.Request.Id).Should().ContainInOrder(high.Id, oldA.Id, newB.Id, low.Id);
    }

    [Fact]
    public void NoStock_EveryRequestIsUnfulfilled()
    {
        var plan = _sut.Plan(new[] { Req(UrgencyLevel.Critical, 90) }, new[] { Stock(0) }, NothingAllocated);

        plan.Lines.Should().ContainSingle().Which.Outcome.Should().Be("Unfulfilled");
        plan.Shortfall.Should().Be(9);
    }

    [Fact]
    public void RequestsThatAreAlreadyFullyServed_AreSkipped()
    {
        var served = Req(UrgencyLevel.High, 70);
        var waiting = Req(UrgencyLevel.Medium, 50);
        var already = new Dictionary<Guid, int> { [served.Id] = 9 };

        var plan = _sut.Plan(new[] { served, waiting }, new[] { Stock(100) }, already);

        plan.Lines.Should().ContainSingle().Which.Request.Id.Should().Be(waiting.Id);
    }

    [Fact]
    public void PartiallyServedRequest_OnlyNeedsTheRemainder()
    {
        var request = Req(UrgencyLevel.High, 70);
        var already = new Dictionary<Guid, int> { [request.Id] = 4 };

        var plan = _sut.Plan(new[] { request }, new[] { Stock(100) }, already);

        plan.Lines.Single().Needed.Should().Be(5);
    }

    [Fact]
    public void NeedCanBeSplitAcrossSeveralResources()
    {
        var a = Stock(5, "Depot A");
        var b = Stock(5, "Depot B");

        var plan = _sut.Plan(new[] { Req(UrgencyLevel.High, 70) }, new[] { a, b }, NothingAllocated);

        var line = plan.Lines.Single();
        line.Outcome.Should().Be("Full");
        line.Takes.Should().HaveCount(2);
        line.Takes.Sum(t => t.Quantity).Should().Be(9);
    }

    [Fact]
    public void Planning_NeverChangesTheRealStock()
    {
        var stock = Stock(20);

        _sut.Plan(new[] { Req(UrgencyLevel.Critical, 90) }, new[] { stock }, NothingAllocated);

        stock.QuantityAvailable.Should().Be(20);
    }
}
