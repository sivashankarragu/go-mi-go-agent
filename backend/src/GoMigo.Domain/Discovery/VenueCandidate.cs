namespace GoMigo.Domain.Discovery;

public enum VerificationStatus
{
    /// <summary>Source URL, supporting evidence and at least one practical detail (hours or price) were found.</summary>
    Verified,
    /// <summary>Venue has a source URL but key details are missing or unsupported.</summary>
    PartiallyVerified,
    /// <summary>No usable source could be confirmed.</summary>
    Unverified
}

/// <summary>
/// A venue found during research. Every uncertain field is nullable: <c>null</c> means "unknown",
/// and GoMigo never fills in opening hours, prices, availability or travel times it did not find.
/// </summary>
public sealed record VenueCandidate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public ActivityCategory Category { get; init; } = ActivityCategory.Other;
    public string? Address { get; init; }
    public string? Neighborhood { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? Description { get; init; }

    public required string SourceName { get; init; }
    public string? SourceUrl { get; init; }

    /// <summary>Opening hours exactly as published by the source; never inferred.</summary>
    public string? OpeningHours { get; init; }
    public decimal? EstimatedCostPerPerson { get; init; }
    public string? Currency { get; init; }
    public int? SuggestedVisitMinutes { get; init; }

    /// <summary>Short excerpt from the source supporting the extracted details.</summary>
    public string? Evidence { get; init; }
    public required DateTimeOffset LastCheckedUtc { get; init; }
    public VerificationStatus VerificationStatus { get; init; } = VerificationStatus.Unverified;

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

    /// <summary>Stable key used to drop duplicate results for the same venue.</summary>
    public required string DedupeKey { get; init; }
    public double RankScore { get; init; }

    public bool HasCoordinates => Latitude is not null && Longitude is not null;
}
