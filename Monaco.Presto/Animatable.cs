using Monaco.Presto.Core;

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
    private readonly Dictionary<string, Animation> _animations = [];
    private readonly Animation _callbacks;

    internal Animatable(object? targets, AnimatableParams parameters)
    {
        Globals.CurrentScope?.Register(this);

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
            _animations[property] = animation;
            if (_targets.Count == 0) _targets.AddRange(animation.Targets);
        }
    }

    public IReadOnlyList<object> Targets => _targets;

    public IReadOnlyDictionary<string, Animation> Animations => _animations;

    /// <summary>The property's current value, or 0 once <see cref="Revert"/> has run.</summary>
    public double Get(string property) =>
        _animations.TryGetValue(property, out var animation) && animation._headTween is { } tween ? tween._modifier(tween._number) : 0;

    /// <summary>The channels of a colour or complex property, empty for a scalar one.</summary>
    public double[] GetChannels(string property) =>
        _animations.TryGetValue(property, out var animation) && animation._headTween is { } tween ? tween._numbers : [];

    /// <summary>Animates the property from where it is now to <paramref name="to"/>; a no-op after <see cref="Revert"/>.</summary>
    /// <returns>This animatable, so calls chain.</returns>
    public Animatable Set(string property, double to, double? duration = null, EaseValue? ease = null)
    {
        if (!_animations.TryGetValue(property, out var animation)) return this;
        for (var tween = animation._headTween; tween != null; tween = tween._next)
        {
            tween._fromNumber = tween._modifier(tween._number);
            tween._toNumber = to;
            if (ease is { } easeValue) tween._ease = easeValue.Resolve(tween.Target, 0, 1, out _);
            tween._currentTime = 0;
        }
        return Replay(animation, duration);
    }

    /// <inheritdoc cref="Set(string, double, double?, EaseValue?)"/>
    /// <remarks>The channel form, for the colour and complex properties <see cref="GetChannels"/> reads.</remarks>
    public Animatable Set(string property, IReadOnlyList<double> to, double? duration = null, EaseValue? ease = null)
    {
        if (!_animations.TryGetValue(property, out var animation)) return this;
        for (var tween = animation._headTween; tween != null; tween = tween._next)
        {
            for (var i = 0; i < to.Count && i < tween._numbers.Length; i++)
            {
                tween._fromNumbers[i] = tween._modifier(tween._numbers[i]);
                tween._toNumbers[i] = to[i];
            }
            if (ease is { } easeValue) tween._ease = easeValue.Resolve(tween.Target, 0, 1, out _);
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
