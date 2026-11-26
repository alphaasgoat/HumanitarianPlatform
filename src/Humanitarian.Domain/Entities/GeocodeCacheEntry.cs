using Humanitarian.Domain.Common;

namespace Humanitarian.Domain.Entities;

/// <summary>A resolved address. Saves calls to the external geocoder and still answers when that service is down.</summary>
public class GeocodeCacheEntry : BaseEntity
{
    /// <summary>The address as typed, trimmed and lower-cased.</summary>
    public string Query { get; set; } = "";

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? DisplayName { get; set; }
    public string Provider { get; set; } = "";
}
