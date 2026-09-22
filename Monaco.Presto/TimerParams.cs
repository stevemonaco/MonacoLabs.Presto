namespace Monaco.Presto;

/// <summary>Parameters for <see cref="Presto.CreateTimer"/>. <c>null</c> falls back to <see cref="Presto.Defaults"/>; a <c>null</c> <see cref="Duration"/> makes the timer infinite.</summary>
public record TimerParams
{
    public string? Id { get; init; }
    public double? Duration { get; init; }
    public double? Delay { get; init; }
    public double? LoopDelay { get; init; }
    public bool? Reversed { get; init; }
    public bool? Alternate { get; init; }
    public LoopCount? Loop { get; init; }
    public bool? Autoplay { get; init; }
    public double? FrameRate { get; init; }
    public double? PlaybackRate { get; init; }
    public int Priority { get; init; } = 1;
    public Action<Timer>? OnBegin { get; init; }
    public Action<Timer>? OnBeforeUpdate { get; init; }
    public Action<Timer>? OnUpdate { get; init; }
    public Action<Timer>? OnLoop { get; init; }
    public Action<Timer>? OnPause { get; init; }
    public Action<Timer>? OnComplete { get; init; }
}
