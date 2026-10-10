using Avalonia.Media;

namespace MonacoLabs.Presto.Prebuilt;

/// <summary>
/// Attention recipes: keyframed jolts that end back at identity, with their travel multiplied by <c>intensity</c>
/// (default 1). Each becomes a <see cref="MotionTokens.Short"/> blink of Opacity to 0.6 and back to 1 while
/// <see cref="PrestoDefaults.ReducedMotion"/> is set.
/// </summary>
/// <remarks>
/// Arguments left null come from <see cref="Recipes.Tokens"/>, read when the recipe is called: the duration is
/// <see cref="MotionTokens.Long"/> and the ease, applied to each keyframe, <see cref="MotionTokens.Ease"/>. <c>stagger</c>
/// delays each element of a batch by <see cref="MotionTokens.StaggerStep"/> more than the one before, starting at
/// <c>delay</c>. All but <see cref="FlashTint"/>, which filters, can run through
/// <see cref="PrestoAvalonia.AnimateOnCompositor(object?, AnimationBuilder)"/>.
/// </remarks>
public static class Attention
{
    /// <summary>Shakes side to side, 10 DIPs at first. Reduced: an opacity blink.</summary>
    public static AnimationBuilder Shake(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Start(duration, delay, ease, stagger).X(Frames(0, intensity, -10, 10, -6, 6, -3, 3));

    /// <summary>Swells to 1.06 times its size and back. Reduced: an opacity blink.</summary>
    public static AnimationBuilder Pulse(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Start(duration, delay, ease, stagger).Scale(Frames(1, intensity, 0.06));

    /// <summary>Sways side to side while tilting, like a wobbling top. Reduced: an opacity blink.</summary>
    public static AnimationBuilder Wobble(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Start(duration, delay, ease, stagger)
            .X(Frames(0, intensity, -16, 12, -9, 6, -3))
            .Rotate(Frames(0, intensity, -5, 3, -3, 2, -1));

    /// <summary>Squashes and stretches, widening as it flattens. Reduced: an opacity blink.</summary>
    public static AnimationBuilder Jelly(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Start(duration, delay, ease, stagger)
            .ScaleX(Frames(1, intensity, 0.25, -0.25, 0.15, -0.05, 0.05))
            .ScaleY(Frames(1, intensity, -0.25, 0.25, -0.15, 0.05, -0.05));

    /// <summary>Beats twice, swelling to 1.15 times its size. Reduced: an opacity blink.</summary>
    public static AnimationBuilder Heartbeat(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Start(duration, delay, ease, stagger).Scale(Frames(1, intensity, 0.15, 0, 0.15));

    /// <summary>Shrinks, then grows while rocking, like a flourish. Reduced: an opacity blink.</summary>
    public static AnimationBuilder Tada(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Start(duration, delay, ease, stagger)
            .Scale(Frames(1, intensity, -0.1, 0.1, 0.1, 0.1, 0.1))
            .Rotate(Frames(0, intensity, -3, 3, -3, 3, -3));

    /// <summary>Hops 20 DIPs, then twice more, lower each time. Reduced: an opacity blink.</summary>
    public static AnimationBuilder Bounce(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Start(duration, delay, ease, stagger).Y(Frames(0, intensity, -20, 0, -10, 0, -4));

    /// <summary>
    /// Flashes a <see cref="Controls.PrestoImageBase"/> towards <paramref name="color"/> (default <see cref="Colors.DodgerBlue"/>):
    /// <see cref="Controls.PrestoImageBase.Tint"/> rises to <paramref name="intensity"/> and falls back to 0, with
    /// <see cref="Controls.PrestoImageBase.TintColor"/> set at the start. Reduced: an opacity blink.
    /// </summary>
    public static AnimationBuilder FlashTint(Color? color = null, double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Start(duration, delay, ease, stagger)
            .TintColor(to: TweenValue.Of(color ?? Colors.DodgerBlue), duration: 0)
            .Tint(Frames(0, intensity, 1));

    private static AnimationBuilder Start(double? duration, double? delay, EaseValue? ease, bool stagger)
    {
        var tokens = Recipes.Tokens;
        return Recipes.Start(tokens, duration ?? tokens.Long, delay, ease ?? tokens.Ease, stagger)
            .WhenReduced(reduced => reduced.Opacity(k => k.To(0.6).To(1)).Duration(tokens.Short));
    }

    private static Action<TweenKeyframesBuilder> Frames(double rest, double intensity, params double[] offsets) => k =>
    {
        foreach (var offset in offsets) k.To(rest + (offset * intensity));
        k.To(rest);
    };
}
