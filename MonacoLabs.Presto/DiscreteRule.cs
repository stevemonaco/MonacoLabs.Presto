namespace MonacoLabs.Presto;

/// <summary>When a discrete tween, one over a value that cannot be interpolated, switches from its from value to its to value.</summary>
/// <remarks>The switch is a function of the eased progress, so seeking and reversing cross it at the same point.</remarks>
public enum DiscreteRule
{
    /// <summary>Halfway through, as CSS switches a discrete property.</summary>
    Midpoint,

    /// <summary>As soon as the tween moves off its start.</summary>
    AtStart,

    /// <summary>Only once the tween reaches its end.</summary>
    AtEnd,

    /// <summary>
    /// Holds <c>true</c> between the ends when either end is <c>true</c>, as CSS <c>visibility</c> stays visible through a
    /// fade; <see cref="Midpoint"/> for any other pair.
    /// </summary>
    TrueThroughout,

    /// <summary>Holds <c>false</c> between the ends when either end is <c>false</c>; <see cref="Midpoint"/> for any other pair.</summary>
    FalseThroughout,
}
