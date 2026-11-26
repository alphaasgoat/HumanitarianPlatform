using System.Globalization;
using System.Text.Json;
using Humanitarian.Application.Mapping;
using Microsoft.Extensions.Options;

namespace Humanitarian.Infrastructure.Mapping;

/// <summary>Adapter for an OSRM routing server: driving distance and time between two points.</summary>
public class OsrmRoutingProvider(HttpClient http, IOptions<MappingSettings> options) : IRoutingProvider
{
    public string Name => "osrm";

    public async Task<RouteInfo?> GetRouteAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct)
    {
        var s = options.Value;
        if (!s.Enabled) throw new InvalidOperationException("The mapping provider is disabled in configuration.");

        string C(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
        var url = $"{s.RoutingBaseUrl.TrimEnd('/')}/route/v1/driving/{C(fromLng)},{C(fromLat)};{C(toLng)},{C(toLat)}?overview=false";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.TryParseAdd(s.UserAgent);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var root = doc.RootElement;
        if (!root.TryGetProperty("code", out var code) || code.GetString() != "Ok") return null;
        if (!root.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0) return null;
        var route = routes[0];
        return new RouteInfo(route.GetProperty("distance").GetDouble() / 1000.0, route.GetProperty("duration").GetDouble() / 60.0);
    }
}
