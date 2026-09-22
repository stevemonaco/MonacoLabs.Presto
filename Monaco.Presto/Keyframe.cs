namespace Monaco.Presto;

/// <summary>One entry of <see cref="AnimationParams.Keyframes"/>: the property values it sets plus the tween settings for that frame.</summary>
public sealed class Keyframe
{
    public TweenValue this[string property]
    {
        get => Properties[property];
        init => Properties[property] = value;
    }

    internal OrderedDictionary<string, TweenValue> Properties { get; } = new();

    public TweenValue? Duration { get; init; }
    public TweenValue? Delay { get; init; }
    public EaseValue? Ease { get; init; }
    public Func<double, double>? Modifier { get; init; }
    public Composition? Composition { get; init; }
}
