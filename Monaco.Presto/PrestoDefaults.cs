using Monaco.Presto.Core;

namespace Monaco.Presto;

/// <summary>Global default parameter values applied to every timer that does not override them.</summary>
public sealed record PrestoDefaults
{
    /// <inheritdoc cref="TimerParams.PlaybackRate"/>
    public double PlaybackRate { get; set; } = 1;

    /// <inheritdoc cref="TimerParams.FrameRate"/>
    public double FrameRate { get; set; } = Constants.MaxFps;

    /// <inheritdoc cref="TimerParams.Loop"/>
    public LoopCount Loop { get; set; }

    /// <inheritdoc cref="TimerParams.Reversed"/>
    public bool Reversed { get; set; }

    /// <inheritdoc cref="TimerParams.Alternate"/>
    public bool Alternate { get; set; }

    /// <inheritdoc cref="TimerParams.Autoplay"/>
    public bool Autoplay { get; set; } = true;

    /// <summary>Default length of a single iteration, <c>1000</c>; <see cref="Engine.TimeUnit"/> rescales it (upstream <c>duration</c>).</summary>
    public TweenValue Duration { get; set; } = Constants.K;

    /// <inheritdoc cref="TimerParams.Delay"/>
    public TweenValue Delay { get; set; } = 0;

    /// <inheritdoc cref="TimerParams.LoopDelay"/>
    public double LoopDelay { get; set; }

    /// <summary>Default easing for every tween, <c>out(2)</c> (upstream <c>ease</c>).</summary>
    public EaseValue Ease { get; set; } = Eases.Default;

    /// <summary>Easing applied to the playhead itself rather than to the tween values; null keeps playback linear (upstream <c>playbackEase</c>).</summary>
    public EaseValue? PlaybackEase { get; set; }

    /// <summary>How a new tween composes with the tweens already running on the same target property (upstream <c>composition</c>).</summary>
    public Composition Composition { get; set; } = Composition.Replace;

    /// <summary>Applied to every tween value after easing, before it is written to the target (upstream <c>modifier</c>).</summary>
    public Func<double, double> Modifier { get; set; } = static v => v;

    /// <inheritdoc cref="Timer.OnBegin"/>
    public Action<Timer>? OnBegin { get; set; }

    /// <inheritdoc cref="Timer.OnBeforeUpdate"/>
    public Action<Timer>? OnBeforeUpdate { get; set; }

    /// <inheritdoc cref="Timer.OnUpdate"/>
    public Action<Timer>? OnUpdate { get; set; }

    /// <inheritdoc cref="Timer.OnLoop"/>
    public Action<Timer>? OnLoop { get; set; }

    /// <inheritdoc cref="Timer.OnPause"/>
    public Action<Timer>? OnPause { get; set; }

    /// <inheritdoc cref="Timer.OnComplete"/>
    public Action<Timer>? OnComplete { get; set; }

    /// <summary>Called on every frame on which a tween actually wrote a value to a target (upstream <c>onRender</c>).</summary>
    public Action<Timer>? OnRender { get; set; }

    /// <summary>
    /// Makes <see cref="AnimationBuilder.ToParams"/> build the <see cref="AnimationBuilder.WhenReduced"/> alternative where one
    /// exists. Like every default it belongs to one engine or <see cref="Scope"/>, which copy it when created.
    /// </summary>
    public bool ReducedMotion { get; set; }

    /// <summary>
    /// Collapses every animation not built from a <see cref="AnimationBuilder.WhenReduced"/> alternative to an instant,
    /// non-looping set of its end values. Like every default it belongs to one engine or <see cref="Scope"/>, which copy it when created.
    /// </summary>
    public bool ReduceAll { get; set; }
}
