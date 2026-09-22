namespace Monaco.Presto;

/// <summary>
/// Timeline scoped overrides of <see cref="Presto.Defaults"/>, inherited by every child added to the timeline.
/// Every member left <c>null</c> keeps the global value.
/// </summary>
public sealed class DefaultsParams
{
    public double? PlaybackRate { get; init; }
    public double? FrameRate { get; init; }
    public LoopCount? Loop { get; init; }
    public bool? Reversed { get; init; }
    public bool? Alternate { get; init; }
    public bool? Autoplay { get; init; }
    public TweenValue? Duration { get; init; }
    public TweenValue? Delay { get; init; }
    public double? LoopDelay { get; init; }
    public EaseValue? Ease { get; init; }
    public EaseValue? PlaybackEase { get; init; }
    public Composition? Composition { get; init; }
    public Func<double, double>? Modifier { get; init; }
    public Action<Timer>? OnBegin { get; init; }
    public Action<Timer>? OnBeforeUpdate { get; init; }
    public Action<Timer>? OnUpdate { get; init; }
    public Action<Timer>? OnLoop { get; init; }
    public Action<Timer>? OnPause { get; init; }
    public Action<Timer>? OnComplete { get; init; }
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
