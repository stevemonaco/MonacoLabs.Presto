namespace Monaco.Presto;

/// <summary>
/// A fluent, reusable recipe for <see cref="AnimationParams"/>. Every terminal snapshots it into fresh params, so
/// changing or reusing the builder afterwards never affects an animation already created.
/// </summary>
public sealed class AnimationBuilder : PropertyBuilder<AnimationBuilder>
{
    private AnimationParams _settings = new();
    private KeyframesBuilder? _keyframes;
    private Action<AnimationBuilder>? _reduced;

    /// <inheritdoc cref="AnimationParams.Duration"/>
    public AnimationBuilder Duration(TweenValue duration) { _settings = _settings with { Duration = duration }; return this; }

    /// <inheritdoc cref="AnimationParams.Delay"/>
    public AnimationBuilder Delay(TweenValue delay) { _settings = _settings with { Delay = delay }; return this; }

    /// <inheritdoc cref="AnimationParams.Ease"/>
    public AnimationBuilder Ease(EaseValue ease) { _settings = _settings with { Ease = ease }; return this; }

    /// <inheritdoc cref="AnimationParams.PlaybackEase"/>
    public AnimationBuilder PlaybackEase(EaseValue ease) { _settings = _settings with { PlaybackEase = ease }; return this; }

    /// <inheritdoc cref="AnimationParams.Composition"/>
    public AnimationBuilder Composition(Composition composition) { _settings = _settings with { Composition = composition }; return this; }

    /// <inheritdoc cref="AnimationParams.Modifier"/>
    public AnimationBuilder Modifier(Func<double, double> modifier) { _settings = _settings with { Modifier = modifier }; return this; }

    /// <inheritdoc cref="TimerParams.Id"/>
    public AnimationBuilder Id(string id) { _settings = _settings with { Id = id }; return this; }

    /// <summary>Repeats forever.</summary>
    public AnimationBuilder Loop() => Loop(LoopCount.Infinite);

    /// <inheritdoc cref="TimerParams.Loop"/>
    public AnimationBuilder Loop(LoopCount loop) { _settings = _settings with { Loop = loop }; return this; }

    /// <inheritdoc cref="TimerParams.LoopDelay"/>
    public AnimationBuilder LoopDelay(double ms) { _settings = _settings with { LoopDelay = ms }; return this; }

    /// <inheritdoc cref="TimerParams.Alternate"/>
    public AnimationBuilder Alternate(bool alternate = true) { _settings = _settings with { Alternate = alternate }; return this; }

    /// <inheritdoc cref="TimerParams.Reversed"/>
    public AnimationBuilder Reversed(bool reversed = true) { _settings = _settings with { Reversed = reversed }; return this; }

    /// <inheritdoc cref="TimerParams.Autoplay"/>
    public AnimationBuilder Autoplay(bool autoplay) { _settings = _settings with { Autoplay = autoplay }; return this; }

    /// <inheritdoc cref="TimerParams.FrameRate"/>
    public AnimationBuilder FrameRate(double fps) { _settings = _settings with { FrameRate = fps }; return this; }

    /// <inheritdoc cref="TimerParams.PlaybackRate"/>
    public AnimationBuilder PlaybackRate(double rate) { _settings = _settings with { PlaybackRate = rate }; return this; }

    /// <inheritdoc cref="TimerParams.Priority"/>
    public AnimationBuilder Priority(int priority) { _settings = _settings with { Priority = priority }; return this; }

    /// <inheritdoc cref="Timer.OnBegin"/>
    public AnimationBuilder OnBegin(Action<Timer> callback) { _settings = _settings with { OnBegin = callback }; return this; }

    /// <inheritdoc cref="Timer.OnBeforeUpdate"/>
    public AnimationBuilder OnBeforeUpdate(Action<Timer> callback) { _settings = _settings with { OnBeforeUpdate = callback }; return this; }

    /// <inheritdoc cref="Timer.OnUpdate"/>
    public AnimationBuilder OnUpdate(Action<Timer> callback) { _settings = _settings with { OnUpdate = callback }; return this; }

    /// <inheritdoc cref="Timer.OnLoop"/>
    public AnimationBuilder OnLoop(Action<Timer> callback) { _settings = _settings with { OnLoop = callback }; return this; }

    /// <inheritdoc cref="Timer.OnPause"/>
    public AnimationBuilder OnPause(Action<Timer> callback) { _settings = _settings with { OnPause = callback }; return this; }

    /// <inheritdoc cref="Timer.OnComplete"/>
    public AnimationBuilder OnComplete(Action<Timer> callback) { _settings = _settings with { OnComplete = callback }; return this; }

    /// <inheritdoc cref="AnimationParams.OnRender"/>
    public AnimationBuilder OnRender(Action<Timer> callback) { _settings = _settings with { OnRender = callback }; return this; }

    /// <summary>Sets the animation-level keyframes <paramref name="keyframes"/> adds, replacing any set before.</summary>
    public AnimationBuilder Keyframes(Action<KeyframesBuilder> keyframes)
    {
        _keyframes = new KeyframesBuilder();
        keyframes(_keyframes);
        return this;
    }

    /// <summary>
    /// Sets the form <see cref="ToParams"/> builds while <see cref="PrestoDefaults.ReducedMotion"/> is set: a new builder
    /// carrying this one's settings, including those chained later, but none of its properties or keyframes, changed by <paramref name="reduced"/>.
    /// </summary>
    public AnimationBuilder WhenReduced(Action<AnimationBuilder> reduced) { _reduced = reduced; return this; }

    /// <summary>Copies this builder's settings, properties, keyframes and reduced form into a new builder that changes independently of it.</summary>
    public AnimationBuilder Clone() =>
        CopyPropertiesTo(new AnimationBuilder { _settings = _settings, _keyframes = _keyframes?.Clone(), _reduced = _reduced });

    /// <summary>Returns a <see cref="Clone"/> with <paramref name="change"/> applied, leaving this builder untouched.</summary>
    public AnimationBuilder With(Action<AnimationBuilder> change)
    {
        var copy = Clone();
        change(copy);
        return copy;
    }

    /// <summary>Snapshots the builder into new <see cref="AnimationParams"/>, equal to the indexer form of the same calls.</summary>
    public AnimationParams ToParams()
    {
        if (_reduced == null || !Engine.Current.Defaults.ReducedMotion) return Snapshot(false);
        var reduced = new AnimationBuilder { _settings = _settings };
        _reduced(reduced);
        return reduced.Snapshot(true);
    }

    /// <summary>Animates <paramref name="targets"/> with a snapshot of this builder, as <see cref="Presto.Animate(object?, AnimationParams)"/> does.</summary>
    public Animation Animate(object? targets) => Presto.Animate(targets, ToParams());

    /// <summary>Writes this builder's values to <paramref name="targets"/> immediately, as <see cref="Presto.Set(object?, AnimationParams)"/> does.</summary>
    public Animation Set(object? targets) => Presto.Set(targets, ToParams());

    private AnimationParams Snapshot(bool reducedMotion) =>
        _settings with { Properties = new(Properties), Keyframes = _keyframes?.ToKeyframes(), IsReducedMotion = reducedMotion };
}
