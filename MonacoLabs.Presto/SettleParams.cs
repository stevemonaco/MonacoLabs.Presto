namespace MonacoLabs.Presto;

/// <summary>
/// How a released, scrubbed timer settles: the resting progresses it may land on, how the release velocity picks one,
/// and the spring that carries the playhead there.
/// </summary>
public sealed record SettleParams
{
    /// <summary>The release velocity in progress per engine-time unit; null reads the scrub source's velocity.</summary>
    public double? Velocity { get; init; }

    /// <summary>The resting progresses, clamped to [0, 1]; null means 0 and 1.</summary>
    public double[]? Points { get; init; }

    /// <summary>Whether a <see cref="Timeline"/> also rests on each label, at its time divided by the duration.</summary>
    public bool Labels { get; init; }

    /// <summary>How far ahead the velocity is projected to pick the nearest point, in engine-time units, as <see cref="Inertia.TimeConstant"/>.</summary>
    public double Projection { get; init; } = 350;

    /// <summary>The speed at or above which the settle advances to the next point in the velocity's direction; null disables it.</summary>
    public double? FlickVelocity { get; init; }

    /// <summary>The spring that carries the playhead; null means a new <see cref="Springs.Gentle"/>.</summary>
    public Spring? Spring { get; init; }
}
