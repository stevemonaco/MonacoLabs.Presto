namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// How a value-driven property moves to each new value. Give one timing source: a <see cref="Builder"/>,
/// <see cref="Duration"/>/<see cref="Ease"/>, a <see cref="Spring"/> or <see cref="Bounce"/>, or a <see cref="Token"/>;
/// with none, it follows <see cref="Springs.Gentle"/>.
/// </summary>
/// <remarks>A spring keeps its velocity when a new value arrives mid-flight; a duration ease restarts from the displayed value with its full duration.</remarks>
public sealed record FollowParams
{
    /// <summary>Milliseconds to reach a new value, or a <see cref="Bounce"/> spring's perceived duration.</summary>
    public double? Duration { get; init; }

    /// <summary>The ease to each new value; a <see cref="Monaco.Presto.Spring"/> here sets its own duration.</summary>
    public EaseValue? Ease { get; init; }

    /// <summary>A <see cref="Springs"/> preset name, such as <c>"Gentle"</c>, or <c>"bounce,duration"</c>, such as <c>"0.2,400"</c>.</summary>
    public string? Spring { get; init; }

    /// <summary>A spring's bounce, from 0 (critically damped) towards 1, with <see cref="Duration"/> as its perceived duration.</summary>
    public double? Bounce { get; init; }

    /// <summary>A builder whose duration and ease are taken; its properties and playback settings are not.</summary>
    public AnimationBuilder? Builder { get; init; }

    /// <summary>Motion tokens whose <see cref="MotionTokens.Medium"/> duration and <see cref="MotionTokens.Ease"/> are used when nothing else gives a timing.</summary>
    public MotionTokens? Token { get; init; }

    /// <summary>Animates the first value from the property's value before the binding, instead of snapping to it.</summary>
    public bool AnimateInitial { get; init; }

    /// <summary>The timing while <see cref="PrestoDefaults.ReducedMotion"/> is set; without it, each value snaps.</summary>
    public AnimationBuilder? WhenReduced { get; init; }
}
