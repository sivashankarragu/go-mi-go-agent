namespace GoMigo.Domain.Itineraries;

/// <summary>
/// An ordered plan built from ranked venues. Bootstrap model only: no route optimisation yet.
/// Travel and cost figures stay <c>null</c> when unknown rather than being estimated.
/// </summary>
public sealed record Itinerary
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid ResearchRunId { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public IReadOnlyList<ItineraryStop> Stops { get; init; } = Array.Empty<ItineraryStop>();
    public string Currency { get; init; } = Discovery.Money.DefaultCurrency;

    /// <summary>Sum of stop costs, or <c>null</c> if any stop's cost is unknown.</summary>
    public decimal? TotalEstimatedCostPerPerson =>
        Stops.Any(s => s.EstimatedCostPerPerson is null) ? null : Stops.Sum(s => s.EstimatedCostPerPerson!.Value);

    /// <summary>Sum of visit and travel time, or <c>null</c> if any part is unknown.</summary>
    public int? TotalMinutes =>
        Stops.Any(s => s.VisitMinutes is null || (s.Order > 1 && s.TravelMinutesFromPrevious is null))
            ? null
            : Stops.Sum(s => s.VisitMinutes!.Value + (s.TravelMinutesFromPrevious ?? 0));
}

public sealed record ItineraryStop
{
    public required int Order { get; init; }
    public required Guid VenueCandidateId { get; init; }
    public required string VenueName { get; init; }
    public int? VisitMinutes { get; init; }
    public decimal? EstimatedCostPerPerson { get; init; }

    /// <summary>Travel time from the previous stop; <c>null</c> for the first stop or when unknown.</summary>
    public int? TravelMinutesFromPrevious { get; init; }
    public string? TravelMode { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
