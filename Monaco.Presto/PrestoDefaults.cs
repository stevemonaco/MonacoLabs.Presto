using Monaco.Presto.Core;

namespace Monaco.Presto;

/// <summary>Global default parameter values applied to every timer that does not override them.</summary>
public sealed record PrestoDefaults
{
    public double PlaybackRate { get; set; } = 1;
    public double FrameRate { get; set; } = Constants.MaxFps;
    public LoopCount Loop { get; set; }
    public bool Reversed { get; set; }
    public bool Alternate { get; set; }
    public bool Autoplay { get; set; } = true;
    public TweenValue Duration { get; set; } = Constants.K;
    public TweenValue Delay { get; set; } = 0;
    public double LoopDelay { get; set; }
    public EaseValue Ease { get; set; } = Eases.Out(2);
    public EaseValue? PlaybackEase { get; set; }
    public Composition Composition { get; set; } = Composition.Replace;
    public Func<double, double> Modifier { get; set; } = static v => v;
    public Action<Timer>? OnBegin { get; set; }
    public Action<Timer>? OnBeforeUpdate { get; set; }
    public Action<Timer>? OnUpdate { get; set; }
    public Action<Timer>? OnLoop { get; set; }
    public Action<Timer>? OnPause { get; set; }
    public Action<Timer>? OnComplete { get; set; }
    public Action<Timer>? OnRender { get; set; }
}
