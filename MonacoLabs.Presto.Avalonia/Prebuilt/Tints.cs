using Avalonia.Media;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Prebuilt;

/// <summary>
/// Color recipes: a color property follows the <see cref="PrestoColor.Mix"/> path in <see cref="MotionTokens.ColorSpace"/>
/// with <see cref="MotionTokens.Hue"/>, expanded by <see cref="TweenKeyframesBuilder.Color(TweenValue, TweenValue, ColorSpace, HueInterpolation, TweenValue?, TweenValue?, EaseValue?, int?)"/>. A color change is not
/// motion, so <see cref="PrestoDefaults.ReducedMotion"/> leaves them as they are.
/// </summary>
/// <remarks>
/// Arguments left null come from <see cref="Recipes.Tokens"/>, read when the recipe is called: the duration is
/// <see cref="MotionTokens.Medium"/> and the ease <see cref="MotionTokens.Ease"/>. <c>stagger</c> delays each element of a
/// batch by <see cref="MotionTokens.StaggerStep"/> more than the one before. UI thread only, since the compositor cannot
/// animate a brush.
/// </remarks>
public static class Tints
{
    /// <summary>
    /// Recolors <paramref name="property"/> (<c>Background</c> when null) from each element's current color, read when the
    /// animation is built, to <paramref name="to"/>. A solid brush or a <see cref="Color"/> property starts from its color; anything else from transparent.
    /// Reduced: the same recolor, which <see cref="PrestoDefaults.ReduceAll"/> leaves alone.
    /// </summary>
    public static AnimationBuilder Recolor(Color to, PropertyKey? property = null, ColorSpace? space = null, HueInterpolation? hue = null,
                                           double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false)
    {
        var name = property?.Unwrap() ?? "Background";
        var tokens = Recipes.Tokens;
        var ms = duration ?? tokens.Medium;
        var easeValue = ease ?? tokens.Ease;
        AnimationBuilder Tint(AnimationBuilder b) =>
            b.Property(name, k => k.Color(to.ToPresto(), space ?? tokens.ColorSpace, hue ?? tokens.Hue, ms, ease: easeValue));
        return Tint(Recipes.Start(tokens, ms, delay, easeValue, stagger)).WhenReduced(reduced => Tint(reduced));
    }
}
