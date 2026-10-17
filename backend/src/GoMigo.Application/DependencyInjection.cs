using GoMigo.Application.Discovery;
using GoMigo.Application.Places;
using GoMigo.Application.Research;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GoMigo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddGoMigoApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<DiscoveryService>();
        services.AddScoped<PlacesService>();
        services.AddScoped<ResearchRunProcessor>();
        return services;
    }
}
