using System.Globalization;
using GoMigo.Application.Common;
using GoMigo.Application.Discovery;
using Microsoft.Extensions.Logging;

namespace GoMigo.Application.Places;

/// <summary>
/// Place type-ahead and reverse geocoding. GoMigo's own neighbourhoods come first, then map-provider results.
/// Provider failures degrade to neighbourhood-only results instead of failing the request.
/// </summary>
public sealed class PlacesService(IPlaceSearchProvider provider, ILogger<PlacesService> logger)
{
    public const int MinQueryLength = 2;
    public const int MaxQueryLength = 100;
    public const int MaxSuggestions = 6;

    public async Task<IReadOnlyList<PlaceSuggestionDto>> AutocompleteAsync(
        string? query, double? latitude, double? longitude, CancellationToken cancellationToken)
    {
        var q = query?.Trim() ?? string.Empty;
        if (q.Length < MinQueryLength || q.Length > MaxQueryLength)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["q"] = [$"Search text must be {MinQueryLength}–{MaxQueryLength} characters."]
            });
        }

        // "Indiranagar, Bengaluru" should still match the Indiranagar neighbourhood.
        var simplified = Simplify(q).Replace("bengaluru", "").Replace("bangalore", "");
        var results = BengaluruAreas.All
            .Where(a => simplified.Length > 0)
            .Where(a => Simplify(a.Name).StartsWith(simplified, StringComparison.Ordinal)
                        || (simplified.Length >= 4 && Simplify(a.Name).Contains(simplified, StringComparison.Ordinal)))
            .Take(3)
            .Select(a => new PlaceSuggestionDto(a.Name, BengaluruAreas.City, $"{a.Name}, {BengaluruAreas.City}",
                a.Latitude, a.Longitude, PlaceKind.Neighbourhood, a.Name))
            .ToList();

        try
        {
            var places = await provider.SearchAsync(q, latitude, longitude, MaxSuggestions + 2, cancellationToken);
            foreach (var place in places)
            {
                if (results.Count >= MaxSuggestions) break;
                var suggestion = ToSuggestion(place);
                if (suggestion is not null && !results.Any(r => IsSamePlace(r, suggestion)))
                {
                    results.Add(suggestion);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Place search provider failed; returning neighbourhood matches only");
        }

        return results;
    }

    /// <summary>Names a coordinate. Falls back to the nearest neighbourhood, then to the raw coordinates.</summary>
    public async Task<PlaceSuggestionDto> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180 || double.IsNaN(latitude) || double.IsNaN(longitude))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["coordinates"] = ["Latitude must be -90..90 and longitude -180..180."]
            });
        }

        try
        {
            var place = await provider.ReverseAsync(latitude, longitude, cancellationToken);
            if (place is not null && ToSuggestion(place with { Latitude = latitude, Longitude = longitude }) is { } named)
            {
                return named;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Reverse geocoding failed; falling back to nearest neighbourhood");
        }

        var nearest = BengaluruAreas.Resolve(null, latitude, longitude).Area;
        if (nearest is not null)
        {
            return new PlaceSuggestionDto($"Near {nearest.Name}", BengaluruAreas.City, $"{nearest.Name}, {BengaluruAreas.City}",
                latitude, longitude, PlaceKind.Neighbourhood, nearest.Name);
        }

        var coords = string.Create(CultureInfo.InvariantCulture, $"{latitude:F5}, {longitude:F5}");
        return new PlaceSuggestionDto("Current location", coords, coords, latitude, longitude, PlaceKind.Area, null);
    }

    /// <summary>"Toit Brewpub" + "100 Feet Road, Indiranagar, Bengaluru"; null when the result has no usable name.</summary>
    public static PlaceSuggestionDto? ToSuggestion(ProviderPlace p)
    {
        var streetLine = Join(" ", p.HouseNumber, p.Street);
        var name = FirstNonEmpty(p.Name, streetLine, p.Locality, p.District);
        if (name is null)
        {
            return null;
        }

        var nearest = BengaluruAreas.Resolve(
            string.Join(' ', name, p.Street, p.Locality, p.District), p.Latitude, p.Longitude).Area?.Name;
        // OSM often omits the city inside Bengaluru; fill it in only when the place is near a known neighbourhood.
        var city = p.City ?? (nearest is not null ? BengaluruAreas.City : p.State);

        var parts = new[] { p.Name is null ? null : streetLine, p.Locality, p.District, city }
            .Where(s => !string.IsNullOrWhiteSpace(s) && !string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var detail = parts.Count == 0 ? null : string.Join(", ", parts);
        var kind = p.OsmKey switch
        {
            "place" => PlaceKind.Area,
            "highway" => PlaceKind.Street,
            _ => p.Name is null ? PlaceKind.Street : PlaceKind.Venue
        };

        // Keep the label short but specific: name plus the most local area and the city.
        var labelParts = new[] { name, p.Locality ?? p.District, city }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var label = string.Join(", ", labelParts);

        return new PlaceSuggestionDto(name, detail, label, p.Latitude, p.Longitude, kind, nearest);
    }

    /// <summary>Same label, or same name within 2 km (e.g. OSM's "Indiranagar" next to GoMigo's neighbourhood).</summary>
    private static bool IsSamePlace(PlaceSuggestionDto a, PlaceSuggestionDto b) =>
        string.Equals(a.Label, b.Label, StringComparison.OrdinalIgnoreCase)
        || (Simplify(a.Name) == Simplify(b.Name)
            && Research.CandidateRanker.DistanceKm(a.Latitude, a.Longitude, b.Latitude, b.Longitude) < 2);

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? Join(string separator, params string?[] values)
    {
        var parts = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        return parts.Count == 0 ? null : string.Join(separator, parts);
    }

    private static string Simplify(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
