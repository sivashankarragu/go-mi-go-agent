using System.Globalization;
using GoMigo.Domain.Discovery;

namespace GoMigo.Application.Research;

/// <summary>
/// Scores venues against the user's budget, time, mood and location. Unknown data is neither rewarded
/// nor treated as a dealbreaker: it earns a neutral partial score plus a warning so the user can check.
/// </summary>
public static class CandidateRanker
{
    public const double BudgetWeight = 35;
    public const double DurationWeight = 20;
    public const double CategoryWeight = 25;
    public const double VerificationWeight = 15;
    public const double ProximityWeight = 5;

    public static IReadOnlyList<VenueCandidate> Rank(IEnumerable<VenueCandidate> candidates, DiscoveryRequest request, int maxResults)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(request);

        // Exact mood matches are kept ahead of others so the result limit never drops them in favour of off-mood venues.
        return Deduplicate(candidates)
            .Select(c => Score(c, request))
            .OrderByDescending(c => request.Category == ActivityCategory.Any || c.Category == request.Category)
            .ThenByDescending(c => c.RankScore)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxResults))
            .ToList();
    }

    /// <summary>Keeps the most complete record for each venue (fewest missing fields, best verification).</summary>
    public static IEnumerable<VenueCandidate> Deduplicate(IEnumerable<VenueCandidate> candidates) =>
        candidates
            .GroupBy(c => c.DedupeKey)
            .Select(g => g
                .OrderBy(c => c.MissingFields.Count)
                .ThenBy(c => c.VerificationStatus)
                .First());

    public static VenueCandidate Score(VenueCandidate candidate, DiscoveryRequest request)
    {
        var warnings = candidate.Warnings.ToList();
        double score = 0;

        // Budget
        var costComparable = candidate.EstimatedCostPerPerson is not null
            && string.Equals(candidate.Currency, request.Currency, StringComparison.OrdinalIgnoreCase);
        if (!costComparable)
        {
            score += BudgetWeight * 0.4;
            warnings.Add(candidate.EstimatedCostPerPerson is null
                ? "Cost is unknown; confirm the price before going."
                : $"Price is in {candidate.Currency}, so it was not compared with your budget.");
        }
        else if (candidate.EstimatedCostPerPerson <= request.BudgetPerPerson)
        {
            score += BudgetWeight;
        }
        else
        {
            warnings.Add(string.Create(CultureInfo.InvariantCulture,
                $"Estimated cost {candidate.EstimatedCostPerPerson:0.##} {candidate.Currency} per person exceeds your budget of {request.BudgetPerPerson:0.##} {request.Currency}."));
        }

        // Time
        if (candidate.SuggestedVisitMinutes is not { } visit)
        {
            score += DurationWeight * 0.5;
        }
        else if (visit <= request.DurationMinutes)
        {
            score += DurationWeight;
        }
        else
        {
            warnings.Add($"Suggested visit of {visit} minutes is longer than your available time.");
        }

        // Mood / category
        if (request.Category == ActivityCategory.Any || candidate.Category == request.Category)
        {
            score += CategoryWeight;
        }
        else if (candidate.Category == ActivityCategory.Other)
        {
            score += CategoryWeight * 0.3;
        }

        // Evidence quality
        score += candidate.VerificationStatus switch
        {
            VerificationStatus.Verified => VerificationWeight,
            VerificationStatus.PartiallyVerified => VerificationWeight * 0.5,
            _ => 0
        };

        // Proximity, only when both sides have coordinates
        if (request.HasCoordinates && candidate.HasCoordinates)
        {
            var km = DistanceKm(request.Latitude!.Value, request.Longitude!.Value, candidate.Latitude!.Value, candidate.Longitude!.Value);
            score += km switch
            {
                <= 2 => ProximityWeight,
                <= 5 => ProximityWeight * 0.6,
                <= 10 => ProximityWeight * 0.2,
                _ => 0
            };
        }

        return candidate with { RankScore = Math.Round(score, 2), Warnings = warnings.Distinct().ToArray() };
    }

    /// <summary>Great-circle distance; used only for ranking, never presented as travel time.</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371;
        static double Rad(double deg) => deg * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
