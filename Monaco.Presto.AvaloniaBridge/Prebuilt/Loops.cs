namespace Monaco.Presto.AvaloniaBridge.Prebuilt;

/// <summary>
/// Looping recipes that play until canceled. Each is static while <see cref="PrestoDefaults.ReducedMotion"/> is set: it
/// animates nothing and plays once, leaving the element at rest.
/// </summary>
/// <remarks>
/// Arguments left null come from <see cref="Recipes.Tokens"/>, read when the recipe is called: each iteration lasts twice
/// <see cref="MotionTokens.Long"/>, except <see cref="Spin"/>'s full turn at three times. The ease is
/// <see cref="Eases.InOutSine"/>, or linear for <see cref="Spin"/>, since a token ease would stall a loop at each turn.
/// <c>stagger</c> delays each element of a batch by <see cref="MotionTokens.StaggerStep"/> more than the one before,
/// starting at <c>delay</c>. All can run through <see cref="PrestoAvalonia.AnimateOnCompositor(object?, AnimationBuilder)"/>.
/// </remarks>
public static class Loops
{
    /// <summary>Bobs up <paramref name="distance"/> DIPs (default half <see cref="MotionTokens.Distance"/>) and back. Reduced: static.</summary>
    public static AnimationBuilder Float(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loop(duration, delay, ease, stagger).Y(-(distance ?? Recipes.Tokens.Distance / 2)).Alternate();

    /// <summary>Turns clockwise, one full turn per iteration. Reduced: static.</summary>
    public static AnimationBuilder Spin(double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loop(duration ?? Recipes.Tokens.Long * 3, delay, ease ?? Eases.None, stagger).Rotate(360);

    /// <summary>Swells to 1.05 times its size and back, the swell multiplied by <paramref name="intensity"/>. Reduced: static.</summary>
    public static AnimationBuilder Breathe(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loop(duration, delay, ease, stagger).Scale(1 + (0.05 * intensity)).Alternate();

    /// <summary>Fades to 0.3 opacity and back, the fade multiplied by <paramref name="intensity"/>. Reduced: static.</summary>
    public static AnimationBuilder Blink(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loop(duration, delay, ease, stagger).Opacity(Math.Max(0, 1 - (0.7 * intensity))).Alternate();

    /// <summary>
    /// Bobs each element up <paramref name="distance"/> DIPs (default half <see cref="MotionTokens.Distance"/>) and back over
    /// <see cref="MotionTokens.Long"/>, each starting <paramref name="step"/> ms (default <see cref="MotionTokens.StaggerStep"/>)
    /// after the one before, so a split line ripples. Reduced: static.
    /// </summary>
    public static AnimationBuilder Wave(double? distance = null, double? step = null, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Loop(duration ?? Recipes.Tokens.Long, null, ease, false).Y(-(distance ?? Recipes.Tokens.Distance / 2)).Alternate()
            .Delay(Presto.Stagger(step ?? Recipes.Tokens.StaggerStep, new() { Start = delay }));

    private static AnimationBuilder Loop(double? duration, double? delay, EaseValue? ease, bool stagger) =>
        Recipes.Start(Recipes.Tokens, duration ?? Recipes.Tokens.Long * 2, delay, ease ?? Eases.InOutSine, stagger)
            .Loop()
            .WhenReduced(reduced => reduced.Loop(false).Alternate(false));
}
