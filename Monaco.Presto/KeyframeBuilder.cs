namespace Monaco.Presto;

/// <summary>One animation-level frame for <see cref="KeyframesBuilder"/>: the properties it sets plus that frame's own settings.</summary>
public sealed class KeyframeBuilder : PropertyBuilder<KeyframeBuilder>
{
    private TweenValue? _duration;
    private TweenValue? _delay;
    private EaseValue? _ease;
    private Composition? _composition;
    private Func<double, double>? _modifier;

    internal KeyframeBuilder() { }

    /// <inheritdoc cref="Keyframe.Duration"/>
    public KeyframeBuilder Duration(TweenValue duration) { _duration = duration; return this; }

    /// <inheritdoc cref="Keyframe.Delay"/>
    public KeyframeBuilder Delay(TweenValue delay) { _delay = delay; return this; }

    /// <inheritdoc cref="Keyframe.Ease"/>
    public KeyframeBuilder Ease(EaseValue ease) { _ease = ease; return this; }

    /// <inheritdoc cref="Keyframe.Composition"/>
    public KeyframeBuilder Composition(Composition composition) { _composition = composition; return this; }

    /// <inheritdoc cref="Keyframe.Modifier"/>
    public KeyframeBuilder Modifier(Func<double, double> modifier) { _modifier = modifier; return this; }

    internal Keyframe ToKeyframe()
    {
        var keyframe = new Keyframe { Duration = _duration, Delay = _delay, Ease = _ease, Composition = _composition, Modifier = _modifier };
        foreach (var (name, value) in Properties) keyframe.Properties[name] = value;
        return keyframe;
    }
}
