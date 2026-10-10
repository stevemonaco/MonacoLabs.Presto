namespace MonacoLabs.Presto.Prebuilt;

/// <summary>
/// Exit recipes, the mirrors of <see cref="Entrances"/>. Each states where the element ends, fading to transparent, and
/// each becomes a <see cref="MotionTokens.Short"/> fade while <see cref="PrestoDefaults.ReducedMotion"/> is set.
/// </summary>
/// <remarks>
/// Arguments left null come from <see cref="Recipes.Tokens"/>, read when the recipe is called: the duration is
/// <see cref="MotionTokens.Short"/> and the ease <see cref="MotionTokens.Ease"/>. <c>stagger</c> delays each element of a
/// batch by <see cref="MotionTokens.StaggerStep"/> more than the one before, starting at <c>delay</c>. All but
/// <see cref="BlurOut"/> and <see cref="FlipOut"/> can run through <see cref="PrestoAvalonia.AnimateOnCompositor(object?, AnimationBuilder)"/>.
/// </remarks>
public static class Exits
{
    /// <summary>Fades out to transparent.</summary>
    public static AnimationBuilder FadeOut(double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exit(duration, delay, ease, stagger);

    /// <summary>Fades out while rising <paramref name="distance"/> DIPs (default <see cref="MotionTokens.Distance"/>).</summary>
    public static AnimationBuilder FadeOutUp(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exit(duration, delay, ease, stagger).Y(-(distance ?? Recipes.Tokens.Distance));

    /// <summary>Fades out while dropping <paramref name="distance"/> DIPs (default <see cref="MotionTokens.Distance"/>).</summary>
    public static AnimationBuilder FadeOutDown(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exit(duration, delay, ease, stagger).Y(distance ?? Recipes.Tokens.Distance);

    /// <summary>Fades out while moving <paramref name="distance"/> DIPs to the left (default <see cref="MotionTokens.Distance"/>).</summary>
    public static AnimationBuilder FadeOutLeft(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exit(duration, delay, ease, stagger).X(-(distance ?? Recipes.Tokens.Distance));

    /// <summary>Fades out while moving <paramref name="distance"/> DIPs to the right (default <see cref="MotionTokens.Distance"/>).</summary>
    public static AnimationBuilder FadeOutRight(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exit(duration, delay, ease, stagger).X(distance ?? Recipes.Tokens.Distance);

    /// <summary>Fades out while shrinking to <paramref name="scale"/> (default <see cref="MotionTokens.Scale"/>).</summary>
    public static AnimationBuilder ScaleOut(double? scale = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exit(duration, delay, ease, stagger).Scale(scale ?? Recipes.Tokens.Scale);

    /// <summary>Shrinks to <paramref name="scale"/> (default <see cref="MotionTokens.Scale"/>) with <see cref="MotionTokens.EmphasizedEase"/>.</summary>
    public static AnimationBuilder PopOut(double? scale = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exit(duration, delay, ease ?? Recipes.Tokens.EmphasizedEase, stagger).Scale(scale ?? Recipes.Tokens.Scale);

    /// <summary>Fades out while blurring to <paramref name="radius"/> (default <see cref="MotionTokens.BlurRadius"/>). UI thread only: the compositor cannot blur.</summary>
    public static AnimationBuilder BlurOut(double? radius = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exit(duration, delay, ease, stagger).Blur(radius ?? Recipes.Tokens.BlurRadius);

    /// <summary>Fades out while sliding towards <paramref name="edge"/>, ending four times <see cref="MotionTokens.Distance"/> away unless <paramref name="distance"/> is given.</summary>
    public static AnimationBuilder SlideOut(Edge edge, double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false)
    {
        var (property, offset) = Recipes.Offset(edge, distance ?? Recipes.Tokens.Distance * 4);
        return Exit(duration, delay, ease, stagger).Property(property, offset);
    }

    /// <summary>
    /// Fades out while swinging about its horizontal axis to <paramref name="angle"/> degrees, in perspective, with
    /// <see cref="MotionTokens.EmphasizedEase"/>. UI thread only: the compositor has no 3D rotation.
    /// </summary>
    public static AnimationBuilder FlipOut(double angle = -90, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exit(duration, delay, ease ?? Recipes.Tokens.EmphasizedEase, stagger).RotateX(angle).Perspective(Recipes.FlipPerspective);

    /// <summary>
    /// Flies each element to a random <c>X</c>/<c>Y</c> offset within <paramref name="distance"/> (default four times
    /// <see cref="MotionTokens.Distance"/>), spun within ±<paramref name="spin"/> degrees (default 180), at <c>Scale</c> 0.6, over
    /// <see cref="MotionTokens.Medium"/> with <see cref="MotionTokens.EmphasizedEase"/>. A <paramref name="seed"/> makes the
    /// offsets the same on every play.
    /// </summary>
    public static AnimationBuilder Scatter(double? distance = null, double? spin = null, int? seed = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false)
    {
        var (x, y, rotate) = Recipes.ScatterOffsets(distance, spin, seed);
        return Exit(duration ?? Recipes.Tokens.Medium, delay, ease ?? Recipes.Tokens.EmphasizedEase, stagger).X(x).Y(y).Rotate(rotate).Scale(0.6);
    }

    /// <summary>
    /// Drops each element <paramref name="distance"/> DIPs (default six times <see cref="MotionTokens.Distance"/>) with
    /// <c>Eases.In(2)</c> over <see cref="MotionTokens.Long"/>, drifting sideways within a quarter of the drop and spinning
    /// within ±<paramref name="spin"/> degrees (default 45), and fades it over the last third. A <paramref name="seed"/> makes the drift
    /// and spin the same on every play.
    /// </summary>
    public static AnimationBuilder Fall(double? distance = null, double? spin = null, int? seed = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false)
    {
        var drop = distance ?? Recipes.Tokens.Distance * 6;
        var span = duration ?? Recipes.Tokens.Long;
        return Exit(span, delay, ease ?? Eases.In(2), stagger)
            .Y(drop).X(Recipes.SeededRandom(seed, 0, drop / 4)).Rotate(Recipes.SeededRandom(seed, 1, spin ?? 45))
            .Opacity(to: 0, delay: span * 2 / 3, duration: span / 3);
    }

    private static AnimationBuilder Exit(double? duration, double? delay, EaseValue? ease, bool stagger)
    {
        var tokens = Recipes.Tokens;
        return Recipes.Start(tokens, duration ?? tokens.Short, delay, ease ?? tokens.Ease, stagger)
            .Opacity(0)
            .WhenReduced(reduced => reduced.Opacity(0).Duration(tokens.Short));
    }
}
