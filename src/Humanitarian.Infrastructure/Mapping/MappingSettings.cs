namespace Humanitarian.Infrastructure.Mapping;

/// <summary>Bound from the "Mapping" section of appsettings. Set Enabled=false to demonstrate the offline fallbacks.</summary>
public class MappingSettings
{
    public const string Section = "Mapping";

    public bool Enabled { get; set; } = true;

    /// <summary>OpenStreetMap Nominatim. Its public server is for light use only (identify yourself, at most 1 request/second).</summary>
    public string GeocoderBaseUrl { get; set; } = "https://nominatim.openstreetmap.org";

    /// <summary>OSRM demo server (driving routes). For heavy use, host your own.</summary>
    public string RoutingBaseUrl { get; set; } = "https://router.project-osrm.org";

    public string UserAgent { get; set; } = "HumanitarianPlatform-Coursework/1.0";

    /// <summary>Optional ISO country codes (for example "lk") to prefer results in the crisis country.</summary>
    public string? CountryCodes { get; set; }
}

/// <summary>One shared HttpClient (the recommended way to avoid exhausting sockets); connections are recycled every 2 minutes.</summary>
internal static class ExternalHttp
{
    public static readonly HttpClient Client = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) })
    {
        Timeout = TimeSpan.FromSeconds(30) // the per-call timeout is enforced by ResilientCall; this is only a ceiling
    };
}
