using FluentAssertions;
using Humanitarian.Application.Resources;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Xunit;

namespace Humanitarian.Tests;

public class ResourceMatcherTests
{
    private readonly ResourceMatcher _sut = new();

    private static AssistanceRequest FoodRequest(UrgencyLevel urgency) => new()
    {
        Category = RequestCategory.Food, PeopleAffected = 4, Urgency = urgency, Latitude = 6.95, Longitude = 79.92
    };

    // Needs 12 units (4 people x 3 days). "Near" is on the spot but small; "Far" is ~50 km away with plenty.
    private static ResourceItem Near() => new()
    {
        Name = "Near depot", Category = ResourceCategory.Food, QuantityTotal = 12, QuantityAvailable = 12,
        ReorderLevel = 10, Latitude = 6.95, Longitude = 79.92
    };

    private static ResourceItem Far() => new()
    {
        Name = "Far warehouse", Category = ResourceCategory.Food, QuantityTotal = 500, QuantityAvailable = 500,
        ReorderLevel = 50, Latitude = 7.40, Longitude = 79.92
    };

    [Fact]
    public void StandardRequest_DoesNotJustPickTheNearest_StockHealthMatters()
    {
        var near = Near();
        var far = Far();

        var ranked = _sut.Rank(FoodRequest(UrgencyLevel.Medium), 12, new[] { near, far });

        ranked[0].Resource.Name.Should().Be("Far warehouse");   // draining the near depot to zero is penalised
        ranked[0].DistanceKm.Should().BeInRange(45, 55);
    }

    [Fact]
    public void CriticalRequest_PrefersProximity()
    {
        var ranked = _sut.Rank(FoodRequest(UrgencyLevel.Critical), 12, new[] { Far(), Near() });

        ranked[0].Resource.Name.Should().Be("Near depot");
    }

    [Fact]
    public void HardConstraints_ExcludeWrongCategoryInactiveAndEmptyStock()
    {
        var wrongCategory = Near(); wrongCategory.Category = ResourceCategory.Medicine;
        var inactive = Near(); inactive.IsActive = false;
        var empty = Near(); empty.QuantityAvailable = 0;

        var ranked = _sut.Rank(FoodRequest(UrgencyLevel.High), 12, new[] { wrongCategory, inactive, empty });

        ranked.Should().BeEmpty();
    }

    [Fact]
    public void ShelterRequests_AreNotMatchedToResources()
    {
        var request = new AssistanceRequest { Category = RequestCategory.Shelter, PeopleAffected = 3, Urgency = UrgencyLevel.High };

        _sut.Rank(request, 1, new[] { Near() }).Should().BeEmpty();
    }

    [Fact]
    public void CanFullyCover_IsFalseWhenStockIsShort()
    {
        var small = Near(); small.QuantityAvailable = 5; small.QuantityTotal = 5;

        var ranked = _sut.Rank(FoodRequest(UrgencyLevel.Medium), 12, new[] { small });

        ranked.Should().HaveCount(1);
        ranked[0].CanFullyCover.Should().BeFalse();
        ranked[0].Rationale.Should().Contain("Covers 42%");
    }

    [Fact]
    public void MissingLocation_UsesNeutralProximity_AndStillRanks()
    {
        var noLocation = Near(); noLocation.Latitude = null; noLocation.Longitude = null;

        var ranked = _sut.Rank(FoodRequest(UrgencyLevel.Medium), 12, new[] { noLocation });

        ranked.Should().HaveCount(1);
        ranked[0].DistanceKm.Should().BeNull();
        ranked[0].Rationale.Should().Contain("distance unknown");
    }
}
