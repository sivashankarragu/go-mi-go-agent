using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using GoMigo.Application.Places;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoMigo.Infrastructure.Places;

public sealed class PhotonOptions
{
    public const string SectionName = "Places:Photon";

    /// <summary>Public Photon instance (OpenStreetMap data, fair use). Self-host or swap the provider for heavy traffic.</summary>
    public string BaseUrl { get; set; } = "https://photon.komoot.io";
    public int TimeoutSeconds { get; set; } = 5;
    public int CacheMinutes { get; set; } = 30;

    /// <summary>Results outside this box are not returned: minLon,minLat,maxLon,maxLat (greater Bengaluru).</summary>
    public string BoundingBox { get; set; } = "77.35,12.75,77.85,13.20";

    /// <summary>Default location bias when the caller has no position (Bengaluru centre).</summary>
    public double DefaultLatitude { get; set; } = 12.9716;
    public double DefaultLongitude { get; set; } = 77.5946;
}

/// <summary>
/// <see cref="IPlaceSearchProvider"/> backed by Photon (https://photon.komoot.io), a free OSM geocoder built for
/// type-ahead. Responses are cached in memory to keep request volume low.
/// </summary>
public sealed class PhotonPlaceSearchProvider(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<PhotonOptions> options,
    ILogger<PhotonPlaceSearchProvider> logger) : IPlaceSearchProvider
{
    public async Task<IReadOnlyList<ProviderPlace>> SearchAsync(
        string query, double? biasLatitude, double? biasLongitude, int limit, CancellationToken cancellationToken)
    {
        var o = options.Value;
        var lat = biasLatitude ?? o.DefaultLatitude;
        var lon = biasLongitude ?? o.DefaultLongitude;
        var url = string.Create(CultureInfo.InvariantCulture,
            $"api/?q={Uri.EscapeDataString(query)}&limit={limit}&lang=en&lat={lat:F4}&lon={lon:F4}&bbox={o.BoundingBox}");

        return await cache.GetOrCreateAsync($"photon:search:{url}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(o.CacheMinutes);
            return (IReadOnlyList<ProviderPlace>)await FetchAsync(url, cancellationToken);
        }) ?? [];
    }

    public async Task<ProviderPlace?> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        // ~11 m precision is plenty for naming a position and lets nearby requests share the cache.
        var url = string.Create(CultureInfo.InvariantCulture, $"reverse?lat={latitude:F4}&lon={longitude:F4}&lang=en&limit=1");
        var places = await cache.GetOrCreateAsync($"photon:reverse:{url}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.CacheMinutes);
            return await FetchAsync(url, cancellationToken);
        });
        return places?.FirstOrDefault();
    }

    private async Task<List<ProviderPlace>> FetchAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(relativeUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Photon returned HTTP {StatusCode}", (int)response.StatusCode);
            throw new HttpRequestException($"Photon returned HTTP {(int)response.StatusCode}.");
        }

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var results = new List<ProviderPlace>();
        if (!doc.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("geometry", out var geometry)
                || !geometry.TryGetProperty("coordinates", out var coords)
                || coords.ValueKind != JsonValueKind.Array || coords.GetArrayLength() < 2
                || !feature.TryGetProperty("properties", out var p))
            {
                continue;
            }

            results.Add(new ProviderPlace(
                Name: Str(p, "name"),
                HouseNumber: Str(p, "housenumber"),
                Street: Str(p, "street"),
                Locality: Str(p, "locality"),
                District: Str(p, "district"),
                City: Str(p, "city"),
                State: Str(p, "state"),
                CountryCode: Str(p, "countrycode"),
                OsmKey: Str(p, "osm_key"),
                OsmValue: Str(p, "osm_value"),
                Latitude: coords[1].GetDouble(),
                Longitude: coords[0].GetDouble()));
        }
        return results;
    }

    private static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;
}
