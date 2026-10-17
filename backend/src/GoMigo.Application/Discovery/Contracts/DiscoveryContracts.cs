using GoMigo.Domain.Discovery;
using GoMigo.Domain.Research;

namespace GoMigo.Application.Discovery.Contracts;

/// <summary>Body of <c>POST /api/discovery/research</c>. Fields are nullable so missing values produce validation errors.</summary>
public sealed record DiscoveryRequestDto
{
    /// <summary>Free-text location, e.g. "Indiranagar, Bengaluru". Optional only when coordinates are given.</summary>
    public string? LocationText { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    /// <summary>Available time in minutes (30–720).</summary>
    public int? DurationMinutes { get; init; }
    /// <summary>Budget per person (0–100000).</summary>
    public decimal? BudgetPerPerson { get; init; }
    /// <summary>ISO currency code. Only INR is supported for now; defaults to INR.</summary>
    public string? Currency { get; init; }
    /// <summary>Preferred activity; defaults to Any.</summary>
    public ActivityCategory? Category { get; init; }
    /// <summary>When the outing starts; defaults to now.</summary>
    public DateTimeOffset? StartTime { get; init; }
    public string? AdditionalPreferences { get; init; }
}

public sealed record StartResearchResponse(Guid RunId, ResearchRunStatus Status, string StatusUrl);

public sealed record ResearchRunDto
{
    public required Guid RunId { get; init; }
    public required ResearchRunStatus Status { get; init; }
    public required int Attempt { get; init; }
    public required bool IsTerminal { get; init; }
    public required bool CanRetry { get; init; }
    public required DiscoveryRequestDto Request { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public required IReadOnlyList<ProgressEventDto> Progress { get; init; }
    public required IReadOnlyList<VenueCandidateDto> Candidates { get; init; }
    /// <summary>Web agents working on this attempt: start page, live browser view while running, and steps taken.</summary>
    public required IReadOnlyList<AgentSessionDto> AgentSessions { get; init; }
    public ResearchErrorDto? Error { get; init; }
}

/// <summary>
/// One web agent's activity. <c>LiveViewUrl</c> is the provider's live browser stream (only while active);
/// <c>Status</c> is the provider's status (PENDING, RUNNING, COMPLETED, FAILED, CANCELLED);
/// <c>IsSimulated</c> is true for demo data, where no real website was visited.
/// </summary>
public sealed record AgentSessionDto(
    string ProviderRunId,
    string SourceName,
    string StartUrl,
    string? LiveViewUrl,
    string Status,
    bool IsActive,
    IReadOnlyList<AgentStepDto> Steps,
    DateTimeOffset UpdatedAtUtc,
    bool IsSimulated);

/// <summary>One agent step; <c>ScreenshotUrl</c> is an API path (relative to the API base) or null.</summary>
public sealed record AgentStepDto(string Id, string Action, DateTimeOffset? AtUtc, string? Duration, string? ScreenshotUrl);

public sealed record ProgressEventDto(int Attempt, string Stage, string Message, DateTimeOffset OccurredAtUtc);

public sealed record ResearchErrorDto(string Code, string Message);

/// <summary>A ranked venue. <c>null</c> values mean the information was not found and must be shown as unknown.</summary>
public sealed record VenueCandidateDto
{
    public required Guid Id { get; init; }
    public required int Rank { get; init; }
    public required double RankScore { get; init; }
    /// <summary>True when the venue's category is the requested mood (always true for "Any").</summary>
    public required bool MatchesMood { get; init; }
    public required string Name { get; init; }
    public required ActivityCategory Category { get; init; }
    public string? Address { get; init; }
    public string? Neighborhood { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? Description { get; init; }
    public required string SourceName { get; init; }
    public string? SourceUrl { get; init; }
    public string? OpeningHours { get; init; }
    public decimal? EstimatedCostPerPerson { get; init; }
    public string? Currency { get; init; }
    public int? SuggestedVisitMinutes { get; init; }
    public string? Evidence { get; init; }
    public required DateTimeOffset LastCheckedUtc { get; init; }
    public required VerificationStatus VerificationStatus { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public required IReadOnlyList<string> MissingFields { get; init; }
}

public sealed record DiscoveryOptionsDto(
    IReadOnlyList<CategoryOptionDto> Categories,
    IReadOnlyList<int> DurationPresetsMinutes,
    IReadOnlyList<decimal> BudgetPresets,
    string DefaultCurrency,
    IReadOnlyList<string> SupportedCurrencies,
    IReadOnlyList<string> SupportedCities,
    DiscoveryLimitsDto Limits);

/// <summary>
/// Suggestion cards for the user's area (<c>GET /api/discovery/curations</c>). <c>Area</c> is the neighbourhood,
/// or the city when none could be resolved; <c>MatchedBy</c> is City, LocationText or Coordinates;
/// <c>DistanceKm</c> is set only when matched by coordinates.
/// </summary>
public sealed record CurationsDto(
    string Area,
    string City,
    AreaMatch MatchedBy,
    double? DistanceKm,
    IReadOnlyList<CurationDto> Curations);

/// <summary>
/// One suggestion card. <c>Key</c> is stable (hidden-gems, food, outdoors, activities);
/// <c>LocationText</c> pre-fills the search form.
/// </summary>
public sealed record CurationDto(string Key, string Title, string Description, ActivityCategory Category, string LocationText);

public sealed record CategoryOptionDto(ActivityCategory Value, string Label, string Description);

public sealed record DiscoveryLimitsDto(
    int MinDurationMinutes,
    int MaxDurationMinutes,
    decimal MinBudgetPerPerson,
    decimal MaxBudgetPerPerson,
    int MaxLocationLength,
    int MaxPreferencesLength);
