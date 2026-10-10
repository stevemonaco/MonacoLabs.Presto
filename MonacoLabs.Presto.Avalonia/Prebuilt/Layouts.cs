namespace MonacoLabs.Presto.Prebuilt;

/// <summary>Layout recipes: timing and ease only, since layout decides where the element goes.</summary>
public static class Layouts
{
    /// <summary>
    /// Eases a layout move over <paramref name="duration"/> (default <see cref="MotionTokens.Medium"/>) with
    /// <paramref name="ease"/> (default <see cref="MotionTokens.Ease"/>), both read from <see cref="Recipes.Tokens"/> when called.
    /// Meant for <c>Animate.Layout</c>, <c>Animate.ItemLayout</c> and <c>AnimateLayout(builder)</c>. Reduced: an instant move.
    /// </summary>
    public static AnimationBuilder Reflow(double? duration = null, double? delay = null, EaseValue? ease = null)
    {
        var tokens = Recipes.Tokens;
        return Recipes.Start(tokens, duration ?? tokens.Medium, delay, ease ?? tokens.Ease, stagger: false)
            .WhenReduced(reduced => reduced.Duration(0).Delay(0));
    }
}
