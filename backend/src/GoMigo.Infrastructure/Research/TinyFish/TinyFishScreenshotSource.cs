using GoMigo.Application.Research;

namespace GoMigo.Infrastructure.Research.TinyFish;

/// <summary>Step screenshots need the API key, so the browser gets them through GoMigo's API instead of from TinyFish.</summary>
public sealed class TinyFishScreenshotSource(TinyFishClient client) : IAgentScreenshotSource
{
    public async Task<AgentScreenshot?> GetAsync(string providerRunId, string stepId, CancellationToken cancellationToken) =>
        await client.GetStepScreenshotAsync(providerRunId, stepId, cancellationToken) is { } shot
            ? new AgentScreenshot(shot.Content, shot.ContentType)
            : null;
}

/// <summary>Providers without screenshots (the mock).</summary>
public sealed class NoAgentScreenshots : IAgentScreenshotSource
{
    public Task<AgentScreenshot?> GetAsync(string providerRunId, string stepId, CancellationToken cancellationToken) =>
        Task.FromResult<AgentScreenshot?>(null);
}
