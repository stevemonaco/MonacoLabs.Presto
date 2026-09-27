namespace Monaco.Presto;

/// <summary>
/// Parameters for <see cref="Presto.CreateAnimatable"/>: the animated properties through the indexer
/// (<c>["X"] = 20</c>), the settings every property animation inherits, and the callbacks, which run on an animation of
/// their own spanning the whole burst rather than on each property.
/// </summary>
public record AnimatableParams
{
    /// <summary>The settings for one animated property, which a bare number gives the duration of.</summary>
    public AnimatableProperty this[string property]
    {
        get => Properties[property];
        init => Properties[property] = value;
    }

    /// <summary>A <c>with</c> copy shares this dictionary; every consumer only reads it.</summary>
    internal OrderedDictionary<string, AnimatableProperty> Properties { get; } = new();

    /// <summary>How long every property takes to reach a value set on it, unless it carries its own <see cref="AnimatableProperty.Duration"/>.</summary>
    public TweenValue? Duration { get; init; }

    /// <summary>The ease every property follows, unless it carries its own <see cref="AnimatableProperty.Ease"/>.</summary>
    public EaseValue? Ease { get; init; }

    /// <summary>Post-processes every property's value, unless it carries its own <see cref="AnimatableProperty.Modifier"/>.</summary>
    public Func<double, double>? Modifier { get; init; }

    /// <inheritdoc cref="TimerParams.PlaybackRate"/>
    public double? PlaybackRate { get; init; }

    /// <inheritdoc cref="TimerParams.FrameRate"/>
    public double? FrameRate { get; init; }

    /// <summary>Raised when a property starts moving and none was already moving.</summary>
    public Action<Animatable>? OnBegin { get; init; }

    /// <summary>Raised on every frame while any property is moving.</summary>
    public Action<Animatable>? OnUpdate { get; init; }

    /// <summary>Raised once every property has come to rest.</summary>
    public Action<Animatable>? OnComplete { get; init; }
}
