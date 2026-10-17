namespace GoMigo.Infrastructure.Research.TinyFish;

public sealed class TinyFishOptions
{
    public const string SectionName = "TinyFish";
    public const string ApiKeyEnvironmentVariable = "TINYFISH_API_KEY";

    public string BaseUrl { get; set; } = "https://agent.tinyfish.ai";

    /// <summary>Set via user-secrets or the TINYFISH_API_KEY / TinyFish__ApiKey environment variable. Never commit it.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Timeout for a single HTTP call to TinyFish (not the whole agent run).</summary>
    public int HttpTimeoutSeconds { get; set; } = 30;

    public int InitialPollIntervalSeconds { get; set; } = 5;
    public int MaxPollIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Custom step limit; only sent when set. TinyFish rejects it (403) unless the account is in their beta program.
    /// </summary>
    public int? MaxStepsPerSource { get; set; }

    /// <summary>Wall-clock cap per agent run; the main lever for limiting credit use.</summary>
    public int? MaxDurationSecondsPerSource { get; set; } = 180;
    public int MaxVenuesPerSource { get; set; } = 8;

    /// <summary>
    /// Find pages with TinyFish Search (free daily allowance) for the user's area, mood and budget,
    /// instead of always reading the configured <see cref="Sources"/>. Those remain the fallback.
    /// </summary>
    public bool UseWebSearch { get; set; } = true;
    public string SearchBaseUrl { get; set; } = "https://api.search.tinyfish.ai";
    public string SearchCountry { get; set; } = "IN";
    public string SearchLanguage { get; set; } = "en";

    /// <summary>
    /// Sites never sent to the agent: social media (login walls) and sites known to block automated access.
    /// Matched against the host and its parent domains. robots.txt is checked for everything else.
    /// </summary>
    public List<string> ExcludedDomains { get; set; } =
    [
        "instagram.com", "facebook.com", "x.com", "twitter.com", "youtube.com", "linkedin.com", "reddit.com",
        "pinterest.com", "quora.com", "tiktok.com", "justdial.com", "zomato.com", "swiggy.com", "tripadvisor.in",
        "tripadvisor.com", "google.com", "maps.app.goo.gl"
    ];

    /// <summary>Upper bound on agent runs per search (each run costs TinyFish credits).</summary>
    public int MaxSourcesPerRun { get; set; } = 2;

    /// <summary>
    /// Public pages the agent starts from. <c>Url</c> may contain <c>{location}</c> and <c>{category}</c>
    /// placeholders (URL-encoded on use). Only list sites whose terms allow automated access.
    /// </summary>
    public List<TinyFishSource> Sources { get; set; } = [];
}

public sealed class TinyFishSource
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    /// <summary>Neighbourhood keywords; the source is used when the request location mentions one of them.</summary>
    public List<string> Areas { get; set; } = [];

    /// <summary>Used only when no area-specific source matches the request location.</summary>
    public bool IsFallback { get; set; }
}
