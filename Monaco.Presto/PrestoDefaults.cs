using Monaco.Presto.Core;

namespace Monaco.Presto;

/// <summary>Global default parameter values applied to every timer that does not override them.</summary>
public sealed class PrestoDefaults
{
    public double PlaybackRate { get; set; } = 1;
    public double FrameRate { get; set; } = Constants.MaxFps;
    public LoopCount Loop { get; set; }
    public bool Reversed { get; set; }
    public bool Alternate { get; set; }
    public bool Autoplay { get; set; } = true;
    public double Duration { get; set; } = Constants.K;
    public double Delay { get; set; }
    public double LoopDelay { get; set; }
    public Action<Timer>? OnBegin { get; set; }
    public Action<Timer>? OnBeforeUpdate { get; set; }
    public Action<Timer>? OnUpdate { get; set; }
    public Action<Timer>? OnLoop { get; set; }
    public Action<Timer>? OnPause { get; set; }
    public Action<Timer>? OnComplete { get; set; }
}
