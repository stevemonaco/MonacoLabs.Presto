namespace MonacoLabs.Presto;

/// <summary>
/// A to-value that coasts from a release velocity and decays to rest, optionally snapped and bounced off bounds. Its end
/// value, ease and duration are resolved once the tween's start value is known, so any duration set beside it is ignored.
/// </summary>
public sealed record Inertia
{
    /// <summary>Creates an inertia starting at <paramref name="velocity"/> units per engine-time unit.</summary>
    public Inertia(double velocity) => Velocity = velocity;

    /// <summary>The start velocity, in units per engine-time unit.</summary>
    public double Velocity { get; init; }

    /// <summary>Scales the distance traveled: the ideal target is <c>from + Power · Velocity · TimeConstant</c>.</summary>
    public double Power { get; init; } = 1;

    /// <summary>The decay's time constant in engine-time units; larger values coast further and longer.</summary>
    public double TimeConstant { get; init; } = 350;

    /// <summary>The distance from the target at which the motion counts as settled.</summary>
    public double RestDelta { get; init; } = 0.5;

    /// <summary>Maps the ideal target to the one landed on, such as <see cref="Utils.Snap(double)"/>.</summary>
    public Func<double, double>? ModifyTarget { get; init; }

    /// <summary>The lower bound; crossing it hands over to a bounce spring that settles on it.</summary>
    public double? Min { get; init; }

    /// <summary>The upper bound; crossing it hands over to a bounce spring that settles on it.</summary>
    public double? Max { get; init; }

    /// <summary>The stiffness of the spring that settles on a crossed bound.</summary>
    public double BounceStiffness { get; init; } = 500;

    /// <summary>The damping of the spring that settles on a crossed bound.</summary>
    public double BounceDamping { get; init; } = 10;
}
