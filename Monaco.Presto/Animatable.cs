using Monaco.Presto.Core;
using Monaco.Presto.Values;

namespace Monaco.Presto;

/// <summary>
/// One paused animation per property, retargeted imperatively. Port of anime.js <c>createAnimatable</c>: <c>Set("X", 100)</c>
/// aims the property's tween at a new value from wherever it currently is and resumes it, so a stream of values — a cursor
/// position, a scroll offset — is followed with that property's own duration and ease.
/// </summary>
/// <remarks>C# has no per-property methods, so upstream's <c>animatable.x(100)</c> is <c>Set("X", 100)</c> and <c>animatable.x()</c> is <c>Get("X")</c>.</remarks>
public sealed class Animatable : IRevertible
{
    private readonly List<object> _targets = [];
    private const double VelocityStep = 1e-4;

    private readonly Dictionary<string, Animation> _animations = [];
    // The properties that keep their momentum, each with the spring it was declared with, if any.
    private readonly Dictionary<string, Spring?> _momentum = [];
    // The eases and duration a flung property returns to on its next Set.
    private readonly Dictionary<string, (List<Ease> Eases, double Duration)> _flung = [];
    private static readonly Inertia DefaultInertia = new(0);
    private static readonly Ease AtRest = static _ => 0;
    private readonly Animation _callbacks;

    internal Animatable(object? targets, AnimatableParams parameters)
    {
        Engine.Current.CurrentScope?.Register(this);

        _callbacks = Presto.Animate(new CallbacksTarget(), new AnimationParams
        {
            ["V"] = 1,
            Autoplay = false,
            OnBegin = Wrap(parameters.OnBegin),
            OnUpdate = Wrap(parameters.OnUpdate),
            OnComplete = Wrap(parameters.OnComplete),
        });

        foreach (var (property, settings) in parameters.Properties)
        {
            var animation = Presto.Animate(targets, new AnimationParams
            {
                // The relative zero starts and ends the tween on the target's current value, which is what carries its unit over.
                [property] = new TweenParams
                {
                    To = "+=0" + settings.Unit,
                    Duration = settings.Duration,
                    Ease = settings.Ease,
                    Modifier = settings.Modifier,
                    Composition = settings.Composition,
                },
                Duration = parameters.Duration,
                Ease = parameters.Ease,
                Modifier = parameters.Modifier,
                Composition = Composition.Replace,
                PlaybackRate = parameters.PlaybackRate,
                FrameRate = parameters.FrameRate,
                Autoplay = false,
                OnBegin = HandleBegin,
                OnComplete = HandlePause,
                OnPause = HandlePause,
            });
            if (_targets.Count == 0) _targets.AddRange(animation.Targets);
            _animations[Key(property)] = animation;
            if (settings.PreserveVelocity ?? parameters.PreserveVelocity ?? false)
            {
                _momentum[Key(property)] = (settings.Ease ?? parameters.Ease)?.Raw as Spring;
            }
        }
    }

    /// <summary>The canonical name of <paramref name="property"/>, so a property declared by one spelling is read and set by any.</summary>
    private string Key(string property) => _targets.Count == 0 ? property : Presto.Canonicalise(_targets[0], property);

    /// <summary>The targets every property animation shares, empty once <see cref="Revert"/> has run.</summary>
    public IReadOnlyList<object> Targets => _targets;

    /// <summary>The paused animation behind each property, keyed by the property's canonical name.</summary>
    public IReadOnlyDictionary<string, Animation> Animations => _animations;

    /// <summary>The property's current value, or 0 once <see cref="Revert"/> has run.</summary>
    public double Get(string property) =>
        _animations.TryGetValue(Key(property), out var animation) && animation._headTween is { } tween ? tween._modifier(tween._number) : 0;

    /// <summary>The channels of a colour or complex property, empty for a scalar one.</summary>
    public double[] GetChannels(string property) =>
        _animations.TryGetValue(Key(property), out var animation) && animation._headTween is { } tween ? tween._numbers : [];

    /// <summary>Animates the property from where it is now to <paramref name="to"/>; a no-op after <see cref="Revert"/>.</summary>
    /// <returns>This animatable, so calls chain.</returns>
    public Animatable Set(string property, double to, double? duration = null, EaseValue? ease = null)
    {
        var key = Key(property);
        if (!_animations.TryGetValue(key, out var animation)) return this;
        _flung.Remove(key, out var flung);
        if (_momentum.TryGetValue(key, out var declared) && (ease is { } given ? given.Raw as Spring : declared) is { } spring)
        {
            return CarryMomentum(animation, spring, to, duration);
        }
        var index = 0;
        for (var tween = animation._headTween; tween != null; tween = tween._next)
        {
            tween._fromNumber = tween._modifier(tween._number);
            tween._toNumber = to;
            if (ease is { } easeValue) tween._ease = easeValue.Resolve(tween.Target, 0, 1, out _);
            else if (flung.Eases is { } eases) tween._ease = eases[index++];
            tween._channelEases = null;
            tween._currentTime = 0;
        }
        return Replay(animation, duration ?? (flung.Eases != null ? flung.Duration : null));
    }

    /// <summary>Coasts the property from where it is now at <paramref name="velocity"/> units per engine-time unit, decaying as <paramref name="settings"/> describes.</summary>
    /// <remarks>The inertia replaces the property's ease and duration until the next <see cref="Set(string, double, double?, EaseValue?)"/>.</remarks>
    public Animatable Fling(string property, double velocity, Inertia? settings = null) => Coast(property, velocity, settings);

    /// <summary>Coasts the property from its current velocity, or from rest when it is not moving, decaying as <paramref name="settings"/> describes.</summary>
    /// <remarks>The inertia replaces the property's ease and duration until the next <see cref="Set(string, double, double?, EaseValue?)"/>.</remarks>
    public Animatable Fling(string property, Inertia? settings = null) => Coast(property, null, settings);

    private Animatable Coast(string property, double? velocity, Inertia? settings)
    {
        var key = Key(property);
        if (!_animations.TryGetValue(key, out var animation)) return this;
        settings ??= DefaultInertia;
        var eases = _flung.ContainsKey(key) ? null : new List<Ease>();
        var span = 0d;
        for (var tween = animation._headTween; tween != null; tween = tween._next)
        {
            var from = tween._modifier(tween._number);
            var plan = InertiaSolver.Resolve(from, velocity ?? (animation.Paused ? 0 : Velocity(tween, tween._ease, tween._toNumber - tween._fromNumber)), settings);
            var (ease, length) = (plan.Ease, plan.Duration);
            // Released on a bound moving outward: out to the peak and back, as one tween from the bound to the peak.
            if (plan.Then is { } back)
            {
                var (outward, split) = (plan.Ease, plan.Duration / (plan.Duration + back.Duration));
                ease = u => u < split ? outward(u / split) : 1 - back.Ease((u - split) / (1 - split));
                length += back.Duration;
            }
            eases?.Add(tween._ease);
            span = Math.Max(span, length);
            // Captures span itself, so every tween samples over the longest plan once the loop has seen them all.
            tween._ease = u => ease(Math.Min(1, u * span / length));
            tween._channelEases = null;
            tween._fromNumber = from;
            tween._toNumber = plan.To;
            tween._currentTime = 0;
        }
        if (eases != null) _flung.Add(key, (eases, animation.Duration));
        return Replay(animation, span);
    }

    /// <inheritdoc cref="Set(string, double, double?, EaseValue?)"/>
    /// <remarks>The channel form, for the colour and complex properties <see cref="GetChannels"/> reads.</remarks>
    public Animatable Set(string property, IReadOnlyList<double> to, double? duration = null, EaseValue? ease = null)
    {
        var key = Key(property);
        if (!_animations.TryGetValue(key, out var animation)) return this;
        if (_momentum.TryGetValue(key, out var declared) && (ease is { } given ? given.Raw as Spring : declared) is { } spring)
        {
            return CarryChannelMomentum(animation, spring, to, duration);
        }
        for (var tween = animation._headTween; tween != null; tween = tween._next)
        {
            for (var i = 0; i < to.Count && i < tween._numbers.Length; i++)
            {
                tween._fromNumbers[i] = tween._modifier(tween._numbers[i]);
                tween._toNumbers[i] = to[i];
            }
            if (ease is { } easeValue) tween._ease = easeValue.Resolve(tween.Target, 0, 1, out _);
            tween._channelEases = null;
            tween._currentTime = 0;
        }
        return Replay(animation, duration);
    }

    /// <summary>Reverts every property to the value it held before the animatable ran and leaves <see cref="Set(string, double, double?, EaseValue?)"/> a no-op.</summary>
    public Animatable Revert()
    {
        foreach (var animation in _animations.Values) animation.Revert();
        _animations.Clear();
        _targets.Clear();
        _callbacks.Revert();
        return this;
    }

    void IRevertible.Revert() => Revert();

    private Animatable Replay(Animation animation, double? duration)
    {
        if (duration is { } newDuration) animation.Stretch(newDuration);
        animation.Reset(true).Resume();
        return this;
    }

    /// <summary>
    /// Retargets every tween of <paramref name="animation"/> with a copy of <paramref name="spring"/> that starts at the tween's
    /// current velocity. Each copy settles in its own time, so all of them are sampled over the longest one's solver span.
    /// </summary>
    private Animatable CarryMomentum(Animation animation, Spring spring, double to, double? duration)
    {
        var springs = new List<Spring>();
        var span = 0d;
        var settling = 0d;
        for (var tween = animation._headTween; tween != null; tween = tween._next)
        {
            var from = tween._modifier(tween._number);
            var distance = to - from;
            // The solver's velocity is progress per solver second, which is its unit of time scaled back to the engine's.
            var velocity = animation.Paused || distance == 0 ? 0 : Velocity(tween, tween._ease, tween._toNumber - tween._fromNumber) * Constants.K * Globals.TimeScale / distance;
            var thrown = new Spring(spring.Mass, spring.Stiffness, spring.Damping, velocity);
            springs.Add(thrown);
            span = Math.Max(span, thrown.SolverDuration);
            settling = Math.Max(settling, thrown.SettlingDuration);
            tween._fromNumber = from;
            tween._toNumber = to;
            tween._currentTime = 0;
        }

        var index = 0;
        for (var tween = animation._headTween; tween != null; tween = tween._next)
        {
            var thrown = springs[index++];
            tween._ease = t => t >= 1 ? 1 : thrown.Solve(t * span);
            tween._channelEases = null;
        }
        return Replay(animation, duration ?? settling);
    }

    /// <summary>
    /// The channel form of <see cref="CarryMomentum"/>: each channel of each tween gets its own thrown spring. Colour RGB
    /// channels carry their velocity in the squared space the renderer blends them in; a channel that does not move stays at rest.
    /// </summary>
    private Animatable CarryChannelMomentum(Animation animation, Spring spring, IReadOnlyList<double> to, double? duration)
    {
        var span = 0d;
        var settling = 0d;
        for (var tween = animation._headTween; tween != null; tween = tween._next)
        {
            var eases = new Ease[tween._numbers.Length];
            for (var i = 0; i < eases.Length; i++)
            {
                var squared = tween._valueType == ValueKind.Color && i < 3;
                var from = tween._modifier(tween._numbers[i]);
                var target = i < to.Count ? to[i] : from;
                var distance = squared ? target * target - from * from : target - from;
                Spring? thrown = null;
                if (distance != 0)
                {
                    var (oldFrom, oldTo) = (tween._fromNumbers[i], tween._toNumbers[i]);
                    var travelled = squared ? oldTo * oldTo - oldFrom * oldFrom : oldTo - oldFrom;
                    var velocity = animation.Paused ? 0 : Velocity(tween, tween._channelEases?[i] ?? tween._ease, travelled) * Constants.K * Globals.TimeScale / distance;
                    thrown = new Spring(spring.Mass, spring.Stiffness, spring.Damping, velocity);
                    span = Math.Max(span, thrown.SolverDuration);
                    settling = Math.Max(settling, thrown.SettlingDuration);
                }
                eases[i] = thrown == null ? AtRest : t => t >= 1 ? 1 : thrown.Solve(t * span);
                tween._fromNumbers[i] = from;
                tween._toNumbers[i] = target;
            }
            tween._currentTime = 0;
            // The tween's own ease only decides when the renderer rounds, and is what a later Set without an ease plays.
            tween._ease = t => t >= 1 ? 1 : spring.Solve(t * span);
            tween._channelEases = eases;
        }
        return Replay(animation, duration ?? (settling > 0 ? settling : spring.SettlingDuration));
    }

    /// <summary>How fast a value eased by <paramref name="ease"/> over <paramref name="distance"/> is changing at <paramref name="tween"/>'s current time, in units per engine-time unit.</summary>
    private static double Velocity(Tween tween, Ease ease, double distance)
    {
        // A spring's own ease raises its OnComplete when sampled past its duration; its pure twin does not.
        if (ease.Target is Spring spring && ease == spring.Ease) ease = spring.PureEase;

        var duration = tween._updateDuration;
        var progress = tween._currentTime / duration;
        var before = Math.Max(0, progress - VelocityStep);
        var after = Math.Min(1, progress + VelocityStep);
        return (ease(after) - ease(before)) / ((after - before) * duration) * distance;
    }

    private Action<Timer>? Wrap(Action<Animatable>? callback) => callback == null ? null : _ => callback(this);

    private void HandleBegin(Timer _)
    {
        if (_callbacks.Completed) _callbacks.Reset();
        _callbacks.Play();
    }

    private void HandlePause(Timer _)
    {
        if (_callbacks.Completed) return;
        foreach (var propertyAnimation in _animations.Values)
        {
            if (!propertyAnimation.Paused) return;
        }
        _callbacks.Complete();
    }

    private sealed class CallbacksTarget
    {
        public double V { get; set; }
    }
}
