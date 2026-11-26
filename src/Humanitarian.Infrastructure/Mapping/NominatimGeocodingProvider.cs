using System.Globalization;
using System.Text.Json;
using Humanitarian.Application.Mapping;
using Microsoft.Extensions.Options;

namespace Humanitarian.Infrastructure.Mapping;

/// <summary>Adapter for OpenStreetMap Nominatim (geocoding and reverse geocoding). Only the address text is sent.</summary>
public class NominatimGeocodingProvider(HttpClient http, IOptions<MappingSettings> options) : IGeocodingProvider
{
    public string Name => "nominatim";

    public async Task<GeoPoint?> GeocodeAsync(string address, CancellationToken ct)
    {
        var s = Require();
        var url = $"{s.GeocoderBaseUrl.TrimEnd('/')}/search?q={Uri.EscapeDataString(address)}&format=jsonv2&limit=1";
        if (!string.IsNullOrWhiteSpace(s.CountryCodes)) url += $"&countrycodes={Uri.EscapeDataString(s.CountryCodes)}";

        using var doc = await GetJsonAsync(url, s, ct);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;
        return ReadPoint(doc.RootElement[0]);
    }

    public async Task<GeoPoint?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct)
    {
        var s = Require();
        var lat = latitude.ToString("0.#####", CultureInfo.InvariantCulture);
        var lon = longitude.ToString("0.#####", CultureInfo.InvariantCulture);
        var url = $"{s.GeocoderBaseUrl.TrimEnd('/')}/reverse?lat={lat}&lon={lon}&format=jsonv2";

        using var doc = await GetJsonAsync(url, s, ct);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || doc.RootElement.TryGetProperty("error", out _)) return null;
        var name = doc.RootElement.TryGetProperty("display_name", out var d) ? d.GetString() : null;
        return new GeoPoint(latitude, longitude, name);
    }

    private MappingSettings Require()
    {
        var s = options.Value;
        if (!s.Enabled) throw new InvalidOperationException("The mapping provider is disabled in configuration.");
        return s;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, MappingSettings s, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.TryParseAdd(s.UserAgent);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private static GeoPoint? ReadPoint(JsonElement item)
    {
        if (!item.TryGetProperty("lat", out var lat) || !item.TryGetProperty("lon", out var lon)) return null;
        if (!double.TryParse(lat.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !double.TryParse(lon.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)) return null;
        var name = item.TryGetProperty("display_name", out var d) ? d.GetString() : null;
        return new GeoPoint(latitude, longitude, name);
    }
}
