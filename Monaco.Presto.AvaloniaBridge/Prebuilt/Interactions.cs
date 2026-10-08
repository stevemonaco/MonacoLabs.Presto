using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.Prebuilt;

/// <summary>
/// Interaction recipes for <see cref="Motion.HoverProperty"/>, <see cref="Motion.PressProperty"/>,
/// <see cref="Motion.FocusProperty"/> and <see cref="MotionState"/>: absolute to-values that the state holds and returns
/// from. While <see cref="PrestoDefaults.ReducedMotion"/> is set each keeps its color, opacity and shadow but drops its
/// travel and scale.
/// </summary>
/// <remarks>
/// Arguments left null come from <see cref="Recipes.Tokens"/>, read when the recipe is called: the duration is
/// <see cref="MotionTokens.Short"/> and the ease <see cref="MotionTokens.Ease"/>. <c>intensity</c> multiplies the change
/// (default 1). A state applies to one element, so there is no <c>stagger</c>. <see cref="Grow"/>, <see cref="PressIn"/>
/// and <see cref="Dim"/> can run through <see cref="PrestoAvalonia.AnimateOnCompositor(object?, AnimationBuilder)"/>;
/// <see cref="Lift"/>, <see cref="ReorderLift"/> and <see cref="Glow"/> are UI thread only, since the compositor cannot draw a shadow, and so is
/// <see cref="Colorize"/>, since it cannot filter.
/// </remarks>
public static class Interactions
{
    /// <summary>Rises <paramref name="distance"/> DIPs (default a sixth of <see cref="MotionTokens.Distance"/>) over a soft shadow. Reduced: the shadow alone.</summary>
    public static AnimationBuilder Lift(double? distance = null, double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Start(duration, delay, ease, b => b.ShadowBlur(12 * intensity).ShadowOpacity(0.25))
            .Y(-(distance ?? Recipes.Tokens.Distance / 6) * intensity);

    /// <summary>Grows to 1.04 times its size. Reduced: nothing.</summary>
    public static AnimationBuilder Grow(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Start(duration, delay, ease, null).Scale(1 + (0.04 * intensity));

    /// <summary>Shrinks to 0.96 times its size. Reduced: dims Opacity to 0.8.</summary>
    public static AnimationBuilder PressIn(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Start(duration, delay, ease, b => b.Opacity(1 - (0.2 * intensity))).Scale(1 - (0.04 * intensity));

    /// <summary>Lifts a reorder drag's item: grows to <paramref name="scale"/> (default 1.03) over a raised shadow, leaving X and Y to the drag. Reduced: the shadow alone.</summary>
    public static AnimationBuilder ReorderLift(double? scale = null, double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Start(duration, delay, ease, b => b.ShadowBlur(16 * intensity).ShadowY(6 * intensity).ShadowOpacity(0.3))
            .Scale(1 + (((scale ?? 1.03) - 1) * intensity));

    /// <summary>Glows with a drop shadow of <paramref name="color"/> (default <see cref="Colors.DodgerBlue"/>), blurred 16 DIPs. Reduced: the same glow.</summary>
    public static AnimationBuilder Glow(Color? color = null, double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Start(duration, delay, ease, b => b.ShadowBlur(16 * intensity).ShadowOpacity(0.8).ShadowColor(color ?? Colors.DodgerBlue));

    /// <summary>Dims Opacity to 0.6. Reduced: the same dimming.</summary>
    public static AnimationBuilder Dim(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Start(duration, delay, ease, b => b.Opacity(1 - (0.4 * intensity)));

    /// <summary>
    /// Brings a <see cref="Controls.PrestoImageBase"/> to full color, <see cref="Controls.PrestoImageBase.Grayscale"/> 0 and
    /// <see cref="Controls.PrestoImageBase.Saturate"/> 1, from the grayscale its resting values set. Reduced: the same change.
    /// </summary>
    public static AnimationBuilder Colorize(double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Start(duration, delay, ease, b => b.Grayscale(0).Saturate(1));

    private static AnimationBuilder Start(double? duration, double? delay, EaseValue? ease, Func<AnimationBuilder, AnimationBuilder>? kept)
    {
        var tokens = Recipes.Tokens;
        var builder = Recipes.Start(tokens, duration ?? tokens.Short, delay, ease ?? tokens.Ease, stagger: false)
            .WhenReduced(reduced => kept?.Invoke(reduced));
        return kept?.Invoke(builder) ?? builder;
    }
}
