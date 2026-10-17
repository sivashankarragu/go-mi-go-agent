using System.Text.Json;
using GoMigo.Application.Research;

namespace GoMigo.Infrastructure.Research.TinyFish;

/// <summary>
/// Turns a TinyFish run <c>result</c> into <see cref="RawVenue"/>s. The result shape depends on the agent's answer:
/// an object directly, or a list/plain text nested under <c>result.result</c>. Everything here is untrusted data.
/// </summary>
public static class TinyFishResultParser
{
    private const int MaxDepth = 4;

    public static IReadOnlyList<RawVenue> ParseVenues(JsonElement? result, string sourceName, string pageUrl, DateTimeOffset retrievedAtUtc, int maxVenues)
    {
        if (result is null)
        {
            return [];
        }

        var array = FindVenueArray(result.Value, 0);
        if (array is null)
        {
            return [];
        }

        return array.Value.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.Object)
            .Take(maxVenues)
            .Select(e => new RawVenue
            {
                Name = Text(e, "name", "venue_name", "title"),
                Category = Text(e, "category", "type"),
                Address = Text(e, "address"),
                Neighborhood = Text(e, "neighborhood", "neighbourhood", "area", "locality"),
                Latitude = Text(e, "latitude", "lat"),
                Longitude = Text(e, "longitude", "lng", "lon"),
                Description = Text(e, "description", "summary"),
                OpeningHours = Text(e, "opening_hours", "openingHours", "hours"),
                PriceText = Text(e, "price_text", "priceText", "price", "cost"),
                VisitDurationText = Text(e, "visit_duration_text", "visitDurationText", "duration"),
                Evidence = Text(e, "evidence", "quote"),
                SourceUrl = Text(e, "source_url", "sourceUrl", "url") ?? pageUrl,
                SourceName = sourceName,
                RetrievedAtUtc = retrievedAtUtc
            })
            .ToList();
    }

    private static JsonElement? FindVenueArray(JsonElement element, int depth)
    {
        if (depth > MaxDepth)
        {
            return null;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                return element;
            case JsonValueKind.Object:
                foreach (var key in new[] { "venues", "places", "items", "results", "result", "data" })
                {
                    if (TryGetProperty(element, key, out var child) && FindVenueArray(child, depth + 1) is { } found)
                    {
                        return found;
                    }
                }
                return null;
            case JsonValueKind.String:
                return TryParseEmbeddedJson(element.GetString(), depth);
            default:
                return null;
        }
    }

    /// <summary>Agents sometimes answer with JSON inside text or a ```json fence.</summary>
    private static JsonElement? TryParseEmbeddedJson(string? text, int depth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var start = text.IndexOfAny(['[', '{']);
        var end = text.LastIndexOfAny([']', '}']);
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            return FindVenueArray(doc.RootElement.Clone(), depth + 1);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(obj, name, out var value))
            {
                continue;
            }

            var text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.Array => string.Join("; ", value.EnumerateArray()
                    .Where(v => v.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                    .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())),
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }
        return null;
    }

    private static bool TryGetProperty(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }
}
