namespace Monaco.Presto;

/// <summary>Per-property tween values and settings (upstream <c>TweenKeyValue</c>): <c>["Rotate"] = new TweenParams { From = -180 }</c>.</summary>
public sealed class TweenParams
{
    /// <summary>The value the tween starts from; without it the tween starts from the previous tween's end value, or from the target's current value.</summary>
    public TweenValue? From { get; init; }

    /// <summary>The value the tween ends on.</summary>
    public TweenValue? To { get; init; }

    /// <summary>Overrides the animation's duration for this property or keyframe.</summary>
    public TweenValue? Duration { get; init; }

    /// <summary>Overrides the animation's delay for this property; on a keyframe it delays that frame after the previous one ends.</summary>
    public TweenValue? Delay { get; init; }

    /// <summary>Overrides the animation's ease for this property or keyframe.</summary>
    public EaseValue? Ease { get; init; }

    /// <summary>Maps the interpolated number just before it is written, for rounding, clamping or wrapping.</summary>
    public Func<double, double>? Modifier { get; init; }

    /// <summary>Maps the tween's current value to the whole string the target receives, in place of the usual value composition.</summary>
    /// <remarks>
    /// The written string is read back as the next tween's from-value, so it must parse back to the number it was given:
    /// no thousands separators, and units kept in a separate element.
    /// </remarks>
    public Func<double, string>? TextModifier { get; init; }

    /// <summary>Overrides the animation's composition mode for this property, deciding how it meets tweens already running on it.</summary>
    public Composition? Composition { get; init; }
}
