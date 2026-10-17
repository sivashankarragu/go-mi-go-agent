namespace GoMigo.Application.Places;

public enum PlaceKind
{
    /// <summary>A GoMigo-known Bengaluru neighbourhood.</summary>
    Neighbourhood,
    /// <summary>A locality, suburb or district from the map provider.</summary>
    Area,
    Street,
    /// <summary>A named place: venue, landmark, building.</summary>
    Venue
}

/// <summary>
/// One place suggestion. <c>Name</c> is the main line, <c>Detail</c> the secondary address line,
/// <c>Label</c> the text to put in the location field. <c>NearestArea</c> is the closest GoMigo neighbourhood, if any.
/// </summary>
public sealed record PlaceSuggestionDto(
    string Name,
    string? Detail,
    string Label,
    double Latitude,
    double Longitude,
    PlaceKind Kind,
    string? NearestArea);

/// <summary>Raw result from a map provider, before GoMigo formats it.</summary>
public sealed record ProviderPlace(
    string? Name,
    string? HouseNumber,
    string? Street,
    string? Locality,
    string? District,
    string? City,
    string? State,
    string? CountryCode,
    string? OsmKey,
    string? OsmValue,
    double Latitude,
    double Longitude);

/// <summary>Map search used for type-ahead and for naming the user's current position.</summary>
public interface IPlaceSearchProvider
{
    Task<IReadOnlyList<ProviderPlace>> SearchAsync(string query, double? biasLatitude, double? biasLongitude, int limit, CancellationToken cancellationToken);

    Task<ProviderPlace?> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken);
}
