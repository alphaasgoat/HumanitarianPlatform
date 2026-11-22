using FluentAssertions;
using Humanitarian.Application.Shelters;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Xunit;

namespace Humanitarian.Tests;

public class ShelterMatcherTests
{
    private readonly ShelterMatcher _sut = new();

    private static AssistanceRequest Household(string description = "Our home is flooded and we need a safe place.", int people = 3) => new()
    {
        Category = RequestCategory.Shelter, Description = description, PeopleAffected = people,
        Latitude = 6.95, Longitude = 79.92
    };

    private static Shelter Hall(string name, int capacity = 100, int occupancy = 0, double lat = 6.95, double lon = 79.92) => new()
    {
        Name = name, Capacity = capacity, Occupancy = occupancy, Latitude = lat, Longitude = lon,
        ProvidesFood = true, ProvidesWater = true, Status = ShelterStatus.Open
    };

    [Fact]
    public void HardConstraints_ExcludeClosedFullAndTooSmallShelters()
    {
        var closed = Hall("Closed"); closed.Status = ShelterStatus.Closed;
        var full = Hall("Full", capacity: 20, occupancy: 20);
        var tooSmall = Hall("Too small", capacity: 20, occupancy: 18);   // 2 spaces, household of 3

        var ranked = _sut.Rank(Household(), 3, new[] { closed, full, tooSmall });

        ranked.Should().BeEmpty();
    }

    [Fact]
    public void WheelchairUser_IsNeverRecommendedAnInaccessibleShelter()
    {
        var inaccessible = Hall("No ramp"); inaccessible.WheelchairAccessible = false;
        var accessible = Hall("Ramp"); accessible.WheelchairAccessible = true;

        var ranked = _sut.Rank(Household("My father uses a wheelchair."), 3, new[] { inaccessible, accessible });

        ranked.Should().ContainSingle().Which.Shelter.Name.Should().Be("Ramp");
    }

    [Fact]
    public void MedicalNeed_PrefersShelterWithMedicalSupport_AndWarnsAboutTheOther()
    {
        var withMedical = Hall("Clinic hall"); withMedical.HasMedicalSupport = true;
        var without = Hall("Plain hall");

        var ranked = _sut.Rank(Household("My wife is pregnant and needs a doctor."), 3, new[] { without, withMedical });

        ranked[0].Shelter.Name.Should().Be("Clinic hall");
        ranked[1].Warnings.Should().Contain(w => w.Contains("medical"));
    }

    [Fact]
    public void NearestShelter_IsNotAlwaysTheBest()
    {
        // "Near" is on the spot but would be completely full after placing 3 people.
        var near = Hall("Near hall", capacity: 10, occupancy: 7, lat: 6.95, lon: 79.92);
        var roomy = Hall("Roomy hall", capacity: 200, occupancy: 100, lat: 7.13, lon: 79.92);   // about 20 km away

        var ranked = _sut.Rank(Household(), 3, new[] { near, roomy });

        ranked[0].Shelter.Name.Should().Be("Roomy hall");
        ranked[0].DistanceKm.Should().BeInRange(18, 22);
    }

    [Fact]
    public void MissingLocation_StillRanks_WithNeutralDistance()
    {
        var noLocation = Hall("No GPS"); noLocation.Latitude = null; noLocation.Longitude = null;

        var ranked = _sut.Rank(Household(), 3, new[] { noLocation });

        ranked.Should().ContainSingle();
        ranked[0].DistanceKm.Should().BeNull();
        ranked[0].Rationale.Should().Contain("distance unknown");
    }

    [Fact]
    public void ShelterNeeds_AreReadFromTheRequestText()
    {
        ShelterNeeds.From(Household("She is bedridden and needs insulin.")).Should().Be(new ShelterNeeds(true, true));
        ShelterNeeds.From(Household("We need a dry place to sleep.")).Should().Be(new ShelterNeeds(false, false));

        var accessibility = Household("Please help.");
        accessibility.Category = RequestCategory.Accessibility;
        ShelterNeeds.From(accessibility).Wheelchair.Should().BeTrue();
    }
}
