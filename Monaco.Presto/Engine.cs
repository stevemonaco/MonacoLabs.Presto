using System.Diagnostics;
using Monaco.Presto.Core;

namespace Monaco.Presto;

/// <summary>Root clock driving every running timer from a single <see cref="IFrameSource"/>; use the singleton at <see cref="Presto.Engine"/>.</summary>
public sealed class Engine : Clock
{
    private IFrameSource _frameSource;
    private int _threadId;

    internal Engine(IFrameSource frameSource) : base(frameSource.Now)
    {
        _frameSource = frameSource;
    }

    /// <summary>Clock and frame pump; swapping it while running restarts the pump on the new source.</summary>
    public IFrameSource FrameSource
    {
        get => _frameSource;
        set
        {
            var wasRunning = IsRunning;
            if (wasRunning) _frameSource.Stop();
            _frameSource = value;
            _threadId = 0;
            if (wasRunning) value.Start(OnFrame);
        }
    }

    internal double Now => _frameSource.Now;

    /// <summary>When false, the engine never starts its frame source and must be driven through <see cref="Update()"/>.</summary>
    public bool UseDefaultMainLoop { get; set; } = true;

    /// <summary>Whether <see cref="Pause"/> has stopped the engine; like upstream <c>engine.paused</c> it also reads true before the first <see cref="Resume"/>.</summary>
    public bool Paused { get; internal set; } = true;

    /// <summary>True while the frame source is pumping frames.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>The global defaults, the same instance as <see cref="Presto.Defaults"/>.</summary>
    public PrestoDefaults Defaults => Globals.Defaults;

    /// <summary>Ticks every running child at the frame source's current time and drops the paused ones.</summary>
    public void Update()
    {
        PinThread();
        var time = _currentTime = Now;
        if (RequestTick(time) == TickMode.None) return;
        ComputeDeltaTime(time);
        var engineSpeed = _speed;
        var engineFps = _fps;
        var activeTickable = _head;
        while (activeTickable != null)
        {
            var nextTickable = activeTickable._next;
            if (!activeTickable.Paused)
            {
                Renderer.Tick(
                    activeTickable,
                    (time - activeTickable._startTime) * activeTickable._speed * engineSpeed,
                    false,
                    false,
                    activeTickable._fps < engineFps ? activeTickable.RequestTick(time) : TickMode.Auto);
            }
            else
            {
                Helpers.RemoveChild(this, activeTickable);
                _hasChildren = _tail != null;
                activeTickable._running = false;
                if (activeTickable.Completed && !activeTickable._cancelled)
                {
                    activeTickable.Cancel();
                }
            }
            activeTickable = nextTickable;
        }
        Additive.Update();
    }

    /// <summary>Starts the frame source unless it is already pumping or <see cref="UseDefaultMainLoop"/> is off; resuming a timer calls it (upstream <c>engine.wake()</c>).</summary>
    public Engine Wake()
    {
        if (UseDefaultMainLoop && !IsRunning)
        {
            // Immediately request a tick so timers created before the first frame compute an accurate offset.
            RequestTick(Now);
            IsRunning = true;
            _frameSource.Start(OnFrame);
        }
        return this;
    }

    private void OnFrame()
    {
        if (_head != null)
        {
            Update();
        }
        else
        {
            IsRunning = false;
            _frameSource.Stop();
        }
    }

    /// <summary>Stops the frame source, freezing every timer where it stands (upstream <c>engine.pause()</c>).</summary>
    public Engine Pause()
    {
        AssertThreadAffinity();
        if (!IsRunning) return this;
        Paused = true;
        IsRunning = false;
        _frameSource.Stop();
        return this;
    }

    /// <summary>Restarts the frame source and rebases every timer's start time, so the paused interval does not count as elapsed (upstream <c>engine.resume()</c>).</summary>
    public Engine Resume()
    {
        AssertThreadAffinity();
        if (!Paused) return this;
        Paused = false;
        Helpers.ForEachChildren(this, static child => child.ResetTime());
        return Wake();
    }

    /// <summary>Global playback rate multiplied into every timer's own (upstream <c>engine.speed</c>); setting it rebases the running timers so they do not jump.</summary>
    public override double Speed
    {
        get => _speed * (Globals.TimeScale == 1 ? 1 : Constants.K);
        set
        {
            AssertThreadAffinity();
            var speed = value * Globals.TimeScale;
            if (_speed == speed) return;
            _speed = speed;
            Helpers.ForEachChildren(this, static child => child.Speed = child._speed);
        }
    }

    /// <summary>
    /// Unit every duration, delay and time value in the API is expressed in (upstream <c>engine.timeUnit</c>): milliseconds unless this is
    /// <see cref="TimeUnit.Seconds"/>. Switching rescales the default duration and the engine speed, so set it before creating timers.
    /// </summary>
    public TimeUnit TimeUnit
    {
        get => Globals.TimeScale == 1 ? TimeUnit.Milliseconds : TimeUnit.Seconds;
        set
        {
            AssertThreadAffinity();
            const double secondsScale = 0.001;
            var isSecond = value == TimeUnit.Seconds;
            var newScale = isSecond ? secondsScale : 1;
            if (Globals.TimeScale == newScale) return;
            Globals.TimeScale = newScale;
            Globals.TickThreshold = 200 * newScale;
            var scaleFactor = isSecond ? secondsScale : Constants.K;
            if (Defaults.Duration.AsDouble is { } defaultDuration) Defaults.Duration = defaultDuration * scaleFactor;
            _speed *= scaleFactor;
        }
    }

    /// <summary>Number of decimals reported by time and progress getters.</summary>
    public int Precision
    {
        get => Globals.Precision;
        set => Globals.Precision = value;
    }

    [Conditional("DEBUG")]
    private void PinThread()
    {
        if (_threadId == 0) _threadId = Environment.CurrentManagedThreadId;
        AssertThreadAffinity();
    }

    /// <summary>Debug-only guard for the single-threaded model: once the engine has ticked, it and its timers must only be touched from that thread.</summary>
    [Conditional("DEBUG")]
    internal void AssertThreadAffinity() =>
        Debug.Assert(_threadId == 0 || _threadId == Environment.CurrentManagedThreadId, "Presto.Engine and its timers must only be used from the thread that ticks the engine.");
}
