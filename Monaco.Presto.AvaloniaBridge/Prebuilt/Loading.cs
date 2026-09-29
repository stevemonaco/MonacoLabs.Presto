namespace Monaco.Presto.AvaloniaBridge.Prebuilt;

/// <summary>
/// Loading indicators that loop until cancelled. Each becomes a slow opacity pulse, to 0.6 and back every three times
/// <see cref="MotionTokens.Long"/>, while <see cref="PrestoDefaults.ReducedMotion"/> is set, so the wait still shows.
/// </summary>
/// <remarks>
/// Arguments left null come from <see cref="Recipes.Tokens"/>, read when the recipe is called: each iteration lasts twice
/// <see cref="MotionTokens.Long"/> and eases with <see cref="Eases.InOutSine"/>, or linearly for <see cref="Spinner"/>.
/// <c>stagger</c> delays each element of a batch by <see cref="MotionTokens.StaggerStep"/> more than the one before,
/// starting at <c>delay</c>. <see cref="Spinner"/>, <see cref="Dots"/> and <see cref="Indeterminate"/> can run through
/// <see cref="PrestoAvalonia.AnimateOnCompositor(object?, AnimationBuilder)"/>; <see cref="Shimmer"/> is UI thread only,
/// since the compositor cannot mask.
/// </remarks>
public static class Loading
{
    /// <summary>Sweeps the element into view from left to right through a soft-edged <c>MaskSweep</c>, over and over, like a skeleton placeholder. Reduced: a slow opacity pulse.</summary>
    public static AnimationBuilder Shimmer(double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loop(duration, delay, ease, stagger).MaskSweep(from: 0, to: 1);

    /// <summary>Turns clockwise, one full turn per iteration. Reduced: a slow opacity pulse.</summary>
    public static AnimationBuilder Spinner(double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loop(duration ?? Recipes.Tokens.Long, delay, ease ?? Eases.None, stagger).Rotate(360);

    /// <summary>
    /// Hops each of a batch, such as a panel's children, up <paramref name="distance"/> DIPs (default a third of
    /// <see cref="MotionTokens.Distance"/>) and back, each a sixth of an iteration after the one before. Reduced: a slow opacity pulse, still staggered.
    /// </summary>
    public static AnimationBuilder Dots(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null)
    {
        var length = duration ?? Recipes.Tokens.Long * 2;
        var hop = -(distance ?? Recipes.Tokens.Distance / 3);
        return Loop(length, null, ease, stagger: false)
            .Delay(Presto.Stagger(length / 6, new() { Start = delay }))
            .Y(k => k.To(hop).To(0));
    }

    /// <summary>
    /// Sweeps a progress bar's fill across its track, from <paramref name="distance"/> DIPs left of its place to as far right,
    /// so give the track's width. Reduced: a slow opacity pulse.
    /// </summary>
    public static AnimationBuilder Indeterminate(double distance, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loop(duration, delay, ease, stagger).X(from: -distance, to: distance);

    private static AnimationBuilder Loop(double? duration, double? delay, EaseValue? ease, bool stagger)
    {
        var tokens = Recipes.Tokens;
        return Recipes.Start(tokens, duration ?? tokens.Long * 2, delay, ease ?? Eases.InOutSine, stagger)
            .Loop()
            .WhenReduced(reduced => reduced.Opacity(0.6).Duration(tokens.Long * 3).Ease(Eases.InOutSine).Alternate());
    }
}
