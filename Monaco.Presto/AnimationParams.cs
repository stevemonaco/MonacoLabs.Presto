namespace Monaco.Presto;

/// <summary>
/// Parameters for animations: property tweens through the indexer (<c>["X"] = 100</c>) mixed with timer settings and the
/// tween defaults every property inherits. <see cref="Duration"/> and <see cref="Delay"/> hide the <see cref="TimerParams"/>
/// members because they also accept a <see cref="FunctionValue"/>; the base members stay <c>null</c>, so the timer
/// constructor starts from its infinite/zero defaults and the animation then overrides them with the tween-derived values,
/// as upstream's <c>super()</c> call does.
/// </summary>
public record AnimationParams : TimerParams
{
    /// <summary>The value every target animates <paramref name="property"/> to.</summary>
    public TweenValue this[string property]
    {
        get => Properties[property];
        init => Properties[property] = value;
    }

    /// <summary>A <c>with</c> copy shares this dictionary; every consumer only reads it.</summary>
    internal OrderedDictionary<string, TweenValue> Properties { get; } = new();

    /// <summary>How long each property takes, in milliseconds, or a <see cref="FunctionValue"/> giving each target its own.</summary>
    public new TweenValue? Duration { get; init; }

    /// <summary>How long each property waits before it starts, in milliseconds, or a <see cref="FunctionValue"/> or <see cref="Stagger"/> giving each target its own.</summary>
    public new TweenValue? Delay { get; init; }

    /// <summary>The ease applied to every property's own progress; a <see cref="Spring"/> also decides the duration.</summary>
    public EaseValue? Ease { get; init; }

    /// <summary>An ease applied to the animation's playhead instead of to each property, so the whole animation including its delays is eased at once.</summary>
    public EaseValue? PlaybackEase { get; init; }

    /// <summary>Maps every interpolated number just before it is written, for rounding, clamping or wrapping.</summary>
    public Func<double, double>? Modifier { get; init; }

    /// <summary>How these tweens meet tweens already running on the same target properties.</summary>
    public Composition? Composition { get; init; }

    /// <summary>Animates the properties through a sequence of frames rather than straight to a single value.</summary>
    public Keyframes? Keyframes { get; init; }

    /// <summary>Called after the tweens have been written each frame, once per animation rather than once per target.</summary>
    public Action<Timer>? OnRender { get; init; }
}
