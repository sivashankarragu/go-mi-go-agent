using GoMigo.Application.Research;

namespace GoMigo.Application.Discovery;

public sealed record Area(string Name, double Latitude, double Longitude);

public sealed record ResolvedArea(Area? Area, AreaMatch Match, double? DistanceKm);

public enum AreaMatch
{
    /// <summary>No usable location: city-wide suggestions.</summary>
    City,
    /// <summary>The typed location names a known neighbourhood.</summary>
    LocationText,
    /// <summary>Nearest neighbourhood to the shared coordinates.</summary>
    Coordinates
}

/// <summary>
/// Well-known Bengaluru neighbourhoods with approximate centre points, used to label suggestions
/// for the user's area. Not venue data: nothing here is shown as a fact about a place.
/// </summary>
public static class BengaluruAreas
{
    public const string City = "Bengaluru";

    /// <summary>Beyond this distance from every known area, fall back to city-wide suggestions.</summary>
    public const double MaxNearestDistanceKm = 15;

    public static readonly IReadOnlyList<Area> All =
    [
        new("Indiranagar", 12.9719, 77.6412),
        new("Koramangala", 12.9352, 77.6245),
        new("MG Road", 12.9756, 77.6066),
        new("Church Street", 12.9752, 77.6047),
        new("Cubbon Park", 12.9763, 77.5929),
        new("Ulsoor", 12.9817, 77.6286),
        new("Richards Town", 13.0005, 77.6189),
        new("Malleshwaram", 13.0035, 77.5647),
        new("Basavanagudi", 12.9406, 77.5738),
        new("Jayanagar", 12.9250, 77.5938),
        new("JP Nagar", 12.9063, 77.5857),
        new("BTM Layout", 12.9166, 77.6101),
        new("HSR Layout", 12.9116, 77.6474),
        new("Bellandur", 12.9304, 77.6784),
        new("Marathahalli", 12.9592, 77.6974),
        new("Whitefield", 12.9698, 77.7500),
        new("Hebbal", 13.0358, 77.5970),
        new("Yelahanka", 13.1005, 77.5963),
        new("Electronic City", 12.8452, 77.6602),
    ];

    /// <summary>Typed neighbourhood wins; otherwise the nearest area to the coordinates; otherwise the whole city.</summary>
    public static ResolvedArea Resolve(string? locationText, double? latitude, double? longitude)
    {
        if (!string.IsNullOrWhiteSpace(locationText))
        {
            var text = Simplify(locationText);
            var named = All.FirstOrDefault(a => text.Contains(Simplify(a.Name), StringComparison.Ordinal));
            if (named is not null)
            {
                return new ResolvedArea(named, AreaMatch.LocationText, null);
            }
        }

        if (latitude is { } lat && longitude is { } lon && lat is >= -90 and <= 90 && lon is >= -180 and <= 180)
        {
            var nearest = All
                .Select(a => (Area: a, Km: CandidateRanker.DistanceKm(lat, lon, a.Latitude, a.Longitude)))
                .MinBy(x => x.Km);
            if (nearest.Km <= MaxNearestDistanceKm)
            {
                return new ResolvedArea(nearest.Area, AreaMatch.Coordinates, Math.Round(nearest.Km, 1));
            }
        }

        return new ResolvedArea(null, AreaMatch.City, null);
    }

    // "Indira Nagar" and "indiranagar" should both match.
    private static string Simplify(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
