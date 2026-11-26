using Humanitarian.Application.Common;
using Humanitarian.Application.Requests.Duplicates;
using Humanitarian.Domain.Entities;

namespace Humanitarian.Application.Mapping;

public interface IMappingService
{
    Task<GeocodeResultDto> GeocodeAsync(string address, CancellationToken ct = default);
    Task<GeocodeResultDto> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default);
    Task<RouteResultDto> GetRouteAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct = default);
}

/// <summary>
/// Location services with graceful degradation. Every external call has a timeout and a retry; when the provider is
/// down the caller still gets a useful answer instead of an error:
///   geocoding -> an earlier cached answer, otherwise "enter the coordinates manually";
///   routing   -> a straight-line distance with a road factor, clearly labelled as an estimate.
/// Only the address text is sent to the provider, never names or other personal data.
/// </summary>
public class MappingService(
    IUnitOfWork uow, IGeocodingProvider geocoder, IRoutingProvider router, TimeProvider time,
    MappingOptions? options = null) : IMappingService
{
    private readonly MappingOptions _o = options ?? new MappingOptions();

    public async Task<GeocodeResultDto> GeocodeAsync(string address, CancellationToken ct = default)
    {
        var text = (address ?? "").Trim();
        if (text.Length < 3 || text.Length > 300)
            throw new AppValidationException("The address must be 3-300 characters.");
        var key = string.Join(' ', text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var cache = uow.Repository<GeocodeCacheEntry>();
        var cached = await cache.FirstOrDefaultAsync(c => c.Query == key, ct);
        if (cached is not null)
            return new GeocodeResultDto(true, true, cached.Latitude, cached.Longitude, cached.DisplayName, "cache",
                "Location found (saved from an earlier lookup).");

        GeoPoint? point;
        try
        {
            point = await ResilientCall.ExecuteAsync(token => geocoder.GeocodeAsync(text, token),
                _o.MaxAttempts, _o.Timeout, _o.RetryDelay, ct);
        }
        catch (ExternalServiceException)
        {
            return new GeocodeResultDto(false, false, null, null, null, "unavailable",
                "The map service is not available right now. Please enter the coordinates yourself.");
        }

        if (point is null)
            return new GeocodeResultDto(false, true, null, null, null, geocoder.Name,
                "No place matched that address. Try adding the town, or enter the coordinates.");

        await cache.AddAsync(new GeocodeCacheEntry
        {
            Query = key,
            Latitude = point.Latitude,
            Longitude = point.Longitude,
            DisplayName = point.DisplayName,
            Provider = geocoder.Name,
            CreatedAtUtc = time.GetUtcNow().UtcDateTime
        }, ct);
        await uow.SaveChangesAsync(ct);
        return new GeocodeResultDto(true, true, point.Latitude, point.Longitude, point.DisplayName, geocoder.Name, "Location found.");
    }

    public async Task<GeocodeResultDto> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        RequireValidCoordinates(latitude, longitude);
        try
        {
            var point = await ResilientCall.ExecuteAsync(token => geocoder.ReverseGeocodeAsync(latitude, longitude, token),
                _o.MaxAttempts, _o.Timeout, _o.RetryDelay, ct);
            return point is null
                ? new GeocodeResultDto(false, true, latitude, longitude, null, geocoder.Name, "No address was found at that point.")
                : new GeocodeResultDto(true, true, latitude, longitude, point.DisplayName, geocoder.Name, "Address found.");
        }
        catch (ExternalServiceException)
        {
            return new GeocodeResultDto(false, false, latitude, longitude, null, "unavailable",
                "The map service is not available right now.");
        }
    }

    public async Task<RouteResultDto> GetRouteAsync(
        double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct = default)
    {
        RequireValidCoordinates(fromLat, fromLng);
        RequireValidCoordinates(toLat, toLng);
        try
        {
            var route = await ResilientCall.ExecuteAsync(token => router.GetRouteAsync(fromLat, fromLng, toLat, toLng, token),
                _o.MaxAttempts, _o.Timeout, _o.RetryDelay, ct);
            if (route is not null)
                return new RouteResultDto(Math.Round(route.DistanceKm, 1), Math.Round(route.DurationMinutes), router.Name, false,
                    "Road distance and driving time.");
        }
        catch (ExternalServiceException)
        {
            // fall through to the estimate
        }
        return Estimate(fromLat, fromLng, toLat, toLng);
    }

    private RouteResultDto Estimate(double fromLat, double fromLng, double toLat, double toLng)
    {
        var km = GeoMath.DistanceKm(fromLat, fromLng, toLat, toLng) * _o.RoadFactor;
        var minutes = km / _o.AverageSpeedKmh * 60;
        return new RouteResultDto(Math.Round(km, 1), Math.Round(minutes), "estimate", true,
            "The routing service is not available, so this is an estimate (straight line plus a road allowance).");
    }

    private static void RequireValidCoordinates(double lat, double lng)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180 || double.IsNaN(lat) || double.IsNaN(lng))
            throw new AppValidationException("Coordinates are out of range.");
    }
}
