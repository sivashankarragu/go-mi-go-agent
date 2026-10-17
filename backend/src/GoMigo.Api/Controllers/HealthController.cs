using System.Reflection;
using GoMigo.Application.Research;
using GoMigo.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace GoMigo.Api.Controllers;

[ApiController]
[Route("api/health")]
[Produces("application/json")]
public sealed class HealthController(IResearchRunRepository repository, IConfiguration configuration) : ControllerBase
{
    public sealed record HealthResponse(string Status, string Service, string Version, string ResearchProvider, string Persistence, string Database);

    /// <summary>Liveness plus a database connectivity check. Returns 200 even when degraded.</summary>
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public async Task<HealthResponse> Get(CancellationToken cancellationToken)
    {
        var dbOk = await repository.CanConnectAsync(cancellationToken);
        return new HealthResponse(
            Status: dbOk ? "Healthy" : "Degraded",
            Service: "GoMigo API",
            Version: Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            ResearchProvider: DependencyInjection.GetResearchProvider(configuration),
            Persistence: DependencyInjection.GetPersistenceProvider(configuration),
            Database: dbOk ? "Reachable" : "Unreachable");
    }
}
