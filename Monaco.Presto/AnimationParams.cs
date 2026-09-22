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
    public TweenValue this[string property]
    {
        get => Properties[property];
        init => Properties[property] = value;
    }

    /// <summary>A <c>with</c> copy shares this dictionary; every consumer only reads it.</summary>
    internal OrderedDictionary<string, TweenValue> Properties { get; } = new();

    public new TweenValue? Duration { get; init; }
    public new TweenValue? Delay { get; init; }
    public EaseValue? Ease { get; init; }
    public EaseValue? PlaybackEase { get; init; }
    public Func<double, double>? Modifier { get; init; }
    public Composition? Composition { get; init; }
    public Keyframes? Keyframes { get; init; }
    public Action<Timer>? OnRender { get; init; }
}
