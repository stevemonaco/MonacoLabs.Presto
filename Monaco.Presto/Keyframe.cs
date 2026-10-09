namespace Monaco.Presto;

/// <summary>One entry of <see cref="AnimationParams.Keyframes"/>: the property values it sets plus the tween settings for that frame.</summary>
public sealed class Keyframe
{
    /// <summary>The value this frame animates <paramref name="property"/> to, starting from whatever the previous frame left there.</summary>
    public TweenValue this[PropertyKey property]
    {
        get => Properties[property.Unwrap()];
        init => Properties[property.Unwrap()] = value;
    }

    internal OrderedDictionary<string, TweenValue> Properties { get; } = new();

    /// <summary>How long this frame takes; without it the animation's duration is split evenly between the frames.</summary>
    public TweenValue? Duration { get; init; }

    /// <summary>How long this frame waits after the previous one ends.</summary>
    public TweenValue? Delay { get; init; }

    /// <summary>Overrides the animation's ease for this frame.</summary>
    public EaseValue? Ease { get; init; }

    /// <summary>Maps this frame's interpolated numbers just before they are written.</summary>
    public Func<double, double>? Modifier { get; init; }

    /// <summary>Overrides the animation's composition mode for this frame.</summary>
    public Composition? Composition { get; init; }
}
