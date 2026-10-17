using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GoMigo.Domain.Discovery;

namespace GoMigo.Application.Research;

public sealed record NormalizationResult(VenueCandidate? Candidate, string? RejectionReason)
{
    public bool IsAccepted => Candidate is not null;
}

/// <summary>
/// Maps untrusted provider output into <see cref="VenueCandidate"/>. Unknown values stay <c>null</c>,
/// every assumption is recorded as a warning, and text that looks like instructions is discarded.
/// </summary>
public static partial class VenueNormalizer
{
    public static class Fields
    {
        public const string Address = "address";
        public const string Coordinates = "coordinates";
        public const string OpeningHours = "openingHours";
        public const string EstimatedCost = "estimatedCostPerPerson";
        public const string VisitDuration = "suggestedVisitMinutes";
        public const string SourceUrl = "sourceUrl";
        public const string Evidence = "evidence";
    }

    private const int MaxNameLength = 150;
    private const int MaxShortTextLength = 300;
    private const int MaxLongTextLength = 800;

    public const string SuspiciousContentWarning =
        "Some source text was ignored because it looked like instructions rather than venue information.";

    public static NormalizationResult Normalize(RawVenue raw, DiscoveryRequest request, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<string>();
        var missing = new List<string>();

        var name = CleanText(raw.Name, MaxNameLength, warnings);
        if (name is null)
        {
            return new NormalizationResult(null, "Venue name is missing.");
        }

        var address = CleanText(raw.Address, MaxShortTextLength, warnings);
        var neighborhood = CleanText(raw.Neighborhood, MaxShortTextLength, warnings);
        var description = CleanText(raw.Description, MaxLongTextLength, warnings);
        var openingHours = CleanText(raw.OpeningHours, MaxShortTextLength, warnings);
        var evidence = CleanText(raw.Evidence, MaxLongTextLength, warnings);

        var sourceUrl = ParseHttpUrl(raw.SourceUrl);
        if (raw.SourceUrl is not null && sourceUrl is null)
        {
            warnings.Add("Source link was not a valid web address and was removed.");
        }
        var sourceName = CleanText(raw.SourceName, MaxShortTextLength, warnings)
            ?? (sourceUrl is not null ? new Uri(sourceUrl).Host : "Unknown source");

        var (latitude, longitude) = ParseCoordinates(raw.Latitude, raw.Longitude);

        var (cost, currency) = ParsePrice(raw.PriceText, request.Currency, warnings);
        var visitMinutes = ParseDurationMinutes(raw.VisitDurationText);
        if (!string.IsNullOrWhiteSpace(raw.VisitDurationText) && visitMinutes is null)
        {
            warnings.Add("Suggested visit duration could not be interpreted.");
        }

        if (address is null && neighborhood is null) missing.Add(Fields.Address);
        if (latitude is null) missing.Add(Fields.Coordinates);
        if (openingHours is null) missing.Add(Fields.OpeningHours);
        if (cost is null) missing.Add(Fields.EstimatedCost);
        if (visitMinutes is null) missing.Add(Fields.VisitDuration);
        if (sourceUrl is null) missing.Add(Fields.SourceUrl);
        if (evidence is null) missing.Add(Fields.Evidence);

        var verification = sourceUrl is null
            ? VerificationStatus.Unverified
            : evidence is not null && (openingHours is not null || cost is not null)
                ? VerificationStatus.Verified
                : VerificationStatus.PartiallyVerified;

        var candidate = new VenueCandidate
        {
            Name = name,
            Category = ParseCategory(raw.Category, description),
            Address = address,
            Neighborhood = neighborhood,
            Latitude = latitude,
            Longitude = longitude,
            Description = description,
            SourceName = sourceName,
            SourceUrl = sourceUrl,
            OpeningHours = openingHours,
            EstimatedCostPerPerson = cost,
            Currency = currency,
            SuggestedVisitMinutes = visitMinutes,
            Evidence = evidence,
            LastCheckedUtc = (raw.RetrievedAtUtc ?? nowUtc).ToUniversalTime(),
            VerificationStatus = verification,
            Warnings = warnings.Distinct().ToArray(),
            MissingFields = missing,
            DedupeKey = BuildDedupeKey(name, neighborhood)
        };
        return new NormalizationResult(candidate, null);
    }

    /// <summary>Trims, removes control characters, collapses whitespace, caps length and drops instruction-like text.</summary>
    public static string? CleanText(string? value, int maxLength, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(char.IsControl(c) ? ' ' : c);
        }
        var text = WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
        if (text.Length == 0 || IsPlaceholder(text))
        {
            return null;
        }

        if (InstructionLikeRegex().IsMatch(text))
        {
            warnings.Add(SuspiciousContentWarning);
            return null;
        }

        return text.Length <= maxLength ? text : text[..(maxLength - 1)].TrimEnd() + "…";
    }

    public static string? ParseHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }
        return uri.AbsoluteUri;
    }

    public static (double? Latitude, double? Longitude) ParseCoordinates(string? latitude, string? longitude)
    {
        if (double.TryParse(latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
            && lat is >= -90 and <= 90 && lon is >= -180 and <= 180
            && !(lat == 0 && lon == 0))
        {
            return (lat, lon);
        }
        return (null, null);
    }

    /// <summary>
    /// Interprets published price text, e.g. "₹300", "Rs. 400-600", "₹1,200 for two", "Free entry".
    /// Ranges use the upper bound so the budget check stays conservative.
    /// </summary>
    public static (decimal? Amount, string? Currency) ParsePrice(string? text, string fallbackCurrency, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, null);
        }

        var lower = text.ToLowerInvariant();
        var numbers = NumberRegex().Matches(text)
            .Select(m => decimal.TryParse(m.Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null)
            .Where(d => d is not null)
            .Select(d => d!.Value)
            .ToList();

        if (numbers.Count == 0)
        {
            if (FreeRegex().IsMatch(lower))
            {
                return (0m, fallbackCurrency);
            }
            warnings.Add("Published price could not be interpreted.");
            return (null, null);
        }

        string? currency = null;
        if (text.Contains('₹') || RupeeRegex().IsMatch(lower)) currency = "INR";
        else if (text.Contains('$') || lower.Contains("usd")) currency = "USD";

        // Only the first number unless the text is an explicit range ("400-600", "400 to 600").
        var isRange = RangeRegex().IsMatch(text);
        var amount = isRange ? numbers.Max() : numbers[0];
        if (isRange)
        {
            warnings.Add("Price was given as a range; the upper bound is used.");
        }
        if (!isRange && StartingPriceRegex().IsMatch(lower))
        {
            warnings.Add("Price is a starting price for a single item; expect to spend more.");
        }
        if (ForTwoRegex().IsMatch(lower))
        {
            amount = decimal.Round(amount / 2, 2);
            warnings.Add("Price was published for two people; halved to estimate the cost per person.");
        }
        if (currency is null)
        {
            currency = fallbackCurrency;
            warnings.Add($"Currency was not stated; assumed {fallbackCurrency}.");
        }

        return (amount, currency);
    }

    /// <summary>Interprets durations like "90 min", "1-2 hours", "1 hr 30 mins". Ranges use the upper bound.</summary>
    public static int? ParseDurationMinutes(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        double total = 0;
        foreach (Match match in DurationRegex().Matches(text.ToLowerInvariant()))
        {
            var valueText = match.Groups["upper"].Success ? match.Groups["upper"].Value : match.Groups["value"].Value;
            if (!double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }
            total += match.Groups["unit"].Value.StartsWith('h') ? value * 60 : value;
        }

        var minutes = (int)Math.Round(total);
        return minutes is > 0 and <= 24 * 60 ? minutes : null;
    }

    public static ActivityCategory ParseCategory(string? category, string? description)
    {
        if (Enum.TryParse<ActivityCategory>(category?.Trim(), ignoreCase: true, out var parsed)
            && parsed is not ActivityCategory.Any)
        {
            return parsed;
        }
        if (category is not null && TravelGuideSections.TryGetValue(category.Trim(), out var section))
        {
            return section;
        }

        var text = $" {category} {description} ".ToLowerInvariant();
        foreach (var (target, keywords) in CategoryKeywords)
        {
            if (keywords.Any(k => text.Contains(k, StringComparison.Ordinal)))
            {
                return target;
            }
        }
        return ActivityCategory.Other;
    }

    public static string BuildDedupeKey(string name, string? neighborhood)
    {
        static string Simplify(string? s) =>
            new((s ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        return $"{Simplify(name)}|{Simplify(neighborhood)}";
    }

    private static bool IsPlaceholder(string text) =>
        text.Equals("null", StringComparison.OrdinalIgnoreCase)
        || text.Equals("n/a", StringComparison.OrdinalIgnoreCase)
        || text.Equals("unknown", StringComparison.OrdinalIgnoreCase)
        || text.Equals("not available", StringComparison.OrdinalIgnoreCase)
        || text == "-";

    // Section headings used by travel guides such as Wikivoyage, which agents often return as the category.
    private static readonly Dictionary<string, ActivityCategory> TravelGuideSections = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Eat"] = ActivityCategory.Food,
        ["Drink"] = ActivityCategory.Nightlife,
        ["See"] = ActivityCategory.Culture,
        ["Learn"] = ActivityCategory.Culture,
        ["Do"] = ActivityCategory.Fun,
        ["Buy"] = ActivityCategory.Shopping,
    };

    // Order matters: more specific categories first (a "brewery" is nightlife, not food).
    private static readonly (ActivityCategory Category, string[] Keywords)[] CategoryKeywords =
    [
        (ActivityCategory.Nightlife, [" bar", " pub", "brewery", "brewpub", "club", "lounge", "nightlife"]),
        (ActivityCategory.Relaxation, [" spa", "massage", "wellness", "yoga", "relax"]),
        (ActivityCategory.Culture, ["museum", "gallery", "theatre", "theater", "heritage", "exhibition", "history", "culture", " art"]),
        (ActivityCategory.Outdoors, [" park", " lake", "garden", "trek", "hike", "outdoor", "nature"]),
        (ActivityCategory.Shopping, ["mall", "market", "bazaar", "shopping", "boutique", " store"]),
        (ActivityCategory.Fun, ["arcade", "bowling", "escape room", "gaming", "karaoke", "go-kart", "trampoline", "comedy", "board game", " fun", "activity", "adventure"]),
        (ActivityCategory.Food, ["restaurant", "cafe", "café", "food", "bakery", "dessert", "dining", "bistro", "eatery"])
    ];

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\b(ignore|disregard|forget)\b.{0,40}\b(instructions|prompt|rules)\b|\bsystem prompt\b|\byou are (an? |the )?(ai|assistant|language model|chatbot)\b|<\s*script\b|\bnew instructions\b", RegexOptions.IgnoreCase)]
    private static partial Regex InstructionLikeRegex();

    [GeneratedRegex(@"\d{1,3}(?:,\d{2,3})+(?:\.\d+)?|\d+(?:\.\d+)?")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\bfree\b")]
    private static partial Regex FreeRegex();

    [GeneratedRegex(@"\b(rs\.?|inr|rupees?)(?=\s|\d|$)")]
    private static partial Regex RupeeRegex();

    [GeneratedRegex(@"\bfor\s+(two|2)\b(?!\s*(hours?|hrs?|h|minutes?|mins?)\b)|\b(two|2)\s+people\b|\bper\s+couple\b")]
    private static partial Regex ForTwoRegex();

    [GeneratedRegex(@"\b(from|starting|starts at|onwards)\b|\+\s*$")]
    private static partial Regex StartingPriceRegex();

    [GeneratedRegex(@"\d\s*(-|–|to)\s*(₹|rs\.?\s*|inr\s*)?\d", RegexOptions.IgnoreCase)]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"(?<value>\d+(?:\.\d+)?)\s*(?:(?:-|–|to)\s*(?<upper>\d+(?:\.\d+)?))?\s*(?<unit>hours?|hrs?|h|minutes?|mins?|m)\b")]
    private static partial Regex DurationRegex();
}
