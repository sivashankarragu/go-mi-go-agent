namespace GoMigo.Domain.Discovery;

/// <summary>Business limits for a discovery request (short local outings).</summary>
public static class DiscoveryLimits
{
    public const int MinDurationMinutes = 30;
    public const int MaxDurationMinutes = 720;
    public const decimal MinBudgetPerPerson = 0m;
    public const decimal MaxBudgetPerPerson = 100_000m;
    public const int MaxLocationLength = 200;
    public const int MaxPreferencesLength = 500;

    /// <summary>A start time may be slightly in the past (clock skew, "starting now").</summary>
    public static readonly TimeSpan MaxStartTimeInPast = TimeSpan.FromHours(1);
    public static readonly TimeSpan MaxStartTimeInFuture = TimeSpan.FromDays(7);

    public static readonly IReadOnlyList<string> SupportedCurrencies = [Money.DefaultCurrency];
    public static readonly IReadOnlyList<string> SupportedCities = ["Bengaluru"];
}
