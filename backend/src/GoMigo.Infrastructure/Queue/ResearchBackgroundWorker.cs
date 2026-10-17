using GoMigo.Application.Research;
using GoMigo.Domain.Research;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoMigo.Infrastructure.Queue;

/// <summary>Runs queued research in the background with bounded concurrency.</summary>
public sealed class ResearchBackgroundWorker(
    ChannelResearchQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<ResearchOptions> options,
    TimeProvider timeProvider,
    ILogger<ResearchBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverUnfinishedRunsAsync(stoppingToken);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, options.Value.MaxConcurrentRuns),
            CancellationToken = stoppingToken
        };

        try
        {
            await Parallel.ForEachAsync(queue.ReadAllAsync(stoppingToken), parallelOptions, async (runId, _) =>
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var processor = scope.ServiceProvider.GetRequiredService<ResearchRunProcessor>();
                    await processor.ProcessAsync(runId, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Research run {RunId} could not be processed", runId);
                }
            });
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    /// <summary>
    /// After a restart: runs left Running were interrupted and are marked Failed (retryable);
    /// runs left Pending are queued again.
    /// </summary>
    private async Task RecoverUnfinishedRunsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IResearchRunRepository>();

            foreach (var id in await repository.GetIdsByStatusAsync(ResearchRunStatus.Running, cancellationToken))
            {
                var run = await repository.GetAsync(id, cancellationToken);
                if (run is { Status: ResearchRunStatus.Running })
                {
                    run.Fail(new ResearchError("Interrupted", "Research was interrupted because the server restarted. Please retry."), timeProvider.GetUtcNow());
                    await repository.SaveAsync(run, cancellationToken);
                }
            }

            var pending = await repository.GetIdsByStatusAsync(ResearchRunStatus.Pending, cancellationToken);
            foreach (var id in pending)
            {
                await queue.EnqueueAsync(id, cancellationToken);
            }
            if (pending.Count > 0)
            {
                logger.LogInformation("Re-queued {Count} pending research runs", pending.Count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not recover unfinished research runs; continuing");
        }
    }
}
