using GoMigo.Application.Discovery.Contracts;
using GoMigo.Domain.Discovery;
using GoMigo.Domain.Research;

namespace GoMigo.Application.Discovery;

public static class ResearchRunMapper
{
    public static ResearchRunDto ToDto(ResearchRun run) => new()
    {
        RunId = run.Id,
        Status = run.Status,
        Attempt = run.Attempt,
        IsTerminal = run.IsTerminal,
        CanRetry = run.CanRetry,
        Request = ToDto(run.Request),
        CreatedAtUtc = run.CreatedAtUtc,
        StartedAtUtc = run.StartedAtUtc,
        CompletedAtUtc = run.CompletedAtUtc,
        Progress = run.ProgressEvents
            .OrderBy(e => e.OccurredAtUtc)
            .Select(e => new ProgressEventDto(e.Attempt, e.Stage, e.Message, e.OccurredAtUtc))
            .ToList(),
        // Exact mood matches always come first; other venues follow as alternatives.
        Candidates = run.Candidates
            .Select(c => (Candidate: c, Matches: MatchesMood(c, run.Request)))
            .OrderByDescending(x => x.Matches)
            .ThenByDescending(x => x.Candidate.RankScore)
            .ThenBy(x => x.Candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Select((x, i) => ToDto(x.Candidate, i + 1, x.Matches))
            .ToList(),
        AgentSessions = run.AgentSessions
            .Select(s => new AgentSessionDto(
                s.ProviderRunId, s.SourceName, s.StartUrl, s.IsActive ? s.LiveViewUrl : null, s.Status, s.IsActive,
                s.Steps.Select(step => new AgentStepDto(step.Id, step.Action, step.AtUtc, step.Duration,
                    step.HasScreenshot ? ScreenshotPath(run.Id, s.ProviderRunId, step.Id) : null)).ToList(),
                s.UpdatedAtUtc,
                s.IsSimulated))
            .ToList(),
        Error = run.Error is null ? null : new ResearchErrorDto(run.Error.Code, run.Error.Message)
    };

    public static string ScreenshotPath(Guid runId, string providerRunId, string stepId) =>
        $"/api/discovery/research/{runId}/agents/{Uri.EscapeDataString(providerRunId)}/steps/{Uri.EscapeDataString(stepId)}/screenshot";

    public static DiscoveryRequestDto ToDto(DiscoveryRequest request) => new()
    {
        LocationText = request.LocationText,
        Latitude = request.Latitude,
        Longitude = request.Longitude,
        DurationMinutes = request.DurationMinutes,
        BudgetPerPerson = request.BudgetPerPerson,
        Currency = request.Currency,
        Category = request.Category,
        StartTime = request.StartTime,
        AdditionalPreferences = request.AdditionalPreferences
    };

    public static bool MatchesMood(VenueCandidate candidate, DiscoveryRequest request) =>
        request.Category == ActivityCategory.Any || candidate.Category == request.Category;

    public static VenueCandidateDto ToDto(VenueCandidate c, int rank, bool matchesMood) => new()
    {
        Id = c.Id,
        Rank = rank,
        RankScore = c.RankScore,
        MatchesMood = matchesMood,
        Name = c.Name,
        Category = c.Category,
        Address = c.Address,
        Neighborhood = c.Neighborhood,
        Latitude = c.Latitude,
        Longitude = c.Longitude,
        Description = c.Description,
        SourceName = c.SourceName,
        SourceUrl = c.SourceUrl,
        OpeningHours = c.OpeningHours,
        EstimatedCostPerPerson = c.EstimatedCostPerPerson,
        Currency = c.Currency,
        SuggestedVisitMinutes = c.SuggestedVisitMinutes,
        Evidence = c.Evidence,
        LastCheckedUtc = c.LastCheckedUtc,
        VerificationStatus = c.VerificationStatus,
        Warnings = c.Warnings,
        MissingFields = c.MissingFields
    };
}
