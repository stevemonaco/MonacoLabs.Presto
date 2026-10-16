using System.Diagnostics;
using System.Runtime.CompilerServices;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto;

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
    internal bool _canceled;
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
    internal MotionValue? _progressValue;
    private MotionValue? _scrub;
    private Action<MotionValue>? _onScrub;
    private bool _scrubRelative;
    private double _scrubProgress;
    private double _scrubLast;
    private TimerSettle? _settle;
    internal int _priority;
    internal TimerDiagnostics? _diagnostics;
    internal readonly object? _hostLifetime;

    internal Timer(TimerParams parameters, Timer? parent, double parentPosition, Engine? engine = null) : base(0)
    {
        var timerId = Interlocked.Increment(ref s_timerId);

        engine = _engine = engine ?? parent?.OwnerEngine ?? Engine.Current;
        if (engine.Diagnostics.Enabled && parent == null)
        {
            var diagnostics = engine.Diagnostics.Register(this);
            diagnostics.Origin = PrestoDiagnostics.CurrentOrigin;
            if (PrestoDiagnostics.CaptureCreationTrace) diagnostics.CreationTrace = new StackTrace(1, true);
        }
        // After the diagnostics register, so the scope finds them to record itself on.
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

        Id = parameters.Id ?? timerId.ToString();
        Parent = parent;
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
        UpdateDuration();
        _reversed = parameters.Reversed ?? timerDefaults.Reversed;
        _reverse = _reversed;
        _alternate = parameters.Alternate ?? timerDefaults.Alternate;

        _lastTickTime = timerInitTime;
        _startTime = timerInitTime;
        _lastTime = timerInitTime;
        _fps = parameters.FrameRate ?? timerDefaults.FrameRate;
        _speed = parameters.PlaybackRate ?? timerDefaults.PlaybackRate;
        _priority = parameters.Priority;
        _hostLifetime = parameters.HostLifetime;
    }

    /// <inheritdoc cref="TimerParams.Id"/>
    public string Id { get; internal set; }

    /// <summary>The timeline this timer was added to, or null when it runs directly on the engine.</summary>
    public Timer? Parent { get; }

    /// <summary>The engine this timer runs on.</summary>
    public Engine Engine => OwnerEngine;

    /// <summary>Wait before the first iteration, as <see cref="TimerParams.Delay"/> resolved.</summary>
    public double Delay => _delay;

    /// <summary>Wait between iterations, as <see cref="TimerParams.LoopDelay"/> resolved.</summary>
    public double LoopDelay => _loopDelay;

    /// <summary>Whether every other iteration plays backwards (<see cref="TimerParams.Alternate"/>); <see cref="Alternate()"/> is the method that flips direction.</summary>
    public bool IsAlternating => _alternate;

    /// <summary>Start position in the parent timeline, or for a root timer the engine time it was created at.</summary>
    public double Offset => _offset;

    /// <summary>The ease applied to the whole playhead (upstream <c>playbackEase</c>), or null.</summary>
    public Ease? PlaybackEase => _ease;

    /// <inheritdoc cref="TimerParams.Priority"/>
    public int Priority => _priority;

    /// <summary>True while the timer is linked into the engine's list of ticked timers.</summary>
    public bool IsScheduled => _running;

    /// <summary>The timeline children in order, enumerated without allocating; empty for a timer that is not a timeline.</summary>
    public TimerChildren Children => new(this);

    /// <summary>The tweens in order, enumerated without allocating.</summary>
    public TimerTweens Tweens => new(this);

    /// <summary>The bridge feature or core part that created this timer's root, as tagged by <see cref="PrestoDiagnostics.Origin"/>; null for app code or while diagnostics were off.</summary>
    public string? Origin => _diagnostics?.Origin ?? Parent?.Origin;

    /// <summary>Where this root timer was created, recorded while <see cref="PrestoDiagnostics.CaptureCreationTrace"/> and diagnostics were on.</summary>
    public StackTrace? CreationTrace => _diagnostics?.CreationTrace;

    /// <summary>The scope this root timer registered with while diagnostics were on.</summary>
    public Scope? Scope => _diagnostics?.Scope;

    /// <summary>The defaults the children of this timer inherit; a timeline overrides it with its own.</summary>
    internal virtual PrestoDefaults ChildDefaults => OwnerEngine.Defaults;

    /// <summary>Total duration including every iteration and loop delay.</summary>
    public double Duration { get; internal set; }

    /// <summary>Duration of a single iteration.</summary>
    public double IterationDuration { get; internal set; }

    /// <summary>Number of iterations; <see cref="double.PositiveInfinity"/> when looping forever.</summary>
    public double IterationCount { get; internal set; }

    /// <summary>Iterations to play beyond the first, as <see cref="TimerParams.Loop"/>; setting it recomputes <see cref="IterationCount"/> and <see cref="Duration"/> in place.</summary>
    /// <remarks>
    /// A running timer lowered below its current iteration completes on the next tick, a paused one once it plays again;
    /// one given more loops keeps playing them.
    /// A parent timeline grows to fit a child that now runs past its end, but keeps its duration when the child gets shorter.
    /// </remarks>
    public LoopCount Loop
    {
        get => double.IsPositiveInfinity(IterationCount) ? LoopCount.Infinite : (int)IterationCount - 1;
        set
        {
            IterationCount = value.IsInfinite ? double.PositiveInfinity : value.Value + 1;
            UpdateDuration();
            // Just short of the end: a playhead already at it would complete on the next tick without rendering or updating.
            if (Paused && _currentTime > Duration) Seek(Math.BitDecrement(Duration), muteCallbacks: true);
            (Parent as Timeline)?.FitChildren();
        }
    }

    /// <summary>True while the playhead is moving towards 0, combining <see cref="Reversed"/>, alternating iterations and the parent's direction.</summary>
    public bool Backwards { get; internal set; }

    /// <summary>True while the timer is not being advanced; it keeps its position until <see cref="Resume"/>.</summary>
    public bool Paused { get; internal set; }

    /// <summary>True once the playhead has moved past the delay and <see cref="OnBegin"/> has fired.</summary>
    public bool Began { get; internal set; }

    /// <summary>True once the last iteration has finished and <see cref="OnComplete"/> has fired; an infinite timer never sets it.</summary>
    public bool Completed { get; internal set; }

    /// <summary>Called once when the timer passes its delay and starts playing (upstream <c>onBegin</c>).</summary>
    public Action<Timer>? OnBegin { get; set; }

    /// <summary>Called every frame before the tweens are rendered (upstream <c>onBeforeUpdate</c>).</summary>
    public Action<Timer>? OnBeforeUpdate { get; set; }

    /// <summary>Called every frame after the tweens are rendered (upstream <c>onUpdate</c>).</summary>
    public Action<Timer>? OnUpdate { get; set; }

    /// <summary>Called each time an iteration ends and the next one begins (upstream <c>onLoop</c>).</summary>
    public Action<Timer>? OnLoop { get; set; }

    /// <summary>Called when <see cref="Pause"/> pauses a running timer; reaching the end does not fire it (upstream <c>onPause</c>).</summary>
    public Action<Timer>? OnPause { get; set; }

    /// <summary>Called once when the last iteration finishes; an infinite timer never fires it (upstream <c>onComplete</c>).</summary>
    public Action<Timer>? OnComplete { get; set; }

    /// <summary>True once <see cref="Cancel"/> has detached the timer; setting it false revives it and plays it from the start.</summary>
    public bool Canceled
    {
        get => _canceled;
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
        set => CurrentTime = IterationPeriod * _currentIteration + value;
    }

    /// <summary>The rendered position inside the current iteration, after <see cref="Reversed"/>, alternation and the playback ease are applied.</summary>
    public double IterationTime => Helpers.Round(_iterationTime, Globals.Precision);

    /// <summary>Position of the playhead across every iteration, from 0 to 1; setting it seeks.</summary>
    public double Progress
    {
        get => Helpers.Clamp(Helpers.Round(_currentTime / Duration, 10), 0, 1);
        set => CurrentTime = Duration * value;
    }

    /// <summary>A <see cref="MotionValue"/> holding <see cref="Progress"/>, updated whenever the timer renders.</summary>
    public MotionValue ProgressValue => _progressValue ??= new(OwnerEngine, [], Progress);

    /// <summary>Position of the playhead inside the current iteration, from 0 to 1; setting it seeks.</summary>
    public double IterationProgress
    {
        get => Helpers.Clamp(Helpers.Round(_iterationTime / IterationDuration, 10), 0, 1);
        set => CurrentTime = IterationPeriod * _currentIteration + IterationDuration * value;
    }

    /// <summary>Zero based index of the iteration being played; setting it seeks to the start of that iteration.</summary>
    public int CurrentIteration
    {
        get => _currentIteration;
        set => CurrentTime = IterationPeriod * Helpers.Clamp(value, 0, IterationCount - 1);
    }

    private double IterationPeriod => IterationDuration + _loopDelay;

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
        if (!timer._canceled) return;
        if (timer._hasChildren)
        {
            Helpers.ForEachChildren(timer, ReviveTimer);
        }
        else
        {
            TweenList.ForEachChildren(timer, static tween =>
            {
                if (tween._composition != Composition.Overlap) Composer.ComposeTween(tween, Composer.GetTweenSiblings(tween.Target, tween.Property, tween.Parent.OwnerEngine));
            });
        }
        timer._canceled = false;
    }

    internal void UpdateDuration()
    {
        var duration = Helpers.ClampInfinity(((IterationDuration + _loopDelay) * IterationCount) - _loopDelay);
        Duration = duration == 0 || double.IsNaN(duration) ? Constants.MinValue : duration;
    }

    private static bool SortByPriority(Timer prev, Timer child) => prev._priority > child._priority;

    /// <summary>Rewinds to 0 and clears the began, completed and canceled state, leaving the timer paused (upstream <c>timer.reset()</c>).</summary>
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
        Announce();
        return this;
    }

    /// <summary>Rebases the start time against the engine clock so the timer carries on from <see cref="CurrentTime"/> instead of jumping (upstream <c>timer.resetTime()</c>).</summary>
    public Timer ResetTime() => ResetTime(OwnerEngine.Now);

    internal Timer ResetTime(double now)
    {
        var timeScale = 1 / (_speed * OwnerEngine._speed);
        _startTime = now - (_currentTime + _delay) * timeScale;
        return this;
    }

    /// <summary>Stops advancing the timer where it stands and fires <see cref="OnPause"/> (upstream <c>timer.pause()</c>).</summary>
    public Timer Pause()
    {
        EndSettle();
        return PauseTimer();
    }

    private Timer PauseTimer()
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
        EndSettle();
        if (!Paused) return this;
        Paused = false;
        if (Duration <= Constants.MinValue && _delay <= Constants.MinValue && !_hasChildren)
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
        Announce();
        return this;
    }

    private void Announce()
    {
        if (_diagnostics is not { Announced: false } diagnostics) return;
        diagnostics.Announced = true;
        OwnerEngine.Diagnostics.Announce(this);
    }

    /// <summary>Rewinds to 0 and plays from there (upstream <c>timer.restart()</c>).</summary>
    public Timer Restart()
    {
        EndSettle();
        return Reset().Resume();
    }

    /// <summary>Moves the playhead and renders there, leaving the timer paused or playing as it was (upstream <c>timer.seek()</c>).</summary>
    /// <param name="time">Position to move to, measured from the end of the delay.</param>
    /// <param name="muteCallbacks">Suppresses the callbacks the jump would otherwise fire, including those of the iterations skipped over.</param>
    /// <param name="internalRender">Updates the timing state without writing values to the targets.</param>
    public Timer Seek(double time, bool muteCallbacks = false, bool internalRender = false)
    {
        EndSettle();
        return SeekTime(time, muteCallbacks, internalRender);
    }

    /// <summary>The seek a running <see cref="Settle"/> drives the playhead through, which leaves the settle running.</summary>
    internal Timer SeekTime(double time, bool muteCallbacks = false, bool internalRender = false)
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
        if (_diagnostics != null && !_canceled) OwnerEngine.Diagnostics.Ended(this);
        Scrub(null);
        return DetachTweens();
    }

    /// <summary>The engine's cancel of a completed timer, which leaves a <see cref="Scrub"/> running so it can seek back.</summary>
    internal Timer DetachTweens()
    {
        if (_hasChildren) Helpers.ForEachChildren(this, static child => child.Cancel(), true);
        // Unlike upstream, a repeat cancel leaves composition alone: the tweens already left their sibling lists.
        else if (!_canceled) TweenList.ForEachChildren(this, Composer.RemoveTweenSiblings);
        _canceled = true;
        return PauseTimer();
    }

    /// <summary>
    /// Pauses the timer and seeks it to <paramref name="progress"/> times <see cref="Duration"/>, then again on every frame the value changes,
    /// like a scroll-synced timer driven by any source. <c>null</c>, <see cref="Cancel"/>, <see cref="Revert"/> and <see cref="Settle"/> end the scrub.
    /// </summary>
    /// <param name="progress">The value whose changes drive the playhead; null ends the scrub.</param>
    /// <param name="relative">Keep the current progress and apply the value's later changes as deltas, clamped to [0, 1].</param>
    /// <exception cref="InvalidOperationException"><paramref name="progress"/> is not null and the timer has a <see cref="Parent"/>.</exception>
    public Timer Scrub(MotionValue? progress, bool relative = false)
    {
        if (progress != null && Parent != null) throw new InvalidOperationException(ChildSettleMessage);
        EndSettle();
        if (_scrub != null) _scrub.Changed -= _onScrub;
        _scrub = progress;
        if (progress == null) return this;
        // The first read only starts measuring, so a later Settle reads the drag's real velocity.
        _ = progress.Velocity;
        _scrubRelative = relative;
        _scrubProgress = Progress;
        _scrubLast = progress.Value;
        _onScrub ??= OnScrub;
        progress.Changed += _onScrub;
        Pause();
        return relative ? this : Seek(progress.Value * Duration);
    }

    private void OnScrub(MotionValue value)
    {
        if (!_scrubRelative)
        {
            Seek(value.Value * Duration);
            return;
        }
        _scrubProgress = Helpers.Clamp(_scrubProgress + value.Value - _scrubLast, 0, 1);
        _scrubLast = value.Value;
        Seek(_scrubProgress * Duration);
    }

    private const string ChildSettleMessage = "A child timer follows its parent; scrub or settle the parent timeline instead.";

    /// <summary>
    /// Ends any scrub, pauses the timer and springs its playhead from <see cref="Progress"/> to a resting progress chosen from the release
    /// velocity, seeking to the spring's value, clamped to [0, 1], times <see cref="Duration"/> on every frame it moves.
    /// </summary>
    /// <returns>A task that completes with the target once the spring rests on it, or with <see cref="Progress"/> when a playhead control or a new settle ends this one; it never faults.
    /// Under <see cref="PrestoDefaults.ReducedMotion"/> it seeks to the target at once and returns a completed task.</returns>
    /// <exception cref="InvalidOperationException">The timer has a <see cref="Parent"/>, or its <see cref="Duration"/> is infinite.</exception>
    /// <exception cref="ArgumentException"><paramref name="settings"/> has empty or non-finite points, or a negative or non-finite projection or flick velocity.</exception>
    public Task<double> Settle(SettleParams? settings = null)
    {
        if (Parent != null) throw new InvalidOperationException(ChildSettleMessage);
        if (Duration >= Constants.MaxValue) throw new InvalidOperationException("An infinitely looping timer has no end to settle on.");
        settings ??= new();
        TimerSettle.Validate(settings);
        var velocity = settings.Velocity ?? _scrub?.Velocity ?? 0;
        Scrub(null);
        Pause();
        var target = TimerSettle.Choose(Progress, velocity, TimerSettle.RestingPoints(this, settings), settings.Projection, settings.FlickVelocity);
        if (OwnerEngine.Defaults.ReducedMotion)
        {
            Seek(target * Duration);
            return Task.FromResult(target);
        }
        _settle = new TimerSettle(this, settings.Spring ?? Springs.Gentle, target, velocity * Globals.TimeScale * Constants.K);
        return _settle.Task;
    }

    private void EndSettle()
    {
        if (_settle == null) return;
        _settle.End(Progress);
        _settle = null;
    }

    /// <summary>Rescales the total duration to <paramref name="newDuration"/>, scaling the iteration duration, delay and loop delay proportionally.</summary>
    /// <remarks><see cref="CurrentTime"/> stays where it was and <see cref="Progress"/> is not preserved.</remarks>
    public virtual Timer Stretch(double newDuration)
    {
        var currentDuration = Duration;
        var normalizedDuration = Helpers.NormalizeTime(newDuration);
        if (currentDuration == normalizedDuration) return this;
        var timeScale = newDuration / currentDuration;
        var isSetter = newDuration <= Constants.MinValue;
        Duration = isSetter ? Constants.MinValue : normalizedDuration;
        IterationDuration = isSetter ? Constants.MinValue : Helpers.NormalizeTime(IterationDuration * timeScale);
        // Deviates from upstream: a top level offset is the engine time the timer was created at, not a position in a parent,
        // and stretching from a setter's 1e-11 duration would scale it past the precision a frame needs.
        if (Parent != null) _offset *= timeScale;
        _delay *= timeScale;
        _loopDelay *= timeScale;
        return this;
    }

    /// <summary>Seeks back to 0 without callbacks, then cancels.</summary>
    public virtual Timer Revert()
    {
        EndSettle();
        Renderer.Tick(this, 0, true, false, TickMode.Auto);
        _onRevert?.Invoke(this);
        return Cancel();
    }

    void IRevertible.Revert() => Revert();

    /// <summary>Seeks to the end, firing <see cref="OnComplete"/> unless muted, then cancels without firing <see cref="OnPause"/>.</summary>
    public Timer Complete(bool muteCallbacks = false)
    {
        // Paused first so the seek does not resume the timer only for Cancel to pause it again.
        Paused = true;
        return Seek(Duration, muteCallbacks).Cancel();
    }

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

    /// <summary>Makes the timer directly awaitable: <c>await timer</c> resumes once it completes, as <see cref="Then"/> does.</summary>
    public TaskAwaiter<Timer> GetAwaiter() => Then().GetAwaiter();

    internal void Resolve()
    {
        var completion = _completion;
        if (completion == null) return;
        var onResolve = _onResolve;
        _completion = null;
        _onResolve = null;
        try
        {
            onResolve?.Invoke(this);
        }
        finally
        {
            completion.SetResult(this);
        }
    }
}
