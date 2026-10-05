namespace Monaco.Presto;

/// <summary>
/// Parameters for <see cref="Presto.CreateTimer"/>. <c>null</c> falls back to <see cref="Presto.Defaults"/>; a <c>null</c> <see cref="Duration"/> makes the timer infinite.
/// Every duration, delay and time here — and everywhere else in the API — is in milliseconds unless <see cref="Engine.TimeUnit"/> is <see cref="TimeUnit.Seconds"/>.
/// A timer animates nothing, so it has no reduced-motion form.
/// </summary>
public record TimerParams
{
    /// <summary>Identifier for the timer; an auto-incrementing number is used when this is null.</summary>
    public string? Id { get; init; }

    /// <summary>Length of a single iteration; null runs the timer forever (upstream <c>duration</c>).</summary>
    public double? Duration { get; init; }

    /// <summary>Wait before the first iteration starts; it is not repeated between loops (upstream <c>delay</c>).</summary>
    public double? Delay { get; init; }

    /// <summary>Wait inserted between iterations (upstream <c>loopDelay</c>).</summary>
    public double? LoopDelay { get; init; }

    /// <summary>Starts the playhead at the end and runs it towards 0 (upstream <c>reversed</c>).</summary>
    public bool? Reversed { get; init; }

    /// <summary>Flips the direction after every iteration, so the timer ping-pongs (upstream <c>alternate</c>).</summary>
    public bool? Alternate { get; init; }

    /// <summary>Iterations to play beyond the first; <see cref="LoopCount.Infinite"/> never stops (upstream <c>loop</c>).</summary>
    public LoopCount? Loop { get; init; }

    /// <summary>Starts the timer as soon as it is created; false leaves it paused until <see cref="Timer.Play"/> (upstream <c>autoplay</c>).</summary>
    public bool? Autoplay { get; init; }

    /// <summary>Caps how often this timer renders, in frames per second; unlike <see cref="PlaybackRate"/> it does not change how fast its time advances (upstream <c>frameRate</c>).</summary>
    /// <remarks>0 or less means uncapped (240 fps), where upstream clamps it to a rate that never renders.</remarks>
    public double? FrameRate { get; init; }

    /// <summary>Multiplier on this timer's own time, so 2 finishes it in half the wall-clock duration (upstream <c>playbackRate</c>).</summary>
    public double? PlaybackRate { get; init; }

    /// <summary>Order this timer is ticked in among the engine's children each frame, lowest first (upstream <c>priority</c>).</summary>
    public int Priority { get; init; } = DefaultPriority;

    internal const int DefaultPriority = 1;

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
}
