using System.Runtime.CompilerServices;
using Monaco.Presto.Core;

namespace Monaco.Presto;

/// <summary>Base class of timers, animations and timelines: delay/duration/loop state machine, callbacks and playback controls.</summary>
public class Timer : Clock
{
    private static int s_timerId;

    private TaskCompletionSource<Timer>? _completion;
    private Action<Timer>? _onResolve;

    internal double _offset;
    internal double _delay;
    internal double _loopDelay;
    internal double _iterationTime;
    internal int _currentIteration;
    internal bool _running;
    internal bool _reversed;
    internal bool _reverse;
    internal bool _cancelled;
    internal bool _alternate;
    internal bool _autoplay;
    internal Timer? _prev;
    internal Timer? _next;
    internal Tween? _headTween;
    internal Tween? _tailTween;
    internal Ease? _ease;
    internal Action<Timer>? _onRender;
    internal int _priority;

    internal Timer(TimerParams parameters, Timer? parent, double parentPosition) : base(0)
    {
        ++s_timerId;

        var engine = Presto.Engine;
        var timerInitTime = parent != null ? 0 : engine._lastTickTime;
        var timerDefaults = parent?.ChildDefaults ?? Globals.Defaults;
        // A function based default delay is meaningless without targets, so a plain timer falls back to 0.
        var timerDelay = parameters.Delay ?? timerDefaults.Delay.AsDouble ?? 0;
        var timerDuration = parameters.Duration ?? double.PositiveInfinity;
        var timerLoop = parameters.Loop ?? timerDefaults.Loop;
        var timerLoopDelay = parameters.LoopDelay ?? timerDefaults.LoopDelay;
        var timerIterationCount = timerLoop.IsInfinite ? double.PositiveInfinity : timerLoop.Value + 1;

        double offsetPosition;
        if (parent != null)
        {
            offsetPosition = parentPosition;
        }
        else
        {
            if (!engine.IsRunning) engine.RequestTick(engine.Now);
            offsetPosition = (engine._lastTickTime - engine._startTime) * Globals.TimeScale;
        }

        var duration = Helpers.ClampInfinity(((timerDuration + timerLoopDelay) * timerIterationCount) - timerLoopDelay);

        Id = parameters.Id ?? s_timerId.ToString();
        Parent = parent;
        Duration = duration == 0 || double.IsNaN(duration) ? Constants.MinValue : duration;
        Paused = true;
        OnBegin = parameters.OnBegin ?? timerDefaults.OnBegin;
        OnBeforeUpdate = parameters.OnBeforeUpdate ?? timerDefaults.OnBeforeUpdate;
        OnUpdate = parameters.OnUpdate ?? timerDefaults.OnUpdate;
        OnLoop = parameters.OnLoop ?? timerDefaults.OnLoop;
        OnPause = parameters.OnPause ?? timerDefaults.OnPause;
        OnComplete = parameters.OnComplete ?? timerDefaults.OnComplete;
        IterationDuration = timerDuration;
        IterationCount = timerIterationCount;
        _autoplay = parent == null && (parameters.Autoplay ?? timerDefaults.Autoplay);
        _offset = offsetPosition;
        _delay = timerDelay;
        _loopDelay = timerLoopDelay;
        _reversed = parameters.Reversed ?? timerDefaults.Reversed;
        _reverse = _reversed;
        _alternate = parameters.Alternate ?? timerDefaults.Alternate;

        _lastTickTime = timerInitTime;
        _startTime = timerInitTime;
        _lastTime = timerInitTime;
        _fps = parameters.FrameRate ?? timerDefaults.FrameRate;
        _speed = parameters.PlaybackRate ?? timerDefaults.PlaybackRate;
        _priority = parameters.Priority;
    }

    public string Id { get; internal set; }

    public Timer? Parent { get; }

    /// <summary>The defaults the children of this timer inherit; a timeline overrides it with its own.</summary>
    internal virtual PrestoDefaults ChildDefaults => Globals.Defaults;

    /// <summary>Total duration including every iteration and loop delay.</summary>
    public double Duration { get; internal set; }

    /// <summary>Duration of a single iteration.</summary>
    public double IterationDuration { get; internal set; }

    /// <summary>Number of iterations; <see cref="double.PositiveInfinity"/> when looping forever.</summary>
    public double IterationCount { get; internal set; }

    public bool Backwards { get; internal set; }

    public bool Paused { get; internal set; }

    public bool Began { get; internal set; }

    public bool Completed { get; internal set; }

    public Action<Timer>? OnBegin { get; internal set; }

    public Action<Timer>? OnBeforeUpdate { get; internal set; }

    public Action<Timer>? OnUpdate { get; internal set; }

    public Action<Timer>? OnLoop { get; internal set; }

    public Action<Timer>? OnPause { get; internal set; }

    public Action<Timer>? OnComplete { get; internal set; }

    public bool Cancelled
    {
        get => _cancelled;
        set
        {
            if (value) Cancel(); else Reset(true).Play();
        }
    }

    public double CurrentTime
    {
        get => Helpers.Clamp(Helpers.Round(_currentTime, Globals.Precision), -_delay, Duration);
        set
        {
            var paused = Paused;
            Pause().Seek(value);
            if (!paused) Resume();
        }
    }

    public double IterationCurrentTime
    {
        get => Helpers.Clamp(Helpers.Round(_iterationTime, Globals.Precision), 0, IterationDuration);
        set => CurrentTime = (IterationDuration * _currentIteration) + value;
    }

    public double Progress
    {
        get => Helpers.Clamp(Helpers.Round(_currentTime / Duration, 10), 0, 1);
        set => CurrentTime = Duration * value;
    }

    public double IterationProgress
    {
        get => Helpers.Clamp(Helpers.Round(_iterationTime / IterationDuration, 10), 0, 1);
        set
        {
            var iterationDuration = IterationDuration;
            CurrentTime = (iterationDuration * _currentIteration) + (iterationDuration * value);
        }
    }

    public int CurrentIteration
    {
        get => _currentIteration;
        set => CurrentTime = IterationDuration * Helpers.Clamp(value, 0, IterationCount - 1);
    }

    public bool Reversed
    {
        get => _reversed;
        set
        {
            if (value) Reverse(); else Play();
        }
    }

    public override double Speed
    {
        set
        {
            base.Speed = value;
            ResetTime();
        }
    }

    private static void ResetTimerProperties(Timer timer)
    {
        timer.Paused = true;
        timer.Began = false;
        timer.Completed = false;
    }

    private static void ReviveTimer(Timer timer)
    {
        if (!timer._cancelled) return;
        if (timer._hasChildren)
        {
            Helpers.ForEachChildren(timer, ReviveTimer);
        }
        else
        {
            TweenList.ForEachChildren(timer, static tween =>
            {
                if (tween._composition != Composition.None) Composer.ComposeTween(tween, Composer.GetTweenSiblings(tween.Target, tween.Property));
            });
        }
        timer._cancelled = false;
    }

    private static bool SortByPriority(Timer prev, Timer child) => prev._priority > child._priority;

    public Timer Reset(bool softReset = false)
    {
        Presto.Engine.AssertThreadAffinity();
        ReviveTimer(this);
        if (_reversed && !_reverse) Reversed = false;
        _iterationTime = IterationDuration;
        Renderer.Tick(this, 0, true, softReset, TickMode.Force);
        ResetTimerProperties(this);
        if (_hasChildren) Helpers.ForEachChildren(this, ResetTimerProperties);
        return this;
    }

    public virtual Timer Init(bool internalRender = false)
    {
        Fps = _fps;
        Speed = _speed;
        if (!internalRender && _hasChildren) Renderer.Tick(this, Duration, true, internalRender, TickMode.Force);
        Reset(internalRender);
        if (_autoplay) Resume();
        return this;
    }

    public Timer ResetTime()
    {
        var engine = Presto.Engine;
        var timeScale = 1 / (_speed * engine._speed);
        _startTime = engine.Now - (_currentTime + _delay) * timeScale;
        return this;
    }

    public Timer Pause()
    {
        Presto.Engine.AssertThreadAffinity();
        if (Paused) return this;
        Paused = true;
        OnPause?.Invoke(this);
        return this;
    }

    public Timer Resume()
    {
        Presto.Engine.AssertThreadAffinity();
        if (!Paused) return this;
        Paused = false;
        if (Duration <= Constants.MinValue && !_hasChildren)
        {
            Renderer.Tick(this, Constants.MinValue, false, false, TickMode.Force);
        }
        else
        {
            var engine = Presto.Engine;
            if (!_running)
            {
                Helpers.AddChild(engine, this, SortByPriority);
                engine._hasChildren = true;
                _running = true;
            }
            ResetTime();
            // Forces the timer to advance by at least one frame when the next tick occurs.
            _startTime -= 12;
            engine.Wake();
        }
        return this;
    }

    public Timer Restart() => Reset().Resume();

    public Timer Seek(double time, bool muteCallbacks = false, bool internalRender = false)
    {
        Presto.Engine.AssertThreadAffinity();
        ReviveTimer(this);
        Completed = false;
        var isPaused = Paused;
        Paused = true;
        Renderer.Tick(this, time + _delay, muteCallbacks, internalRender, TickMode.Auto);
        return isPaused ? this : Resume();
    }

    /// <summary>Flips the playback direction in place, so the timer continues from its current position the other way.</summary>
    public Timer Alternate()
    {
        var reversed = _reversed;
        var count = IterationCount;
        var duration = IterationDuration;
        var iterations = double.IsPositiveInfinity(count) ? Math.Floor(Constants.MaxValue / duration) : count;
        _reversed = _alternate && iterations % 2 == 0 ? reversed : !reversed;
        if (double.IsPositiveInfinity(count))
        {
            IterationProgress = _reversed ? 1 - IterationProgress : IterationProgress;
        }
        else
        {
            Seek((duration * iterations) - _currentTime);
        }
        ResetTime();
        return this;
    }

    public Timer Play()
    {
        if (_reversed) Alternate();
        return Resume();
    }

    public Timer Reverse()
    {
        if (!_reversed) Alternate();
        return Resume();
    }

    public Timer Cancel()
    {
        if (_hasChildren) Helpers.ForEachChildren(this, static child => child.Cancel(), true);
        else TweenList.ForEachChildren(this, Composer.RemoveTweenSiblings);
        _cancelled = true;
        return Pause();
    }

    /// <summary>Rescales the total duration to <paramref name="newDuration"/>, scaling the iteration duration, delay and loop delay proportionally.</summary>
    public virtual Timer Stretch(double newDuration)
    {
        var currentDuration = Duration;
        var normalizedDuration = Helpers.NormalizeTime(newDuration);
        if (currentDuration == normalizedDuration) return this;
        var timeScale = newDuration / currentDuration;
        var isSetter = newDuration <= Constants.MinValue;
        Duration = isSetter ? Constants.MinValue : normalizedDuration;
        IterationDuration = isSetter ? Constants.MinValue : Helpers.NormalizeTime(IterationDuration * timeScale);
        _offset *= timeScale;
        _delay *= timeScale;
        _loopDelay *= timeScale;
        return this;
    }

    /// <summary>Seeks back to 0 without callbacks, then cancels.</summary>
    public virtual Timer Revert()
    {
        Renderer.Tick(this, 0, true, false, TickMode.Auto);
        return Cancel();
    }

    /// <summary>Seeks to the end, firing <see cref="OnComplete"/> unless muted, then cancels.</summary>
    public Timer Complete(bool muteCallbacks = false) => Seek(Duration, muteCallbacks).Cancel();

    /// <summary>Completes when the timer completes; an already completed timer resolves immediately.</summary>
    /// <param name="callback">Invoked synchronously on completion, before the task completes.</param>
    public Task<Timer> Then(Action<Timer>? callback = null)
    {
        if (Completed)
        {
            callback?.Invoke(this);
            return Task.FromResult(this);
        }
        // Awaiters must never resume inline: the resolving SetResult runs inside the render tick, on the frame pump's thread.
        _completion ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
        _onResolve += callback;
        return _completion.Task;
    }

    public TaskAwaiter<Timer> GetAwaiter() => Then().GetAwaiter();

    internal void Resolve()
    {
        var completion = _completion;
        if (completion == null) return;
        var onResolve = _onResolve;
        _completion = null;
        _onResolve = null;
        onResolve?.Invoke(this);
        completion.SetResult(this);
    }
}
