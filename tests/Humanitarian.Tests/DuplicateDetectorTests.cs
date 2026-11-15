using FluentAssertions;
using Humanitarian.Application.Requests.Duplicates;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Xunit;

namespace Humanitarian.Tests;

public class DuplicateDetectorTests
{
    private readonly DuplicateDetector _sut = new();
    private static readonly Guid UserX = Guid.NewGuid();
    private static readonly Guid UserY = Guid.NewGuid();

    private static AssistanceRequest Req(string text, RequestCategory cat, Guid user, double? lat = null, double? lon = null) => new()
    {
        Description = text, Category = cat, RequesterId = user, Latitude = lat, Longitude = lon
    };

    [Fact]
    public void SameRequesterRepeatingNearlyTheSameRequestAtTheSamePlace_IsFlagged()
    {
        var first = Req("Need drinking water and food for family of five, Kelaniya near the temple", RequestCategory.Food, UserX, 6.95, 79.92);
        var second = Req("Need drinking water and food for family of 5 near Kelaniya temple", RequestCategory.Food, UserX, 6.95, 79.92);

        var matches = _sut.Find(second, new[] { first });

        matches.Should().HaveCount(1);
        matches[0].DuplicateOfRequestId.Should().Be(first.Id);
        matches[0].Score.Should().BeGreaterOrEqualTo(DuplicateDetector.Threshold);
        matches[0].Reason.Should().Contain("same requester");
    }

    [Fact]
    public void UnrelatedRequestFarAway_IsNotFlagged()
    {
        var food = Req("Need drinking water and food for family of five, Kelaniya near the temple", RequestCategory.Food, UserX, 6.95, 79.92);
        var medicine = Req("Insulin needed for diabetic grandfather urgently", RequestCategory.Medicine, UserY, 7.29, 80.63);

        _sut.Find(medicine, new[] { food }).Should().BeEmpty();
    }

    [Fact]
    public void WorksWithoutCoordinates_WhenTextAndCategoryMatch()
    {
        var a = Req("Need drinking water and food for family of five, Kelaniya near the temple", RequestCategory.Food, UserX);
        var b = Req("Need drinking water and food for family of 5 near Kelaniya temple", RequestCategory.Food, UserY);

        _sut.Find(b, new[] { a }).Should().HaveCount(1);
    }

    [Fact]
    public void RequestIsNeverMatchedWithItself()
    {
        var a = Req("Need drinking water and food for family of five", RequestCategory.Food, UserX, 6.95, 79.92);

        _sut.Find(a, new[] { a }).Should().BeEmpty();
    }

    [Fact]
    public void Haversine_KnownDistance_IsReasonable()
    {
        // Colombo to Kandy is roughly 94 km in a straight line.
        var km = GeoMath.DistanceKm(6.9271, 79.8612, 7.2906, 80.6337);

        km.Should().BeInRange(85, 105);
    }
}
