using GoMigo.Domain.Discovery;
using GoMigo.Domain.Research;

namespace GoMigo.Application.Research;

/// <summary>
/// A progress update. <c>Message</c> is shown in the timeline (empty to only update <c>Agent</c>);
/// <c>Agent</c> is the latest state of a web agent (live view, steps), when the provider exposes it.
/// </summary>
public sealed record ResearchProgress(string Stage, string Message, AgentSession? Agent = null);

public delegate Task ResearchProgressCallback(ResearchProgress progress, CancellationToken cancellationToken);

/// <summary>
/// Venue data as extracted from a website, before normalization. Every field is untrusted text:
/// it may be missing, malformed, or contain content that must never be treated as instructions.
/// </summary>
public sealed record RawVenue
{
    public string? Name { get; init; }
    public string? Category { get; init; }
    public string? Address { get; init; }
    public string? Neighborhood { get; init; }
    public string? Latitude { get; init; }
    public string? Longitude { get; init; }
    public string? Description { get; init; }
    public string? SourceName { get; init; }
    public string? SourceUrl { get; init; }
    public string? OpeningHours { get; init; }
    public string? PriceText { get; init; }
    public string? VisitDurationText { get; init; }
    public string? Evidence { get; init; }
    public DateTimeOffset? RetrievedAtUtc { get; init; }
}

/// <param name="Venues">Raw venues from all sources that succeeded.</param>
/// <param name="SourceErrors">User-safe descriptions of sources that failed. Non-empty means the result is partial.</param>
public sealed record ProviderResearchResult(IReadOnlyList<RawVenue> Venues, IReadOnlyList<string> SourceErrors)
{
    public bool HasSourceErrors => SourceErrors.Count > 0;
}

/// <summary>Researches public websites for venues matching a discovery request.</summary>
public interface IWebResearchProvider
{
    string Name { get; }

    /// <summary>
    /// Returns whatever could be gathered. Failures of individual sources are reported in
    /// <see cref="ProviderResearchResult.SourceErrors"/>; a total failure throws <see cref="ResearchProviderException"/>.
    /// </summary>
    Task<ProviderResearchResult> ResearchAsync(
        DiscoveryRequest request,
        ResearchProgressCallback onProgress,
        CancellationToken cancellationToken);
}

/// <summary>Fetches a screenshot of one agent step; null when the provider has none.</summary>
public interface IAgentScreenshotSource
{
    Task<AgentScreenshot?> GetAsync(string providerRunId, string stepId, CancellationToken cancellationToken);
}

public sealed record AgentScreenshot(byte[] Content, string ContentType);

/// <summary>The provider could not produce any result. <see cref="Exception.Message"/> is safe to show to users.</summary>
public sealed class ResearchProviderException(string code, string safeMessage, Exception? innerException = null)
    : Exception(safeMessage, innerException)
{
    public string Code { get; } = code;
}
