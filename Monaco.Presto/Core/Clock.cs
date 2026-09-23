namespace Monaco.Presto.Core;

/// <summary>Base class controlling frame rate and playback rate; inherited by <c>Engine</c> and <c>Timer</c>.</summary>
public abstract class Clock
{
    internal double _currentTime;
    internal double _lastTickTime;
    internal double _startTime;
    internal double _lastTime;
    internal double _frameDuration = Constants.K / Constants.MaxFps;
    internal double _fps = Constants.MaxFps;
    internal double _speed = 1;
    internal bool _hasChildren;
    internal Timer? _head;
    internal Timer? _tail;
    internal Engine? _engine;

    /// <summary>Starts the clock at <paramref name="initTime"/>, the frame source time its first tick is measured from.</summary>
    protected Clock(double initTime = 0)
    {
        _currentTime = initTime;
        _lastTickTime = initTime;
        _startTime = initTime;
        _lastTime = initTime;
    }

    /// <summary>The engine this clock belongs to; a clock that belongs to none falls back to the ambient engine.</summary>
    internal Engine OwnerEngine => _engine ?? Engine.Current;

    /// <summary>Time in milliseconds elapsed since the previous tick.</summary>
    public double DeltaTime { get; internal set; }

    /// <summary>Cap on how often this clock renders, in frames per second (upstream <c>frameRate</c>); raising it past <see cref="PrestoDefaults.FrameRate"/> raises that default too, as upstream does.</summary>
    public double Fps
    {
        get => _fps;
        set
        {
            var fps = value < Constants.MinValue ? Constants.MinValue : value;
            var defaults = OwnerEngine.Defaults;
            if (fps > defaults.FrameRate) defaults.FrameRate = fps;
            _fps = fps;
            _frameDuration = Constants.K / fps;
        }
    }

    /// <summary>Multiplier on how fast this clock's time advances (upstream <c>playbackRate</c>); clamped to a tiny positive value, so it never stops.</summary>
    public virtual double Speed
    {
        get => _speed;
        set => _speed = value < Constants.MinValue ? Constants.MinValue : value;
    }

    internal TickMode RequestTick(double time)
    {
        var frameDuration = _frameDuration;
        var elapsed = time - _lastTickTime;
        var scaled = frameDuration * .25;
        // Tolerance keeps frames that arrive slightly early due to frame-source jitter, capped so it never dominates at high fps.
        var tolerance = scaled < 4 ? scaled : 4;
        if (elapsed + tolerance < frameDuration) return TickMode.None;
        _lastTickTime = elapsed >= frameDuration ? time - (elapsed % frameDuration) : time;
        return TickMode.Auto;
    }

    internal double ComputeDeltaTime(double time)
    {
        var delta = time - _lastTime;
        DeltaTime = delta;
        _lastTime = time;
        return delta;
    }
}
