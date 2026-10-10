namespace MonacoLabs.Presto.Compositor;

/// <summary>
/// How a layout transition moves, as opposed to where, which layout decides. A <c>null</c> per-axis ease follows
/// <see cref="Ease"/>; differing <see cref="EaseX"/> and <see cref="EaseY"/> curve the flight into an arc.
/// </summary>
internal readonly record struct LayoutMotion(
    double Duration,
    double Delay = 0,
    EaseValue? Ease = null,
    EaseValue? EaseX = null,
    EaseValue? EaseY = null,
    EaseValue? EaseScale = null,
    bool LandsWhenReduced = false)
{
    /// <summary>The motion for a move starting now: one frame while <see cref="LandsWhenReduced"/> and <see cref="PrestoDefaults.ReducedMotion"/> are both set.</summary>
    public LayoutMotion Current => LandsWhenReduced && Engine.Current.Defaults.ReducedMotion ? new(0, Ease: Eases.None) : this;

    /// <exception cref="NotSupportedException">The builder sets something a layout transition cannot honor.</exception>
    public static LayoutMotion From(AnimationBuilder builder)
    {
        var p = builder.ToParams();
        var unsupported =
            p.Keyframes != null ? nameof(p.Keyframes) :
            p.Loop != null ? nameof(p.Loop) :
            p.LoopDelay != null ? nameof(p.LoopDelay) :
            p.Alternate != null ? nameof(p.Alternate) :
            p.Reversed != null ? nameof(p.Reversed) :
            p.Autoplay != null ? nameof(p.Autoplay) :
            p.FrameRate != null ? nameof(p.FrameRate) :
            p.PlaybackRate != null ? nameof(p.PlaybackRate) :
            p.PlaybackEase != null ? nameof(p.PlaybackEase) :
            p.Composition != null ? nameof(p.Composition) :
            p.Modifier != null ? nameof(p.Modifier) :
            p.Id != null ? nameof(p.Id) :
            p.Priority != new TimerParams().Priority ? nameof(p.Priority) :
            p.OnBegin != null ? nameof(p.OnBegin) :
            p.OnBeforeUpdate != null ? nameof(p.OnBeforeUpdate) :
            p.OnUpdate != null ? nameof(p.OnUpdate) :
            p.OnLoop != null ? nameof(p.OnLoop) :
            p.OnPause != null ? nameof(p.OnPause) :
            p.OnComplete != null ? nameof(p.OnComplete) :
            p.OnRender != null ? nameof(p.OnRender) :
            null;
        if (unsupported != null) throw new NotSupportedException($"{unsupported} has no counterpart on a layout transition; await Then() for completion.");

        EaseValue? easeX = null, easeY = null, easeScale = null;
        foreach (var (name, value) in p.Properties)
        {
            var ease = AxisEase(name, value);
            switch (name)
            {
                case "X": easeX = ease; break;
                case "Y": easeY = ease; break;
                default: easeScale = ease; break;
            }
        }

        var defaults = Engine.Current.Defaults;
        return new LayoutMotion(
            Number(p.Duration, nameof(p.Duration)) ?? Number(defaults.Duration, nameof(defaults.Duration))!.Value,
            Number(p.Delay, nameof(p.Delay)) ?? 0,
            p.Ease,
            easeX,
            easeY,
            easeScale);
    }

    private static EaseValue? AxisEase(string name, TweenValue value)
    {
        if (name is not ("X" or "Y" or "Scale"))
            throw new NotSupportedException($"A layout transition moves only X, Y and Scale, so \"{name}\" cannot ride along; animate it separately.");
        if (value.Raw is not TweenParams tween || tween.To != null || tween.From != null || tween.Duration != null
            || tween.Delay != null || tween.Composition != null || tween.Modifier != null || tween.TextModifier != null)
            throw new NotSupportedException($"Layout decides where {name} goes, so give it only an ease: .{name}(ease: …).");
        return tween.Ease;
    }

    private static double? Number(TweenValue? value, string name) => value switch
    {
        null => null,
        { AsDouble: { } number } => number,
        _ => throw new NotSupportedException($"A layout transition moves one control, so {name} must be a number rather than a per-target value."),
    };
}
