namespace Monaco.Presto;

/// <summary>
/// Parameters for <see cref="Presto.CreateAnimatable"/>: the animated properties through the indexer
/// (<c>["X"] = 20</c>), the settings every property animation inherits, and the callbacks, which run on an animation of
/// their own spanning the whole burst rather than on each property.
/// </summary>
public record AnimatableParams
{
    public AnimatableProperty this[string property]
    {
        get => Properties[property];
        init => Properties[property] = value;
    }

    /// <summary>A <c>with</c> copy shares this dictionary; every consumer only reads it.</summary>
    internal OrderedDictionary<string, AnimatableProperty> Properties { get; } = new();

    public TweenValue? Duration { get; init; }
    public EaseValue? Ease { get; init; }
    public Func<double, double>? Modifier { get; init; }
    public double? PlaybackRate { get; init; }
    public double? FrameRate { get; init; }
    public Action<Animatable>? OnBegin { get; init; }
    public Action<Animatable>? OnUpdate { get; init; }
    public Action<Animatable>? OnComplete { get; init; }
}
