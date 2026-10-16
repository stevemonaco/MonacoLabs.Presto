using System.Diagnostics;
using System.Runtime.CompilerServices;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto;

/// <summary>Root clock driving every running timer from a single <see cref="IFrameSource"/>; the main engine is <see cref="Presto.Engine"/>.</summary>
public sealed class Engine : Clock
{
    [ThreadStatic]
    private static Engine? s_current;

    private IFrameSource _frameSource;
    private int _threadId;
    private List<MotionValue> _sinks = [];
    private List<MotionValue> _flushingSinks = [];
    internal readonly List<MotionValue> _activeValues = [];
    internal ConditionalWeakTable<object, Dictionary<string, TweenSiblings>> ReplaceLookups { get; } = new();
    // Engine time is the frame source's time times _rate plus _timeOffset; _rate is TimeDilation, or 0 while frozen.
    internal double _timeOffset;
    private double _rate = 1;
    private double _dilation = 1;
    // Engine time at which Pause stopped the pump; NaN once the pump runs again.
    private double _pausedAt = double.NaN;
    private bool _hidden;
    private bool _pauseOnHidden = true;

    /// <summary>Creates an independent engine driven by its own frame source, starting from a copy of <see cref="Presto.Defaults"/>; the main engine is <see cref="Presto.Engine"/>.</summary>
    public Engine(IFrameSource frameSource) : this(frameSource, Presto.Defaults with { })
    {
    }

    // The main engine cannot snapshot Presto.Defaults: it is the engine that owns them.
    internal Engine(IFrameSource frameSource, PrestoDefaults defaults) : base(frameSource.Now)
    {
        _engine = this;
        // Globals.TimeScale is process-wide, so an engine created in seconds mode must start out scaled like the main one.
        _speed = Globals.TimeScale;
        _frameSource = frameSource;
        Defaults = defaults;
        Additive = new(this);
        Diagnostics = new(this);
    }

    /// <summary>The engine timers created on this thread run on: the one <see cref="MakeCurrent"/> installed here, or <see cref="Presto.Engine"/>.</summary>
    public static Engine Current => s_current ?? Presto.Engine;

    /// <summary>Makes this the <see cref="Current"/> engine on the calling thread until the returned handle is disposed, which restores the previous one.</summary>
    public EngineScope MakeCurrent()
    {
        var previous = s_current;
        s_current = this;
        return new EngineScope(previous);
    }

    internal static void RestoreCurrent(Engine? previous) => s_current = previous;

    /// <summary>Clock and frame pump; swapping it while running restarts the pump on the new source.</summary>
    public IFrameSource FrameSource
    {
        get => _frameSource;
        set
        {
            var wasRunning = IsRunning;
            if (wasRunning) _frameSource.Stop();
            var now = Now;
            _frameSource = value;
            _timeOffset = now - value.Now * _rate;
            _threadId = 0;
            if (wasRunning) value.Start(OnFrame);
        }
    }

    /// <summary>Engine time in milliseconds: the frame source's clock scaled by <see cref="TimeDilation"/>, held still while <see cref="Frozen"/>.</summary>
    public double Now => _frameSource.Now * _rate + _timeOffset;

    /// <summary>Slow-motion factor on everything this engine runs, springs and velocity tracking included; separate from <see cref="Speed"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public double TimeDilation
    {
        get => _dilation;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _dilation = value;
            if (!Frozen) Rebase(value);
        }
    }

    /// <summary>Whether <see cref="Freeze"/> is holding engine time still.</summary>
    public bool Frozen => _rate == 0;

    /// <summary>When false, the engine never starts its frame source and must be driven through <see cref="Update()"/>.</summary>
    public bool UseDefaultMainLoop { get; set; } = true;

    /// <summary>Whether setting <see cref="Hidden"/> pauses and resumes the engine; turning it off while hidden resumes.</summary>
    public bool PauseOnHidden
    {
        get => _pauseOnHidden;
        set
        {
            AssertThreadAffinity();
            if (_pauseOnHidden == value) return;
            _pauseOnHidden = value;
            if (_hidden) ApplyHidden(value);
        }
    }

    /// <summary>Set by the host when nothing it shows can be seen (minimized, backgrounded, page hidden); with <see cref="PauseOnHidden"/> it pauses the engine and showing resumes it.</summary>
    /// <remarks>
    /// While hidden, <see cref="Wake"/> does not start the frame source, so a timer created or resumed then waits for the show instead of
    /// ticking on frames a minimized host may still deliver. Showing calls <see cref="Resume"/>
    /// even when the engine was paused by hand before hiding. Showing while <see cref="Frozen"/> leaves the restart to <see cref="Thaw"/>.
    /// </remarks>
    public bool Hidden
    {
        get => _hidden;
        set
        {
            AssertThreadAffinity();
            if (_hidden == value) return;
            _hidden = value;
            if (_pauseOnHidden) ApplyHidden(value);
        }
    }

    /// <summary>Whether <see cref="Pause"/> has stopped the engine; it also reads true before the first <see cref="Resume"/>.</summary>
    public bool Paused { get; internal set; } = true;

    /// <summary>True while the frame source is pumping frames.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>The defaults every timer created on this engine inherits; <see cref="Scope.Execute{T}"/> swaps in the scope's own while it runs.</summary>
    public PrestoDefaults Defaults { get; internal set; }

    internal Scope? CurrentScope { get; set; }

    /// <summary>A host hook told of each root timer once the <see cref="Presto"/> factories have initialized it, and of each child a timeline adds.</summary>
    internal Action<Timer>? TimerCreated { get; set; }

    internal Additive Additive { get; }

    /// <summary>Opt-in registry of this engine's timers and frame statistics, for inspectors.</summary>
    public EngineDiagnostics Diagnostics { get; }

    /// <summary>Ticks every running child at the frame source's current time and drops the paused ones.</summary>
    public void Update()
    {
        PinThread();
        var time = _currentTime = Now;
        if (RequestTick(time) == TickMode.None) return;
        var started = Diagnostics.Enabled ? Stopwatch.GetTimestamp() : 0;
        ComputeDeltaTime(time);
        var engineSpeed = _speed;
        var engineFps = _fps;
        var activeTickable = _head;
        while (activeTickable != null)
        {
            _nextChild = activeTickable._next;
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
                if (activeTickable.Completed && !activeTickable._canceled)
                {
                    activeTickable.DetachTweens();
                    Diagnostics.Ended(activeTickable);
                }
            }
            activeTickable = _nextChild;
        }
        Additive.Update();
        var stepped = StepValues(time);
        var flushed = FlushSinks();
        if (started != 0) Diagnostics.Record(time, started, stepped, flushed);
    }

    internal void QueueSink(MotionValue sink)
    {
        _sinks.Add(sink);
        Wake();
    }

    internal void AddActive(MotionValue value)
    {
        _activeValues.Add(value);
        Wake();
    }

    // Values activated by a step are appended and stepped in the same pass; settled ones are compacted out in place.
    private int StepValues(double time)
    {
        var kept = 0;
        for (var i = 0; i < _activeValues.Count; i++)
        {
            var value = _activeValues[i];
            if (value.Step(time)) _activeValues[kept++] = value;
            else value._active = false;
        }
        var stepped = _activeValues.Count;
        _activeValues.RemoveRange(kept, stepped - kept);
        return stepped;
    }

    // Sinks queued by a Changed handler land in the swapped-in list, so they flush next frame instead of looping.
    private int FlushSinks()
    {
        if (_sinks.Count == 0) return 0;
        (_sinks, _flushingSinks) = (_flushingSinks, _sinks);
        for (var i = 0; i < _flushingSinks.Count; i++)
        {
            var sink = _flushingSinks[i];
            if (!sink._queued) continue;
            sink._queued = false;
            sink.Flush();
        }
        var flushed = _flushingSinks.Count;
        _flushingSinks.Clear();
        return flushed;
    }

    /// <summary>Starts the frame source unless it is already pumping, the engine is <see cref="Frozen"/> or paused by <see cref="Hidden"/>, or <see cref="UseDefaultMainLoop"/> is off; resuming a timer calls it.</summary>
    public Engine Wake()
    {
        if (UseDefaultMainLoop && !IsRunning && !Frozen && !(_hidden && _pauseOnHidden))
        {
            _pausedAt = double.NaN;
            // Immediately request a tick so timers created before the first frame compute an accurate offset.
            RequestTick(Now);
            IsRunning = true;
            _frameSource.Start(OnFrame);
        }
        return this;
    }

    private void OnFrame()
    {
        if (Frozen) return;
        if (_head != null || _sinks.Count > 0 || _activeValues.Count > 0)
        {
            Update();
        }
        else
        {
            IsRunning = false;
            _frameSource.Stop();
        }
    }

    /// <summary>Stops the frame source, freezing every timer where it stands.</summary>
    /// <remarks>A soft pause: a timer played afterwards, or showing a <see cref="Hidden"/> engine, resumes pumping; <see cref="Freeze"/> holds through both.</remarks>
    public Engine Pause()
    {
        AssertThreadAffinity();
        if (!IsRunning) return this;
        Paused = true;
        IsRunning = false;
        _frameSource.Stop();
        _pausedAt = Now;
        return this;
    }

    // Paused is set even when the pump was idle, so the show's Resume rebases timers created while hidden.
    private void ApplyHidden(bool hide)
    {
        if (!hide)
        {
            Resume();
            return;
        }
        Pause();
        Paused = true;
    }

    /// <summary>Holds engine time still and stops the frame source, without pausing anything or firing <see cref="Timer.OnPause"/>.</summary>
    public Engine Freeze()
    {
        AssertThreadAffinity();
        if (Frozen) return this;
        Rebase(0);
        if (!IsRunning) return this;
        IsRunning = false;
        _frameSource.Stop();
        return this;
    }

    /// <summary>Lets engine time run again from where <see cref="Freeze"/> held it and restarts the frame source unless <see cref="Pause"/> stopped it.</summary>
    public Engine Thaw()
    {
        AssertThreadAffinity();
        if (!Frozen) return this;
        Rebase(_dilation);
        return double.IsNaN(_pausedAt) ? Wake() : this;
    }

    /// <summary>Advances engine time by <paramref name="milliseconds"/>, undilated, and runs one <see cref="Update"/>; works frozen or running.</summary>
    public void Step(double milliseconds = 1000.0 / 60)
    {
        _timeOffset += milliseconds;
        Update();
    }

    private void Rebase(double rate)
    {
        var now = Now;
        _rate = rate;
        _timeOffset = now - _frameSource.Now * rate;
    }

    /// <summary>Unlinks every child, the way <see cref="Update"/> prunes the paused ones, and drops the queued <see cref="MotionValue"/> sinks and moving values; for an engine that is retired for good and will never tick again.</summary>
    internal void DropChildren()
    {
        var child = _head;
        while (child != null)
        {
            var next = child._next;
            Helpers.RemoveChild(this, child);
            child._running = false;
            child = next;
        }
        _hasChildren = false;
        foreach (var sink in _sinks) sink._queued = false;
        _sinks.Clear();
        foreach (var value in _activeValues) value._active = false;
        _activeValues.Clear();
    }

    /// <summary>Restarts the frame source and rebases every timer's start time and moving value, so the paused interval does not count as elapsed.</summary>
    public Engine Resume()
    {
        AssertThreadAffinity();
        if (!Paused) return this;
        Paused = false;
        Helpers.ForEachChildren(this, static child => child.ResetTime());
        if (!double.IsNaN(_pausedAt))
        {
            var paused = Now - _pausedAt;
            foreach (var value in _activeValues) value.Rebase(paused);
            _pausedAt = double.NaN;
        }
        return Wake();
    }

    /// <summary>Global playback rate multiplied into every timer's own; clamped to a tiny positive value, and setting it rebases the running timers so they do not jump.</summary>
    public override double Speed
    {
        get => _speed * (Globals.TimeScale == 1 ? 1 : Constants.K);
        set
        {
            AssertThreadAffinity();
            var speed = (value < Constants.MinValue ? Constants.MinValue : value) * Globals.TimeScale;
            if (_speed == speed) return;
            _speed = speed;
            Helpers.ForEachChildren(this, static child => child.Speed = child._speed);
        }
    }

    /// <summary>
    /// Unit every duration, delay and time value in the API is expressed in: milliseconds unless this is
    /// <see cref="TimeUnit.Seconds"/>. Switching rescales the default duration and the engine speed, so set it before creating timers.
    /// </summary>
    /// <exception cref="InvalidOperationException">Set on an engine other than <see cref="Presto.Engine"/>; the unit is process-wide.</exception>
    public TimeUnit TimeUnit
    {
        get => Globals.TimeScale == 1 ? TimeUnit.Milliseconds : TimeUnit.Seconds;
        set
        {
            if (this != Presto.Engine) throw new InvalidOperationException("TimeUnit is process-wide and can only be set on Presto.Engine.");
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
        set
        {
            AssertThreadAffinity();
            Globals.Precision = value;
        }
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
        Debug.Assert(_threadId == 0 || _threadId == Environment.CurrentManagedThreadId, "An engine and its timers must only be used from the thread that ticks the engine.");
}
