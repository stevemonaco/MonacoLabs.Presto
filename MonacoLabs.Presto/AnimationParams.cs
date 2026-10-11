namespace MonacoLabs.Presto;

/// <summary>
/// Parameters for animations: property tweens through the indexer (<c>["X"] = 100</c>) mixed with timer settings and the
/// tween defaults every property inherits. <see cref="Duration"/> and <see cref="Delay"/> hide the <see cref="TimerParams"/>
/// members because they also accept a <see cref="FunctionValue"/>; the base members stay <c>null</c>, so the timer
/// constructor starts from its infinite/zero defaults and the animation then overrides them with the tween-derived values.
/// </summary>
public record AnimationParams : TimerParams
{
    /// <summary>The value every target animates <paramref name="property"/> to.</summary>
    public TweenValue this[PropertyKey property]
    {
        get => Properties[property.Unwrap()];
        init => Properties[property.Unwrap()] = value;
    }

    /// <summary>A <c>with</c> copy shares this dictionary; every consumer only reads it.</summary>
    internal OrderedDictionary<string, TweenValue> Properties { get; init; } = new();

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

    /// <summary>
    /// The form played while <see cref="PrestoDefaults.ReducedMotion"/> is set, as <see cref="AnimationBuilder.WhenReduced"/>
    /// gives a builder: its properties and keyframes replace these, and the settings and callbacks it sets override these.
    /// </summary>
    /// <remarks>
    /// A <see cref="WhenReduced"/> inside this one is ignored, and <see cref="PrestoDefaults.ReduceAll"/> leaves the reduced form alone.
    /// <see cref="AnimationBuilder.ToParams"/> never sets this: the builder has already picked its own form.
    /// </remarks>
    public AnimationParams? WhenReduced { get; init; }

    /// <summary>Set on params built from a <see cref="AnimationBuilder.WhenReduced"/> alternative, which <see cref="PrestoDefaults.ReduceAll"/> leaves alone.</summary>
    internal bool IsReducedMotion { get; init; }

    /// <summary>The params to play on <paramref name="engine"/>: these, or merged with <see cref="WhenReduced"/> while its defaults ask for reduced motion.</summary>
    internal AnimationParams Resolve(Engine engine) =>
        WhenReduced is not { } r || !engine.Defaults.ReducedMotion ? this : this with
        {
            Properties = r.Properties,
            Keyframes = r.Keyframes,
            Duration = r.Duration ?? Duration,
            Delay = r.Delay ?? Delay,
            Ease = r.Ease ?? Ease,
            PlaybackEase = r.PlaybackEase ?? PlaybackEase,
            Modifier = r.Modifier ?? Modifier,
            Composition = r.Composition ?? Composition,
            OnRender = r.OnRender ?? OnRender,
            Id = r.Id ?? Id,
            LoopDelay = r.LoopDelay ?? LoopDelay,
            Reversed = r.Reversed ?? Reversed,
            Alternate = r.Alternate ?? Alternate,
            Loop = r.Loop ?? Loop,
            Autoplay = r.Autoplay ?? Autoplay,
            FrameRate = r.FrameRate ?? FrameRate,
            PlaybackRate = r.PlaybackRate ?? PlaybackRate,
            Priority = r.Priority != TimerParams.DefaultPriority ? r.Priority : Priority,
            OnBegin = r.OnBegin ?? OnBegin,
            OnBeforeUpdate = r.OnBeforeUpdate ?? OnBeforeUpdate,
            OnUpdate = r.OnUpdate ?? OnUpdate,
            OnLoop = r.OnLoop ?? OnLoop,
            OnPause = r.OnPause ?? OnPause,
            OnComplete = r.OnComplete ?? OnComplete,
            HostLifetime = r.HostLifetime ?? HostLifetime,
            WhenReduced = null,
            IsReducedMotion = true,
        };
}
