using Avalonia;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Prebuilt;

/// <summary>
/// Color recipes: a color property follows the <see cref="PrestoColor.Mix"/> path in <see cref="MotionTokens.ColorSpace"/>
/// with <see cref="MotionTokens.Hue"/>, expanded by <see cref="TweenKeyframesBuilder.Color"/>. A color change is not
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
    /// Recolors <paramref name="property"/> from each element's current color, read when the animation starts, to
    /// <paramref name="to"/>. A solid brush or a <see cref="Color"/> property starts from its color; anything else from transparent.
    /// </summary>
    public static AnimationBuilder Recolor(Color to, string property = "Background", ColorSpace? space = null, HueInterpolation? hue = null,
                                           double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false)
    {
        var tokens = Recipes.Tokens;
        var ms = duration ?? tokens.Medium;
        var easeValue = ease ?? tokens.Ease;
        return Recipes.Start(tokens, ms, delay, easeValue, stagger)
            .Property(property, k => k.Color((FunctionValue)((target, _, _) => Current(target, property)), to.ToPresto(),
                space ?? tokens.ColorSpace, hue ?? tokens.Hue, ms, ease: easeValue));
    }

    private static TweenValue Current(object target, string property)
    {
        var value = target is AvaloniaObject element && AvaloniaObjectTargetAdapter.ResolveProperty(element.GetType(), property) is { } resolved
            ? element.GetValue(resolved)
            : null;
        return value switch
        {
            ISolidColorBrush brush => brush.Color.ToPresto() with { A = brush.Color.A / 255d * brush.Opacity },
            Color color => color.ToPresto(),
            _ => Colors.Transparent.ToPresto(),
        };
    }
}
