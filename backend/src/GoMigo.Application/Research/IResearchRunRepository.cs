using GoMigo.Domain.Research;

namespace GoMigo.Application.Research;

public interface IResearchRunRepository
{
    Task AddAsync(ResearchRun run, CancellationToken cancellationToken);

    /// <summary>Returns the run with the progress events and candidates of its current attempt.</summary>
    Task<ResearchRun?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Persists status changes, new progress events and the current attempt's candidates.</summary>
    Task SaveAsync(ResearchRun run, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetIdsByStatusAsync(ResearchRunStatus status, CancellationToken cancellationToken);

    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}

/// <summary>Schedules research runs for background execution.</summary>
public interface IResearchQueue
{
    ValueTask EnqueueAsync(Guid runId, CancellationToken cancellationToken);
}
