namespace GoMigo.Domain.Discovery;

/// <summary>Kinds of activity a user can ask for, and that a venue can be classified as.</summary>
public enum ActivityCategory
{
    /// <summary>No preference (requests only).</summary>
    Any = 0,
    Fun,
    Food,
    Culture,
    Outdoors,
    Nightlife,
    Shopping,
    Relaxation,
    /// <summary>Venue could not be classified (venues only).</summary>
    Other
}
