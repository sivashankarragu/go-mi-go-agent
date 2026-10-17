using GoMigo.Domain.Research;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoMigo.Application.Research;

/// <summary>
/// Executes one research run: provider → normalization → ranking, and records the outcome on the run.
/// Only user-safe error messages are stored; details go to the log.
/// </summary>
public sealed class ResearchRunProcessor(
    IResearchRunRepository repository,
    IWebResearchProvider provider,
    IOptions<ResearchOptions> options,
    TimeProvider timeProvider,
    ILogger<ResearchRunProcessor> logger)
{
    private const int MaxProgressMessageLength = 300;

    public async Task ProcessAsync(Guid runId, CancellationToken stoppingToken)
    {
        var run = await repository.GetAsync(runId, stoppingToken);
        if (run is null || run.Status != ResearchRunStatus.Pending)
        {
            logger.LogWarning("Skipping research run {RunId}: not found or not pending ({Status})", runId, run?.Status);
            return;
        }

        using var scope = logger.BeginScope(new Dictionary<string, object> { ["RunId"] = runId });
        run.Start(Now);
        await repository.SaveAsync(run, stoppingToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.Value.RunTimeoutSeconds)));
        var ct = timeoutCts.Token;

        try
        {
            run.AddProgress("researching", $"Researching places with {provider.Name}.", Now);
            await repository.SaveAsync(run, ct);

            var result = await provider.ResearchAsync(run.Request, async (progress, token) =>
            {
                if (progress.Agent is not null) run.RecordAgentSession(progress.Agent);
                if (!string.IsNullOrWhiteSpace(progress.Message)) run.AddProgress(progress.Stage, Truncate(progress.Message), Now);
                await repository.SaveAsync(run, token);
            }, ct);

            var accepted = new List<Domain.Discovery.VenueCandidate>();
            var rejected = 0;
            foreach (var raw in result.Venues)
            {
                var normalized = VenueNormalizer.Normalize(raw, run.Request, Now);
                if (normalized.Candidate is not null) accepted.Add(normalized.Candidate);
                else rejected++;
            }
            run.AddProgress("normalizing",
                rejected == 0
                    ? $"Extracted {accepted.Count} venues."
                    : $"Extracted {accepted.Count} venues; skipped {rejected} incomplete results.",
                Now);

            var ranked = CandidateRanker.Rank(accepted, run.Request, options.Value.MaxCandidates);
            run.AddProgress("ranking", $"Ranked {ranked.Count} venues against your preferences.", Now);

            if (result.HasSourceErrors && ranked.Count == 0)
            {
                run.Fail(new ResearchError("NoResults", "None of the sources could be researched. Please retry."), Now);
            }
            else
            {
                var partialReason = result.HasSourceErrors
                    ? new ResearchError("PartialResults", Truncate("Some sources could not be researched: " + string.Join("; ", result.SourceErrors)))
                    : null;
                run.Complete(ranked, result.HasSourceErrors, partialReason, Now);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning("Research run {RunId} interrupted by shutdown", runId);
            run.Fail(new ResearchError("Interrupted", "Research was interrupted because the server stopped. Please retry."), Now);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Research run {RunId} timed out after {Seconds}s", runId, options.Value.RunTimeoutSeconds);
            run.Fail(new ResearchError("Timeout", "Research took too long and was stopped. Please retry."), Now);
        }
        catch (ResearchProviderException ex)
        {
            logger.LogWarning(ex, "Research provider failed for run {RunId} with {Code}", runId, ex.Code);
            run.Fail(new ResearchError(ex.Code, ex.Message), Now);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected failure in research run {RunId}", runId);
            run.Fail(new ResearchError("UnexpectedError", "Research failed due to an unexpected error. Please retry."), Now);
        }

        // Always record the final state, even during shutdown.
        await repository.SaveAsync(run, CancellationToken.None);
        logger.LogInformation("Research run {RunId} finished with {Status} and {Count} candidates", runId, run.Status, run.Candidates.Count);
    }

    private DateTimeOffset Now => timeProvider.GetUtcNow();

    private static string Truncate(string message) =>
        message.Length <= MaxProgressMessageLength ? message : message[..(MaxProgressMessageLength - 1)] + "…";
}
