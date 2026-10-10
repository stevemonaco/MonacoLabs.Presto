using Avalonia;
using MonacoLabs.Presto.Targets;

namespace MonacoLabs.Presto.Compositor;

/// <summary>
/// Parameters for an animation that runs on the render thread: the <see cref="AvaloniaAnimationParams"/> indexers over the
/// reduced anime.js <c>waapi.animate</c> parameter set. The per-frame callbacks, blend composition, playback ease, frame rate
/// and animation-wide modifier of <see cref="AnimationParams"/> have no counterpart on the compositor (COMPOSITOR-PLAN §3)
/// and are left out rather than silently ignored.
/// </summary>
public sealed record CompositorAnimationParams
{
    /// <summary>Installs the bridge, so the params work without a prior <c>UsePresto()</c>.</summary>
    public CompositorAnimationParams() => PrestoAvalonia.EnsureInitialized();

    /// <inheritdoc cref="AnimationParams.this[PropertyKey]"/>
    public TweenValue this[PropertyKey property]
    {
        get => Properties[property.Unwrap()];
        init => Properties[property.Unwrap()] = value;
    }

    /// <inheritdoc cref="AvaloniaAnimationParams.this[AvaloniaProperty]"/>
    public TweenValue this[AvaloniaProperty property]
    {
        get => Properties[AvaloniaObjectTargetAdapter.Encode(property)];
        init => Properties[AvaloniaObjectTargetAdapter.Encode(property)] = value;
    }

    /// <summary>A <c>with</c> copy shares this dictionary; every consumer only reads it.</summary>
    internal OrderedDictionary<string, TweenValue> Properties { get; } = new();

    /// <inheritdoc cref="AnimationParams.Duration"/>
    public TweenValue? Duration { get; init; }

    /// <inheritdoc cref="AnimationParams.Delay"/>
    public TweenValue? Delay { get; init; }

    /// <inheritdoc cref="AnimationParams.Ease"/>
    public EaseValue? Ease { get; init; }

    /// <inheritdoc cref="TimerParams.Loop"/>
    public LoopCount? Loop { get; init; }

    /// <inheritdoc cref="TimerParams.LoopDelay"/>
    public double? LoopDelay { get; init; }

    /// <inheritdoc cref="TimerParams.Alternate"/>
    public bool? Alternate { get; init; }

    /// <inheritdoc cref="TimerParams.Reversed"/>
    public bool? Reversed { get; init; }

    /// <inheritdoc cref="TimerParams.Autoplay"/>
    public bool? Autoplay { get; init; }

    /// <inheritdoc cref="TimerParams.PlaybackRate"/>
    public double? PlaybackRate { get; init; }

    /// <inheritdoc cref="Timer.OnBegin"/>
    public Action<Timer>? OnBegin { get; init; }

    /// <inheritdoc cref="Timer.OnComplete"/>
    public Action<Timer>? OnComplete { get; init; }

    /// <inheritdoc cref="AnimationParams.IsReducedMotion"/>
    internal bool IsReducedMotion { get; init; }
}
