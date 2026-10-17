using GoMigo.Domain.Discovery;

namespace GoMigo.Domain.Research;

public enum ResearchRunStatus
{
    Pending,
    Running,
    Completed,
    PartiallyCompleted,
    Failed
}

/// <summary>A user-safe error description. Never contains stack traces, secrets or raw provider payloads.</summary>
public sealed record ResearchError(string Code, string Message);

public sealed record ResearchProgressEvent(Guid Id, int Attempt, string Stage, string Message, DateTimeOffset OccurredAtUtc);

/// <summary>
/// One execution of the discovery workflow for a request. Status changes only through the methods below,
/// which enforce the allowed transitions:
/// Pending → Running → (Completed | PartiallyCompleted | Failed), and Failed/PartiallyCompleted → Pending on retry.
/// </summary>
public sealed class ResearchRun
{
    private readonly List<ResearchProgressEvent> _progressEvents;
    private List<VenueCandidate> _candidates;
    private List<AgentSession> _agentSessions;

    private ResearchRun(
        Guid id,
        DiscoveryRequest request,
        ResearchRunStatus status,
        int attempt,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? completedAtUtc,
        ResearchError? error,
        IEnumerable<ResearchProgressEvent> progressEvents,
        IEnumerable<VenueCandidate> candidates,
        IEnumerable<AgentSession>? agentSessions = null)
    {
        Id = id;
        Request = request;
        Status = status;
        Attempt = attempt;
        CreatedAtUtc = createdAtUtc;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        Error = error;
        _progressEvents = progressEvents.ToList();
        _candidates = candidates.ToList();
        _agentSessions = agentSessions?.ToList() ?? [];
    }

    public Guid Id { get; }
    public DiscoveryRequest Request { get; }
    public ResearchRunStatus Status { get; private set; }

    /// <summary>Number of times the run has been started (1 for the first execution).</summary>
    public int Attempt { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public ResearchError? Error { get; private set; }
    public IReadOnlyList<ResearchProgressEvent> ProgressEvents => _progressEvents;
    public IReadOnlyList<VenueCandidate> Candidates => _candidates;

    /// <summary>Web agents used by the current attempt (live view, steps), newest information last.</summary>
    public IReadOnlyList<AgentSession> AgentSessions => _agentSessions;

    public bool IsTerminal => Status is ResearchRunStatus.Completed or ResearchRunStatus.PartiallyCompleted or ResearchRunStatus.Failed;
    public bool CanRetry => Status is ResearchRunStatus.Failed or ResearchRunStatus.PartiallyCompleted;

    public static ResearchRun Create(DiscoveryRequest request, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        var run = new ResearchRun(Guid.NewGuid(), request, ResearchRunStatus.Pending, 0, nowUtc, null, null, null, [], []);
        run.AddProgress("queued", "Research request received.", nowUtc);
        return run;
    }

    /// <summary>Rehydrates a run from storage without re-validating transitions.</summary>
    public static ResearchRun Restore(
        Guid id,
        DiscoveryRequest request,
        ResearchRunStatus status,
        int attempt,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? completedAtUtc,
        ResearchError? error,
        IEnumerable<ResearchProgressEvent> progressEvents,
        IEnumerable<VenueCandidate> candidates,
        IEnumerable<AgentSession>? agentSessions = null) =>
        new(id, request, status, attempt, createdAtUtc, startedAtUtc, completedAtUtc, error, progressEvents, candidates, agentSessions);

    public void Start(DateTimeOffset nowUtc)
    {
        EnsureStatus(nameof(Start), ResearchRunStatus.Pending);
        Status = ResearchRunStatus.Running;
        Attempt++;
        StartedAtUtc = nowUtc;
        CompletedAtUtc = null;
        AddProgress("started", Attempt == 1 ? "Research started." : $"Research restarted (attempt {Attempt}).", nowUtc);
    }

    /// <summary>Adds or replaces the session with the same provider run id.</summary>
    public void RecordAgentSession(AgentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var index = _agentSessions.FindIndex(s => s.ProviderRunId == session.ProviderRunId);
        if (index >= 0) _agentSessions[index] = session;
        else _agentSessions.Add(session);
    }

    public void AddProgress(string stage, string message, DateTimeOffset nowUtc) =>
        _progressEvents.Add(new ResearchProgressEvent(Guid.NewGuid(), Attempt, stage, message, nowUtc));

    /// <param name="isPartial">True when some sources failed but usable results were still produced.</param>
    /// <param name="partialReason">User-safe explanation of what was missed; required when partial.</param>
    public void Complete(IEnumerable<VenueCandidate> candidates, bool isPartial, ResearchError? partialReason, DateTimeOffset nowUtc)
    {
        EnsureStatus(nameof(Complete), ResearchRunStatus.Running);
        _candidates = candidates.ToList();
        Status = isPartial ? ResearchRunStatus.PartiallyCompleted : ResearchRunStatus.Completed;
        Error = isPartial ? partialReason ?? new ResearchError("PartialResults", "Some sources could not be researched.") : null;
        CompletedAtUtc = nowUtc;
        AddProgress(
            "completed",
            isPartial
                ? $"Research finished with partial results ({_candidates.Count} venues)."
                : $"Research finished ({_candidates.Count} venues).",
            nowUtc);
    }

    public void Fail(ResearchError error, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(error);
        EnsureStatus(nameof(Fail), ResearchRunStatus.Pending, ResearchRunStatus.Running);
        Status = ResearchRunStatus.Failed;
        Error = error;
        CompletedAtUtc = nowUtc;
        AddProgress("failed", error.Message, nowUtc);
    }

    /// <summary>Puts a failed or partial run back in the queue. Previous candidates are cleared from the run.</summary>
    public void ResetForRetry(DateTimeOffset nowUtc)
    {
        EnsureStatus(nameof(ResetForRetry), ResearchRunStatus.Failed, ResearchRunStatus.PartiallyCompleted);
        Status = ResearchRunStatus.Pending;
        Error = null;
        CompletedAtUtc = null;
        _candidates = [];
        _agentSessions = [];
        AddProgress("queued", "Retry requested.", nowUtc);
    }

    private void EnsureStatus(string operation, params ResearchRunStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidStatusTransitionException(Id, Status, operation);
        }
    }
}

public sealed class InvalidStatusTransitionException(Guid runId, ResearchRunStatus current, string operation)
    : InvalidOperationException($"Cannot {operation} research run {runId} while it is {current}.")
{
    public ResearchRunStatus Current { get; } = current;
}
