namespace Humanitarian.Application.Mapping;

public record GeoPoint(double Latitude, double Longitude, string? DisplayName);

public record RouteInfo(double DistanceKm, double DurationMinutes);

/// <summary>Port to an external geocoding provider (the adapter lives in the infrastructure layer).</summary>
public interface IGeocodingProvider
{
    string Name { get; }
    Task<GeoPoint?> GeocodeAsync(string address, CancellationToken ct);
    Task<GeoPoint?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct);
}

/// <summary>Port to an external routing provider.</summary>
public interface IRoutingProvider
{
    string Name { get; }
    Task<RouteInfo?> GetRouteAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct);
}

public record GeocodeResultDto(
    bool Found, bool ServiceAvailable, double? Latitude, double? Longitude, string? DisplayName, string Source, string Message);

public record RouteResultDto(double DistanceKm, double DurationMinutes, string Source, bool IsEstimate, string Message);

public class MappingOptions
{
    public int MaxAttempts { get; init; } = 2;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(4);
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Roads are longer than the straight line; used when the routing service is unavailable.</summary>
    public double RoadFactor { get; init; } = 1.3;

    public double AverageSpeedKmh { get; init; } = 35;
}
