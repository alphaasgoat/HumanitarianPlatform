using FluentAssertions;
using Humanitarian.Application.Common;
using Humanitarian.Application.Mapping;
using Humanitarian.Domain.Entities;
using Humanitarian.Tests.Support;
using Xunit;

namespace Humanitarian.Tests;

public class MappingServiceTests
{
    private readonly InMemoryUnitOfWork _uow = new();
    private readonly FakeProvider _provider = new();
    private readonly MappingService _sut;

    public MappingServiceTests()
    {
        var options = new MappingOptions { MaxAttempts = 2, Timeout = TimeSpan.FromSeconds(1), RetryDelay = TimeSpan.Zero };
        _sut = new MappingService(_uow, _provider, _provider, TimeProvider.System, options);
    }

    private sealed class FakeProvider : IGeocodingProvider, IRoutingProvider
    {
        public string Name => "fake";
        public int GeocodeCalls { get; private set; }
        public int RouteCalls { get; private set; }
        public int FailFirst { get; set; }
        public bool AlwaysFail { get; set; }
        public GeoPoint? Point { get; set; } = new(6.95, 79.92, "Kelaniya, Sri Lanka");
        public RouteInfo? Route { get; set; } = new(12.4, 25);

        public Task<GeoPoint?> GeocodeAsync(string address, CancellationToken ct)
        {
            GeocodeCalls++;
            if (AlwaysFail || GeocodeCalls <= FailFirst) throw new HttpRequestException("provider down");
            return Task.FromResult(Point);
        }

        public Task<GeoPoint?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct)
        {
            if (AlwaysFail) throw new HttpRequestException("provider down");
            return Task.FromResult<GeoPoint?>(new GeoPoint(latitude, longitude, "Somewhere"));
        }

        public Task<RouteInfo?> GetRouteAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct)
        {
            RouteCalls++;
            if (AlwaysFail) throw new HttpRequestException("provider down");
            return Task.FromResult(Route);
        }
    }

    // ---------- geocoding ----------
    [Fact]
    public async Task Geocode_ReturnsTheCoordinates_AndSavesThemInTheCache()
    {
        var result = await _sut.GeocodeAsync("12 Lake Road, Kelaniya");

        result.Found.Should().BeTrue();
        result.Latitude.Should().Be(6.95);
        result.Source.Should().Be("fake");
        _uow.Repo<GeocodeCacheEntry>().Items.Should().ContainSingle();
    }

    [Fact]
    public async Task ASecondLookupOfTheSameAddress_UsesTheCache_NotTheProvider()
    {
        await _sut.GeocodeAsync("12 Lake Road, Kelaniya");
        var again = await _sut.GeocodeAsync("  12 LAKE   road, kelaniya ");   // same address, different spacing and case

        again.Source.Should().Be("cache");
        again.Found.Should().BeTrue();
        _provider.GeocodeCalls.Should().Be(1);
    }

    [Fact]
    public async Task ATemporaryProviderFailure_IsRetried()
    {
        _provider.FailFirst = 1;

        var result = await _sut.GeocodeAsync("12 Lake Road, Kelaniya");

        result.Found.Should().BeTrue();
        _provider.GeocodeCalls.Should().Be(2);
    }

    [Fact]
    public async Task WhenTheProviderIsDown_TheAnswerSaysSo_InsteadOfFailing()
    {
        _provider.AlwaysFail = true;

        var result = await _sut.GeocodeAsync("12 Lake Road, Kelaniya");

        result.Found.Should().BeFalse();
        result.ServiceAvailable.Should().BeFalse();
        result.Message.Should().Contain("coordinates");
        _uow.Repo<GeocodeCacheEntry>().Items.Should().BeEmpty();
    }

    [Fact]
    public async Task AnAddressThatMatchesNothing_IsNotAnError_AndIsNotCached()
    {
        _provider.Point = null;

        var result = await _sut.GeocodeAsync("zzzz qqqq nowhere");

        result.Found.Should().BeFalse();
        result.ServiceAvailable.Should().BeTrue();
        _uow.Repo<GeocodeCacheEntry>().Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public async Task ATooShortAddress_IsRejected(string address)
    {
        Func<Task> act = () => _sut.GeocodeAsync(address);
        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task ReverseGeocode_FallsBackGracefully()
    {
        (await _sut.ReverseGeocodeAsync(6.95, 79.92)).DisplayName.Should().Be("Somewhere");

        _provider.AlwaysFail = true;
        var down = await _sut.ReverseGeocodeAsync(6.95, 79.92);

        down.ServiceAvailable.Should().BeFalse();
        down.Latitude.Should().Be(6.95);
    }

    // ---------- routing ----------
    [Fact]
    public async Task Route_UsesTheRoutingProvider_WhenItIsAvailable()
    {
        var route = await _sut.GetRouteAsync(6.9, 79.9, 7.0, 80.0);

        route.IsEstimate.Should().BeFalse();
        route.DistanceKm.Should().Be(12.4);
        route.DurationMinutes.Should().Be(25);
        route.Source.Should().Be("fake");
    }

    [Fact]
    public async Task Route_FallsBackToALabelledEstimate_WhenTheProviderIsDown()
    {
        _provider.AlwaysFail = true;

        var route = await _sut.GetRouteAsync(6.9, 79.9, 7.0, 80.0);

        route.IsEstimate.Should().BeTrue();
        route.Source.Should().Be("estimate");
        route.DistanceKm.Should().BeGreaterThan(14);   // about 15.7 km in a straight line, times the road factor
        route.DistanceKm.Should().BeLessThan(30);
        route.DurationMinutes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Route_AlsoFallsBack_WhenTheProviderFindsNoRoad()
    {
        _provider.Route = null;

        var route = await _sut.GetRouteAsync(6.9, 79.9, 7.0, 80.0);

        route.IsEstimate.Should().BeTrue();
    }

    [Theory]
    [InlineData(91, 0, 0, 0)]
    [InlineData(0, 181, 0, 0)]
    [InlineData(0, 0, -91, 0)]
    public async Task OutOfRangeCoordinates_AreRejected(double a, double b, double c, double d)
    {
        Func<Task> act = () => _sut.GetRouteAsync(a, b, c, d);
        await act.Should().ThrowAsync<AppValidationException>();
    }
}
