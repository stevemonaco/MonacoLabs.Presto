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

    protected Clock(double initTime = 0)
    {
        _currentTime = initTime;
        _lastTickTime = initTime;
        _startTime = initTime;
        _lastTime = initTime;
    }

    /// <summary>Time in milliseconds elapsed since the previous tick.</summary>
    public double DeltaTime { get; internal set; }

    public double Fps
    {
        get => _fps;
        set
        {
            var fps = value < Constants.MinValue ? Constants.MinValue : value;
            if (fps > Globals.Defaults.FrameRate) Globals.Defaults.FrameRate = fps;
            _fps = fps;
            _frameDuration = Constants.K / fps;
        }
    }

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
