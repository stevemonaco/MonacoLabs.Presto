using Avalonia;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge.Prebuilt;

/// <summary>
/// Page transition recipes, each a new <see cref="PrestoPageTransition"/> for a host's <c>PageTransition</c>. All move
/// transforms and opacity only, so they play on the render thread, and each side becomes a <see cref="MotionTokens.Short"/>
/// crossfade while <see cref="PrestoDefaults.ReducedMotion"/> is set.
/// </summary>
/// <remarks>
/// Durations left null are <see cref="MotionTokens.Medium"/> and the ease is <see cref="MotionTokens.Ease"/>, read from
/// <see cref="Recipes.Tokens"/> when the recipe is called. Going back plays the forward pair reversed, except
/// <see cref="PageFadeThrough"/>, which stages the same way both ways. Full-size travel is measured from the host's visual
/// parent when the transition starts; a sliding host needs <c>ClipToBounds</c>.
/// </remarks>
public static class Pages
{
    /// <summary>Crossfades the two pages over <paramref name="duration"/>.</summary>
    public static PrestoPageTransition PageFade(double? duration = null)
    {
        var tokens = Recipes.Tokens;
        var ms = duration ?? tokens.Medium;
        return new() { Enter = Side(tokens, ms, enter: true).Opacity(from: 0), Exit = Side(tokens, ms, enter: false).Opacity(0) };
    }

    /// <summary>Material fade-through: the outgoing page fades out in the first 30% of <paramref name="duration"/>, then the incoming one fades in and grows from 0.92.</summary>
    public static PrestoPageTransition PageFadeThrough(double? duration = null)
    {
        var tokens = Recipes.Tokens;
        var ms = duration ?? tokens.Medium;
        var exit = Side(tokens, ms * 0.3, enter: false).Opacity(0);
        var enter = Side(tokens, ms * 0.7, enter: true).Delay(ms * 0.3).Opacity(from: 0).Scale(from: 0.92);
        return new() { Enter = enter, Exit = exit, BackEnter = enter, BackExit = exit };
    }

    /// <summary>
    /// Material shared axis: along <see cref="PageAxis.X"/> or <see cref="PageAxis.Y"/> the pages fade while the incoming one
    /// arrives from <paramref name="distance"/> DIPs (default <see cref="MotionTokens.Distance"/>) ahead and the outgoing one
    /// leaves as far behind; along <see cref="PageAxis.Z"/> the incoming page grows from <see cref="MotionTokens.Scale"/> and
    /// the outgoing one grows as much past full size, and <paramref name="distance"/> is unused.
    /// </summary>
    public static PrestoPageTransition PageSharedAxis(PageAxis axis, double? distance = null)
    {
        var tokens = Recipes.Tokens;
        var enter = Side(tokens, tokens.Medium, enter: true).Opacity(from: 0);
        var exit = Side(tokens, tokens.Short, enter: false).Opacity(0);
        var d = distance ?? tokens.Distance;
        switch (axis)
        {
            case PageAxis.X:
                enter.X(from: d);
                exit.X(-d);
                break;
            case PageAxis.Y:
                enter.Y(from: d);
                exit.Y(-d);
                break;
            default:
                enter.Scale(from: tokens.Scale);
                exit.Scale(2 - tokens.Scale);
                break;
        }
        return new() { Enter = enter, Exit = exit };
    }

    /// <summary>Both pages slide the full width or height of the host: the incoming one in from <paramref name="edge"/>, the outgoing one out the opposite side.</summary>
    public static PrestoPageTransition PageSlide(Edge edge)
    {
        var (enter, exit) = Slide(edge, -1);
        return new() { Enter = enter, Exit = exit };
    }

    /// <summary>iOS push: the incoming page slides over from <paramref name="edge"/> while the outgoing one moves a third of the way out the opposite side and dims to 60%.</summary>
    public static PrestoPageTransition PagePush(Edge edge)
    {
        var (enter, exit) = Slide(edge, -1 / 3d);
        return new() { Enter = enter, Exit = exit.Opacity(0.6) };
    }

    private static (AnimationBuilder Enter, AnimationBuilder Exit) Slide(Edge edge, double exitFraction)
    {
        var tokens = Recipes.Tokens;
        var property = Recipes.Offset(edge, 0).Property;
        return (Side(tokens, tokens.Medium, enter: true).Property(property, from: Across(edge, 1)),
            Side(tokens, tokens.Medium, enter: false).Property(property, Across(edge, exitFraction)));
    }

    private static AnimationBuilder Side(MotionTokens tokens, double duration, bool enter) =>
        Recipes.Start(tokens, duration, null, tokens.Ease, stagger: false)
            .WhenReduced(reduced => (enter ? reduced.Opacity(from: 0) : reduced.Opacity(0)).Delay(0).Duration(tokens.Short));

    private static FunctionValue Across(Edge edge, double fraction) => (target, _, _) =>
    {
        var host = (Visual)target;
        var size = (host.GetVisualParent() ?? host).Bounds.Size;
        return Recipes.Offset(edge, (edge is Edge.Left or Edge.Right ? size.Width : size.Height) * fraction).Offset;
    };
}
