using System.Collections.Concurrent;
using GoMigo.Application.Research;
using GoMigo.Domain.Research;

namespace GoMigo.Infrastructure.Persistence;

/// <summary>
/// Non-durable repository for tests and database-free local runs. Stores copies so callers
/// never share a mutable <see cref="ResearchRun"/> instance across threads.
/// </summary>
public sealed class InMemoryResearchRunRepository : IResearchRunRepository
{
    private readonly ConcurrentDictionary<Guid, ResearchRun> _runs = new();

    public Task AddAsync(ResearchRun run, CancellationToken cancellationToken)
    {
        if (!_runs.TryAdd(run.Id, Copy(run)))
        {
            throw new InvalidOperationException($"Research run {run.Id} already exists.");
        }
        return Task.CompletedTask;
    }

    public Task<ResearchRun?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_runs.TryGetValue(id, out var run) ? Copy(run) : null);

    public Task SaveAsync(ResearchRun run, CancellationToken cancellationToken)
    {
        _runs[run.Id] = Copy(run);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> GetIdsByStatusAsync(ResearchRunStatus status, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(_runs.Values.Where(r => r.Status == status).Select(r => r.Id).ToList());

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    private static ResearchRun Copy(ResearchRun run) => ResearchRun.Restore(
        run.Id, run.Request, run.Status, run.Attempt, run.CreatedAtUtc, run.StartedAtUtc, run.CompletedAtUtc,
        run.Error, run.ProgressEvents.ToList(), run.Candidates.ToList(), run.AgentSessions.ToList());
}
