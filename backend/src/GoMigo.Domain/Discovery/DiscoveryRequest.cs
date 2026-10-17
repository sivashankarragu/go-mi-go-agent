namespace GoMigo.Domain.Discovery;

/// <summary>
/// What the user wants: where they are, how long they have, what they can spend and what they feel like.
/// Manual <see cref="LocationText"/> is always enough; coordinates are optional.
/// </summary>
public sealed record DiscoveryRequest
{
    public required string LocationText { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public required int DurationMinutes { get; init; }
    public required decimal BudgetPerPerson { get; init; }
    public string Currency { get; init; } = Money.DefaultCurrency;
    public ActivityCategory Category { get; init; } = ActivityCategory.Any;
    public DateTimeOffset? StartTime { get; init; }
    public string? AdditionalPreferences { get; init; }

    public bool HasCoordinates => Latitude is not null && Longitude is not null;
}
