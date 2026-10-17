namespace GoMigo.Domain.Research;

/// <summary>
/// What one web agent did while researching a source: which page it started on, a live view URL while it runs,
/// and the steps it took. Lets users see the research happening instead of a black box.
/// <c>IsSimulated</c> marks demo sessions from the mock provider, where no real website is visited.
/// </summary>
public sealed record AgentSession(
    string ProviderRunId,
    string SourceName,
    string StartUrl,
    string? LiveViewUrl,
    string Status,
    IReadOnlyList<AgentStep> Steps,
    DateTimeOffset UpdatedAtUtc,
    bool IsSimulated = false)
{
    public bool IsActive => Status is "PENDING" or "RUNNING";
}

/// <param name="Action">The agent's own description, e.g. "Navigate to Bengaluru/East Wikivoyage page".</param>
public sealed record AgentStep(string Id, string Action, DateTimeOffset? AtUtc, string? Duration, bool HasScreenshot);
