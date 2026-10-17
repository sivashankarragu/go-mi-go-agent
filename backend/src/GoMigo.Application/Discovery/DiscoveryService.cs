using GoMigo.Application.Common;
using GoMigo.Application.Discovery.Contracts;
using GoMigo.Application.Research;
using GoMigo.Domain.Discovery;
using GoMigo.Domain.Research;
using Microsoft.Extensions.Logging;

namespace GoMigo.Application.Discovery;

/// <summary>Use cases behind the discovery endpoints.</summary>
public sealed class DiscoveryService(
    IResearchRunRepository repository,
    IResearchQueue queue,
    IAgentScreenshotSource screenshots,
    TimeProvider timeProvider,
    ILogger<DiscoveryService> logger)
{
    public async Task<ResearchRunDto> StartResearchAsync(DiscoveryRequestDto dto, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var request = DiscoveryRequestValidator.ValidateAndMap(dto, now);
        var run = ResearchRun.Create(request, now);

        await repository.AddAsync(run, cancellationToken);
        await queue.EnqueueAsync(run.Id, cancellationToken);
        logger.LogInformation("Research run {RunId} queued for {Location}", run.Id, request.LocationText);
        return ResearchRunMapper.ToDto(run);
    }

    public async Task<ResearchRunDto> GetResearchAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await repository.GetAsync(runId, cancellationToken)
                  ?? throw new NotFoundException($"Research run {runId} was not found.");
        return ResearchRunMapper.ToDto(run);
    }

    /// <summary>Screenshot of a step that belongs to this run; anything else is reported as not found.</summary>
    public async Task<AgentScreenshot> GetAgentScreenshotAsync(Guid runId, string providerRunId, string stepId, CancellationToken cancellationToken)
    {
        var run = await repository.GetAsync(runId, cancellationToken);
        var known = run?.AgentSessions.Any(s => s.ProviderRunId == providerRunId && s.Steps.Any(st => st.Id == stepId && st.HasScreenshot)) ?? false;
        if (!known)
        {
            throw new NotFoundException("Screenshot not found.");
        }
        return await screenshots.GetAsync(providerRunId, stepId, cancellationToken)
               ?? throw new NotFoundException("Screenshot not available.");
    }

    public async Task<ResearchRunDto> RetryResearchAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await repository.GetAsync(runId, cancellationToken)
                  ?? throw new NotFoundException($"Research run {runId} was not found.");
        if (!run.CanRetry)
        {
            throw new ConflictException($"Research run is {run.Status}; only failed or partially completed runs can be retried.");
        }

        run.ResetForRetry(timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken);
        await queue.EnqueueAsync(run.Id, cancellationToken);
        logger.LogInformation("Research run {RunId} re-queued", run.Id);
        return ResearchRunMapper.ToDto(run);
    }

    public CurationsDto GetCurations(string? locationText, double? latitude, double? longitude)
    {
        var resolved = BengaluruAreas.Resolve(locationText, latitude, longitude);
        var area = resolved.Area?.Name ?? BengaluruAreas.City;
        var place = resolved.Area is null ? BengaluruAreas.City : $"{area}, {BengaluruAreas.City}";
        var near = resolved.Area is null ? $"across {BengaluruAreas.City}" : $"around {area}";

        return new CurationsDto(area, BengaluruAreas.City, resolved.Match, resolved.DistanceKm,
        [
            new("hidden-gems", "Hidden Gems", $"Galleries, heritage spots, bookstores and quiet corners {near}.", ActivityCategory.Culture, place),
            new("food", "Cafés & Food", $"Cafés, bakeries and local favourites {near}.", ActivityCategory.Food, place),
            new("outdoors", "Outdoor Escapes", $"Parks, lakes and green walks {near}.", ActivityCategory.Outdoors, place),
            new("activities", "Activities & Events", $"Games, comedy, workshops and things to do {near}.", ActivityCategory.Fun, place)
        ]);
    }

    public DiscoveryOptionsDto GetOptions() => new(
        Categories:
        [
            new(ActivityCategory.Any, "Surprise me", "Anything worth doing nearby."),
            new(ActivityCategory.Fun, "Fun", "Games, arcades, bowling, comedy and other activities."),
            new(ActivityCategory.Food, "Food & cafés", "Restaurants, cafés, bakeries and desserts."),
            new(ActivityCategory.Culture, "Culture", "Museums, galleries, theatre and heritage spots."),
            new(ActivityCategory.Outdoors, "Outdoors", "Parks, lakes, gardens and short walks."),
            new(ActivityCategory.Nightlife, "Nightlife", "Bars, pubs, breweries and live music."),
            new(ActivityCategory.Shopping, "Shopping", "Markets, malls and local shops."),
            new(ActivityCategory.Relaxation, "Relaxation", "Spas, wellness and quiet places.")
        ],
        DurationPresetsMinutes: [60, 120, 180, 240, 360],
        BudgetPresets: [300m, 500m, 700m, 1000m, 2000m],
        DefaultCurrency: Money.DefaultCurrency,
        SupportedCurrencies: DiscoveryLimits.SupportedCurrencies,
        SupportedCities: DiscoveryLimits.SupportedCities,
        Limits: new DiscoveryLimitsDto(
            DiscoveryLimits.MinDurationMinutes,
            DiscoveryLimits.MaxDurationMinutes,
            DiscoveryLimits.MinBudgetPerPerson,
            DiscoveryLimits.MaxBudgetPerPerson,
            DiscoveryLimits.MaxLocationLength,
            DiscoveryLimits.MaxPreferencesLength));
}
