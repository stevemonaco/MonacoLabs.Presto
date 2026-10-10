namespace MonacoLabs.Presto;

/// <summary>
/// Timeline scoped overrides of <see cref="Presto.Defaults"/>, inherited by every child added to the timeline.
/// Every member left <c>null</c> keeps the global value.
/// </summary>
public sealed class DefaultsParams
{
    /// <inheritdoc cref="TimerParams.PlaybackRate"/>
    public double? PlaybackRate { get; init; }

    /// <inheritdoc cref="TimerParams.FrameRate"/>
    public double? FrameRate { get; init; }

    /// <inheritdoc cref="TimerParams.Loop"/>
    public LoopCount? Loop { get; init; }

    /// <inheritdoc cref="TimerParams.Reversed"/>
    public bool? Reversed { get; init; }

    /// <inheritdoc cref="TimerParams.Alternate"/>
    public bool? Alternate { get; init; }

    /// <inheritdoc cref="TimerParams.Autoplay"/>
    public bool? Autoplay { get; init; }

    /// <inheritdoc cref="PrestoDefaults.Duration"/>
    public TweenValue? Duration { get; init; }

    /// <inheritdoc cref="TimerParams.Delay"/>
    public TweenValue? Delay { get; init; }

    /// <inheritdoc cref="TimerParams.LoopDelay"/>
    public double? LoopDelay { get; init; }

    /// <inheritdoc cref="PrestoDefaults.Ease"/>
    public EaseValue? Ease { get; init; }

    /// <inheritdoc cref="PrestoDefaults.PlaybackEase"/>
    public EaseValue? PlaybackEase { get; init; }

    /// <inheritdoc cref="PrestoDefaults.Composition"/>
    public Composition? Composition { get; init; }

    /// <inheritdoc cref="PrestoDefaults.Modifier"/>
    public Func<double, double>? Modifier { get; init; }

    /// <inheritdoc cref="Timer.OnBegin"/>
    public Action<Timer>? OnBegin { get; init; }

    /// <inheritdoc cref="Timer.OnBeforeUpdate"/>
    public Action<Timer>? OnBeforeUpdate { get; init; }

    /// <inheritdoc cref="Timer.OnUpdate"/>
    public Action<Timer>? OnUpdate { get; init; }

    /// <inheritdoc cref="Timer.OnLoop"/>
    public Action<Timer>? OnLoop { get; init; }

    /// <inheritdoc cref="Timer.OnPause"/>
    public Action<Timer>? OnPause { get; init; }

    /// <inheritdoc cref="Timer.OnComplete"/>
    public Action<Timer>? OnComplete { get; init; }

    /// <inheritdoc cref="PrestoDefaults.OnRender"/>
    public Action<Timer>? OnRender { get; init; }

    internal PrestoDefaults Merge(PrestoDefaults globals) => globals with
    {
        PlaybackRate = PlaybackRate ?? globals.PlaybackRate,
        FrameRate = FrameRate ?? globals.FrameRate,
        Loop = Loop ?? globals.Loop,
        Reversed = Reversed ?? globals.Reversed,
        Alternate = Alternate ?? globals.Alternate,
        Autoplay = Autoplay ?? globals.Autoplay,
        Duration = Duration ?? globals.Duration,
        Delay = Delay ?? globals.Delay,
        LoopDelay = LoopDelay ?? globals.LoopDelay,
        Ease = Ease ?? globals.Ease,
        PlaybackEase = PlaybackEase ?? globals.PlaybackEase,
        Composition = Composition ?? globals.Composition,
        Modifier = Modifier ?? globals.Modifier,
        OnBegin = OnBegin ?? globals.OnBegin,
        OnBeforeUpdate = OnBeforeUpdate ?? globals.OnBeforeUpdate,
        OnUpdate = OnUpdate ?? globals.OnUpdate,
        OnLoop = OnLoop ?? globals.OnLoop,
        OnPause = OnPause ?? globals.OnPause,
        OnComplete = OnComplete ?? globals.OnComplete,
        OnRender = OnRender ?? globals.OnRender,
    };
}
