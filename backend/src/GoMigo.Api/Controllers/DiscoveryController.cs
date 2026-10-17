using GoMigo.Application.Discovery;
using GoMigo.Application.Discovery.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace GoMigo.Api.Controllers;

[ApiController]
[Route("api/discovery")]
[Produces("application/json")]
public sealed class DiscoveryController(DiscoveryService discoveryService) : ControllerBase
{
    /// <summary>Activity categories, presets and limits for building the discovery form.</summary>
    [HttpGet("options")]
    [ProducesResponseType<DiscoveryOptionsDto>(StatusCodes.Status200OK)]
    public DiscoveryOptionsDto GetOptions() => discoveryService.GetOptions();

    /// <summary>
    /// Suggestion cards for the user's area: a neighbourhood named in <paramref name="location"/>,
    /// else the nearest known neighbourhood to the coordinates, else city-wide. Static; no web research.
    /// </summary>
    [HttpGet("curations")]
    [ProducesResponseType<CurationsDto>(StatusCodes.Status200OK)]
    public CurationsDto GetCurations([FromQuery] string? location, [FromQuery] double? latitude, [FromQuery] double? longitude) =>
        discoveryService.GetCurations(location, latitude, longitude);

    /// <summary>Starts a research run. Poll the returned <c>statusUrl</c> for progress and results.</summary>
    [HttpPost("research")]
    [Consumes("application/json")]
    [ProducesResponseType<StartResearchResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartResearch([FromBody] DiscoveryRequestDto request, CancellationToken cancellationToken)
    {
        var run = await discoveryService.StartResearchAsync(request, cancellationToken);
        return Accepted(StatusUrl(run.RunId), new StartResearchResponse(run.RunId, run.Status, StatusUrl(run.RunId)));
    }

    /// <summary>Current status, progress events and ranked venues of a research run.</summary>
    [HttpGet("research/{runId:guid}")]
    [ProducesResponseType<ResearchRunDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ResearchRunDto> GetResearch(Guid runId, CancellationToken cancellationToken) =>
        discoveryService.GetResearchAsync(runId, cancellationToken);

    /// <summary>
    /// Screenshot of one web-agent step in this run. Proxied because the research provider requires
    /// GoMigo's API key, which never reaches the browser.
    /// </summary>
    [HttpGet("research/{runId:guid}/agents/{providerRunId}/steps/{stepId}/screenshot")]
    [Produces("image/jpeg", "image/png", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> GetAgentStepScreenshot(Guid runId, string providerRunId, string stepId, CancellationToken cancellationToken)
    {
        var shot = await discoveryService.GetAgentScreenshotAsync(runId, providerRunId, stepId, cancellationToken);
        return File(shot.Content, shot.ContentType);
    }

    /// <summary>Re-queues a Failed or PartiallyCompleted run.</summary>
    [HttpPost("research/{runId:guid}/retry")]
    [ProducesResponseType<StartResearchResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RetryResearch(Guid runId, CancellationToken cancellationToken)
    {
        var run = await discoveryService.RetryResearchAsync(runId, cancellationToken);
        return Accepted(StatusUrl(run.RunId), new StartResearchResponse(run.RunId, run.Status, StatusUrl(run.RunId)));
    }

    private static string StatusUrl(Guid runId) => $"/api/discovery/research/{runId}";
}
