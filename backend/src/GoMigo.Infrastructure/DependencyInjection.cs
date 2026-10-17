using GoMigo.Application.Places;
using GoMigo.Application.Research;
using GoMigo.Infrastructure.Places;
using GoMigo.Infrastructure.Persistence;
using GoMigo.Infrastructure.Persistence.ResearchRuns;
using GoMigo.Infrastructure.Queue;
using GoMigo.Infrastructure.Research.Mock;
using GoMigo.Infrastructure.Research.TinyFish;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GoMigo.Infrastructure;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    /// <summary><c>Postgres</c> (default) or <c>InMemory</c> (no database; data lost on restart).</summary>
    public string Provider { get; set; } = "Postgres";
}

public static class DependencyInjection
{
    public const string ConnectionStringName = "GoMigo";

    public static IServiceCollection AddGoMigoInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<ResearchOptions>().Bind(configuration.GetSection(ResearchOptions.SectionName));

        AddPersistence(services, configuration);
        AddResearchProvider(services, configuration);
        AddPlaces(services, configuration);

        services.AddSingleton<ChannelResearchQueue>();
        services.AddSingleton<IResearchQueue>(sp => sp.GetRequiredService<ChannelResearchQueue>());
        services.AddHostedService<ResearchBackgroundWorker>();
        return services;
    }

    public static string GetPersistenceProvider(IConfiguration configuration) =>
        configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>()?.Provider ?? "Postgres";

    public static string GetResearchProvider(IConfiguration configuration) =>
        configuration.GetSection(ResearchOptions.SectionName).Get<ResearchOptions>()?.Provider ?? "Mock";

    private static void AddPlaces(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddOptions<PhotonOptions>().Bind(configuration.GetSection(PhotonOptions.SectionName));
        services.AddHttpClient<IPlaceSearchProvider, PhotonPlaceSearchProvider>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<PhotonOptions>>().Value;
            http.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(Math.Max(1, o.TimeoutSeconds));
            // Photon's fair-use policy asks clients to identify themselves.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("GoMigo/1.0 (hackathon; local discovery)");
        });
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var provider = GetPersistenceProvider(configuration);
        if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IResearchRunRepository, InMemoryResearchRunRepository>();
            return;
        }
        if (!provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unknown Persistence:Provider '{provider}'. Use 'Postgres' or 'InMemory'.");
        }

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'ConnectionStrings:{ConnectionStringName}' is not set. " +
                "Set it with 'dotnet user-secrets' or the ConnectionStrings__GoMigo environment variable, " +
                "or set Persistence:Provider=InMemory to run without a database.");
        }

        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IResearchRunRepository, PostgresResearchRunRepository>();
    }

    private static void AddResearchProvider(IServiceCollection services, IConfiguration configuration)
    {
        var provider = GetResearchProvider(configuration);
        if (provider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddOptions<MockResearchOptions>().Bind(configuration.GetSection(MockResearchOptions.SectionName));
            services.AddSingleton<IWebResearchProvider, MockResearchProvider>();
            services.AddSingleton<IAgentScreenshotSource, NoAgentScreenshots>();
            return;
        }
        if (!provider.Equals("TinyFish", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unknown Research:Provider '{provider}'. Use 'Mock' or 'TinyFish'.");
        }

        services.AddOptions<TinyFishOptions>()
            .Bind(configuration.GetSection(TinyFishOptions.SectionName))
            .PostConfigure(o =>
            {
                if (string.IsNullOrWhiteSpace(o.ApiKey))
                {
                    o.ApiKey = Environment.GetEnvironmentVariable(TinyFishOptions.ApiKeyEnvironmentVariable);
                }
            })
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey),
                $"TinyFish API key is missing. Set TinyFish:ApiKey via user-secrets or the {TinyFishOptions.ApiKeyEnvironmentVariable} environment variable.")
            .Validate(o => o.Sources.Any(s => !string.IsNullOrWhiteSpace(s.Url)), "TinyFish:Sources must list at least one source URL.")
            .ValidateOnStart();

        services.AddHttpClient<TinyFishClient>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<TinyFishOptions>>().Value;
            http.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(Math.Max(5, o.HttpTimeoutSeconds));
            http.DefaultRequestHeaders.Add("X-API-Key", o.ApiKey);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("GoMigo/1.0");
        });
        services.AddHttpClient<TinyFishSearchClient>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<TinyFishOptions>>().Value;
            http.BaseAddress = new Uri(o.SearchBaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(15);
            http.DefaultRequestHeaders.Add("X-API-Key", o.ApiKey);
        });
        services.AddHttpClient<RobotsTxtPolicy>(http =>
        {
            http.Timeout = TimeSpan.FromSeconds(6);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("GoMigo/1.0 (+robots check)");
        });
        services.AddScoped<IWebResearchProvider, TinyFishResearchProvider>();
        services.AddScoped<IAgentScreenshotSource, TinyFishScreenshotSource>();
    }
}
