namespace GoMigo.Application.Research;

public sealed class ResearchOptions
{
    public const string SectionName = "Research";

    /// <summary><c>Mock</c> (default, no credentials needed) or <c>TinyFish</c>.</summary>
    public string Provider { get; set; } = "Mock";

    /// <summary>Hard limit for a whole research run, including all provider calls.</summary>
    public int RunTimeoutSeconds { get; set; } = 300;

    public int MaxCandidates { get; set; } = 10;

    public int MaxConcurrentRuns { get; set; } = 2;
}
