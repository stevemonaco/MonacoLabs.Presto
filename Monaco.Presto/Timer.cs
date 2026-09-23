using System.Runtime.CompilerServices;
using Monaco.Presto.Core;

namespace Monaco.Presto;

/// <summary>Base class of timers, animations and timelines: delay/duration/loop state machine, callbacks and playback controls.</summary>
public class Timer : Clock, IRevertible
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
    // Upstream reverts the ScrollObserver passed as `autoplay` from revert(); Presto's observer hooks itself here instead.
    internal Action<Timer>? _onRevert;
    internal int _priority;

    internal Timer(TimerParams parameters, Timer? parent, double parentPosition, Engine? engine = null) : base(0)
    {
        var timerId = Interlocked.Increment(ref s_timerId);

        engine = _engine = engine ?? parent?.OwnerEngine ?? Engine.Current;
        engine.CurrentScope?.Register(this);

        var timerInitTime = parent != null ? 0 : engine._lastTickTime;
        var timerDefaults = parent?.ChildDefaults ?? engine.Defaults;
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

        Id = parameters.Id ?? timerId.ToString();
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

    /// <inheritdoc cref="TimerParams.Id"/>
    public string Id { get; internal set; }

    /// <summary>The timeline this timer was added to, or null when it runs directly on the engine.</summary>
    public Timer? Parent { get; }

    /// <summary>The defaults the children of this timer inherit; a timeline overrides it with its own.</summary>
    internal virtual PrestoDefaults ChildDefaults => OwnerEngine.Defaults;

    /// <summary>Total duration including every iteration and loop delay.</summary>
    public double Duration { get; internal set; }

    /// <summary>Duration of a single iteration.</summary>
    public double IterationDuration { get; internal set; }

    /// <summary>Number of iterations; <see cref="double.PositiveInfinity"/> when looping forever.</summary>
    public double IterationCount { get; internal set; }

    /// <summary>True while the playhead is moving towards 0, combining <see cref="Reversed"/>, alternating iterations and the parent's direction.</summary>
    public bool Backwards { get; internal set; }

    /// <summary>True while the timer is not being advanced; it keeps its position until <see cref="Resume"/>.</summary>
    public bool Paused { get; internal set; }

    /// <summary>True once the playhead has moved past the delay and <see cref="OnBegin"/> has fired.</summary>
    public bool Began { get; internal set; }

    /// <summary>True once the last iteration has finished and <see cref="OnComplete"/> has fired; an infinite timer never sets it.</summary>
    public bool Completed { get; internal set; }

    /// <summary>Called once when the timer passes its delay and starts playing (upstream <c>onBegin</c>).</summary>
    public Action<Timer>? OnBegin { get; internal set; }

    /// <summary>Called every frame before the tweens are rendered (upstream <c>onBeforeUpdate</c>).</summary>
    public Action<Timer>? OnBeforeUpdate { get; internal set; }

    /// <summary>Called every frame after the tweens are rendered (upstream <c>onUpdate</c>).</summary>
    public Action<Timer>? OnUpdate { get; internal set; }

    /// <summary>Called each time an iteration ends and the next one begins (upstream <c>onLoop</c>).</summary>
    public Action<Timer>? OnLoop { get; internal set; }

    /// <summary>Called when <see cref="Pause"/> pauses a running timer; reaching the end does not fire it (upstream <c>onPause</c>).</summary>
    public Action<Timer>? OnPause { get; internal set; }

    /// <summary>Called once when the last iteration finishes; an infinite timer never fires it (upstream <c>onComplete</c>).</summary>
    public Action<Timer>? OnComplete { get; internal set; }

    /// <summary>True once <see cref="Cancel"/> has detached the timer; setting it false revives it and plays it from the start.</summary>
    public bool Cancelled
    {
        get => _cancelled;
        set
        {
            if (value) Cancel(); else Reset(true).Play();
        }
    }

    /// <summary>Position of the playhead across every iteration, from minus the delay to <see cref="Duration"/>; setting it seeks without changing whether the timer is playing.</summary>
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

    /// <summary>Position of the playhead inside the current iteration, from 0 to <see cref="IterationDuration"/>; setting it seeks.</summary>
    public double IterationCurrentTime
    {
        get => Helpers.Clamp(Helpers.Round(_iterationTime, Globals.Precision), 0, IterationDuration);
        set => CurrentTime = (IterationDuration * _currentIteration) + value;
    }

    /// <summary>Position of the playhead across every iteration, from 0 to 1; setting it seeks.</summary>
    public double Progress
    {
        get => Helpers.Clamp(Helpers.Round(_currentTime / Duration, 10), 0, 1);
        set => CurrentTime = Duration * value;
    }

    /// <summary>Position of the playhead inside the current iteration, from 0 to 1; setting it seeks.</summary>
    public double IterationProgress
    {
        get => Helpers.Clamp(Helpers.Round(_iterationTime / IterationDuration, 10), 0, 1);
        set
        {
            var iterationDuration = IterationDuration;
            CurrentTime = (iterationDuration * _currentIteration) + (iterationDuration * value);
        }
    }

    /// <summary>Zero based index of the iteration being played; setting it seeks to the start of that iteration.</summary>
    public int CurrentIteration
    {
        get => _currentIteration;
        set => CurrentTime = IterationDuration * Helpers.Clamp(value, 0, IterationCount - 1);
    }

    /// <summary>Whether the playhead runs towards 0; setting it calls <see cref="Reverse"/> or <see cref="Play"/>, so it also resumes the timer.</summary>
    public bool Reversed
    {
        get => _reversed;
        set
        {
            if (value) Reverse(); else Play();
        }
    }

    /// <summary>This timer's playback rate, multiplied by <see cref="Engine.Speed"/> (upstream <c>playbackRate</c>); setting it rebases the start time so the playhead does not jump.</summary>
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

    /// <summary>Rewinds to 0 and clears the began, completed and cancelled state, leaving the timer paused (upstream <c>timer.reset()</c>).</summary>
    /// <param name="softReset">Rewinds the timing state without writing the start values back to the targets.</param>
    public Timer Reset(bool softReset = false)
    {
        OwnerEngine.AssertThreadAffinity();
        ReviveTimer(this);
        if (_reversed && !_reverse) Reversed = false;
        _iterationTime = IterationDuration;
        Renderer.Tick(this, 0, true, softReset, TickMode.Force);
        ResetTimerProperties(this);
        if (_hasChildren) Helpers.ForEachChildren(this, ResetTimerProperties);
        return this;
    }

    /// <summary>Applies the frame and playback rates, renders the initial state and starts playing when autoplay is on; the <see cref="Presto"/> factories call it (upstream <c>timer.init()</c>).</summary>
    /// <param name="internalRender">Skips the one-off render of a timeline's children at their end state and leaves the targets untouched.</param>
    public virtual Timer Init(bool internalRender = false)
    {
        Fps = _fps;
        Speed = _speed;
        if (!internalRender && _hasChildren) Renderer.Tick(this, Duration, true, internalRender, TickMode.Force);
        Reset(internalRender);
        if (_autoplay) Resume();
        return this;
    }

    /// <summary>Rebases the start time against the engine clock so the timer carries on from <see cref="CurrentTime"/> instead of jumping (upstream <c>timer.resetTime()</c>).</summary>
    public Timer ResetTime()
    {
        var engine = OwnerEngine;
        var timeScale = 1 / (_speed * engine._speed);
        _startTime = engine.Now - (_currentTime + _delay) * timeScale;
        return this;
    }

    /// <summary>Stops advancing the timer where it stands and fires <see cref="OnPause"/> (upstream <c>timer.pause()</c>).</summary>
    public Timer Pause()
    {
        OwnerEngine.AssertThreadAffinity();
        if (Paused) return this;
        Paused = true;
        OnPause?.Invoke(this);
        return this;
    }

    /// <summary>Carries on from the current position in the current direction, re-registering with the engine (upstream <c>timer.resume()</c>).</summary>
    public Timer Resume()
    {
        OwnerEngine.AssertThreadAffinity();
        if (!Paused) return this;
        Paused = false;
        if (Duration <= Constants.MinValue && !_hasChildren)
        {
            Renderer.Tick(this, Constants.MinValue, false, false, TickMode.Force);
        }
        else
        {
            var engine = OwnerEngine;
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

    /// <summary>Rewinds to 0 and plays from there (upstream <c>timer.restart()</c>).</summary>
    public Timer Restart() => Reset().Resume();

    /// <summary>Moves the playhead and renders there, leaving the timer paused or playing as it was (upstream <c>timer.seek()</c>).</summary>
    /// <param name="time">Position to move to, measured from the end of the delay.</param>
    /// <param name="muteCallbacks">Suppresses the callbacks the jump would otherwise fire, including those of the iterations skipped over.</param>
    /// <param name="internalRender">Updates the timing state without writing values to the targets.</param>
    public Timer Seek(double time, bool muteCallbacks = false, bool internalRender = false)
    {
        OwnerEngine.AssertThreadAffinity();
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

    /// <summary>Resumes playing forwards, flipping the direction first when the timer was reversed (upstream <c>timer.play()</c>).</summary>
    public Timer Play()
    {
        if (_reversed) Alternate();
        return Resume();
    }

    /// <summary>Resumes playing backwards, flipping the direction first when the timer was not already reversed (upstream <c>timer.reverse()</c>).</summary>
    public Timer Reverse()
    {
        if (!_reversed) Alternate();
        return Resume();
    }

    /// <summary>Pauses and detaches the tweens from their targets, leaving the animated values where they stand; <see cref="Reset"/> and <see cref="Seek"/> revive it (upstream <c>timer.cancel()</c>).</summary>
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
        _onRevert?.Invoke(this);
        return Cancel();
    }

    void IRevertible.Revert() => Revert();

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

    /// <summary>Makes the timer directly awaitable, so <c>await timer</c> stands in for upstream's <c>await timer.then()</c>.</summary>
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
