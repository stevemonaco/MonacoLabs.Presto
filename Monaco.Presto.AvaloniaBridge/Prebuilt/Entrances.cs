namespace Monaco.Presto.AvaloniaBridge.Prebuilt;

/// <summary>
/// Entrance recipes. Each states only where the element starts, so it lands on the element's own resting values, and
/// each becomes a <see cref="MotionTokens.Short"/> fade while <see cref="PrestoDefaults.ReducedMotion"/> is set.
/// </summary>
/// <remarks>
/// Arguments left null come from <see cref="Recipes.Tokens"/>, read when the recipe is called: the duration is
/// <see cref="MotionTokens.Medium"/> and the ease <see cref="MotionTokens.Ease"/>. <c>stagger</c> delays each element of a
/// batch by <see cref="MotionTokens.StaggerStep"/> more than the one before, starting at <c>delay</c>. All but
/// <see cref="BlurIn"/> and <see cref="FlipIn"/> can run through <see cref="PrestoAvalonia.AnimateOnCompositor(object?, AnimationBuilder)"/>.
/// </remarks>
public static class Entrances
{
    /// <summary>Fades in from transparent.</summary>
    public static AnimationBuilder Fade(double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Enter(duration, delay, ease, stagger);

    /// <summary>Fades in while rising <paramref name="distance"/> DIPs (default <see cref="MotionTokens.Distance"/>).</summary>
    public static AnimationBuilder FadeUp(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Enter(duration, delay, ease, stagger).Y(from: distance ?? Recipes.Tokens.Distance);

    /// <summary>Fades in while dropping <paramref name="distance"/> DIPs (default <see cref="MotionTokens.Distance"/>).</summary>
    public static AnimationBuilder FadeDown(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Enter(duration, delay, ease, stagger).Y(from: -(distance ?? Recipes.Tokens.Distance));

    /// <summary>Fades in while moving <paramref name="distance"/> DIPs to the left (default <see cref="MotionTokens.Distance"/>).</summary>
    public static AnimationBuilder FadeLeft(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Enter(duration, delay, ease, stagger).X(from: distance ?? Recipes.Tokens.Distance);

    /// <summary>Fades in while moving <paramref name="distance"/> DIPs to the right (default <see cref="MotionTokens.Distance"/>).</summary>
    public static AnimationBuilder FadeRight(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Enter(duration, delay, ease, stagger).X(from: -(distance ?? Recipes.Tokens.Distance));

    /// <summary>Fades in while growing from <paramref name="scale"/> (default <see cref="MotionTokens.Scale"/>).</summary>
    public static AnimationBuilder ScaleIn(double? scale = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Enter(duration, delay, ease, stagger).Scale(from: scale ?? Recipes.Tokens.Scale);

    /// <summary>Grows from <paramref name="scale"/> (default <see cref="MotionTokens.Scale"/>) with <see cref="MotionTokens.EmphasizedEase"/>, which overshoots in the standard tokens.</summary>
    public static AnimationBuilder PopIn(double? scale = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Enter(duration, delay, ease ?? Recipes.Tokens.EmphasizedEase, stagger).Scale(from: scale ?? Recipes.Tokens.Scale);

    /// <summary>Fades in while sharpening from a blur of <paramref name="radius"/> (default <see cref="MotionTokens.BlurRadius"/>). UI thread only: the compositor cannot blur.</summary>
    public static AnimationBuilder BlurIn(double? radius = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Enter(duration, delay, ease, stagger).Blur(from: radius ?? Recipes.Tokens.BlurRadius);

    /// <summary>Fades in while sliding in from <paramref name="edge"/>, from four times <see cref="MotionTokens.Distance"/> away unless <paramref name="distance"/> is given.</summary>
    public static AnimationBuilder SlideIn(Edge edge, double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false)
    {
        var (property, offset) = Recipes.Offset(edge, distance ?? Recipes.Tokens.Distance * 4);
        return Enter(duration, delay, ease, stagger).Property(property, from: offset);
    }

    /// <summary>
    /// Fades in while swinging about its horizontal axis from <paramref name="angle"/> degrees, in perspective, with
    /// <see cref="MotionTokens.EmphasizedEase"/>. UI thread only: the compositor has no 3D rotation.
    /// </summary>
    public static AnimationBuilder FlipIn(double angle = 90, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Enter(duration, delay, ease ?? Recipes.Tokens.EmphasizedEase, stagger).RotateX(from: angle).Perspective(Recipes.FlipPerspective);

    private static AnimationBuilder Enter(double? duration, double? delay, EaseValue? ease, bool stagger)
    {
        var tokens = Recipes.Tokens;
        return Recipes.Start(tokens, duration ?? tokens.Medium, delay, ease ?? tokens.Ease, stagger)
            .Opacity(from: 0)
            .WhenReduced(reduced => reduced.Opacity(from: 0).Duration(tokens.Short));
    }
}
