using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Prebuilt;

/// <summary>
/// Page transition recipes, each a new <see cref="PrestoPageTransition"/> for a host's <c>PageTransition</c>. All but
/// <see cref="PageBloom"/> move transforms and opacity only, so they play on the render thread, and each side becomes a <see cref="MotionTokens.Short"/>
/// crossfade while <see cref="PrestoDefaults.ReducedMotion"/> is set.
/// </summary>
/// <remarks>
/// Durations left null are <see cref="MotionTokens.Medium"/> and the ease is <see cref="MotionTokens.Ease"/>, read from
/// <see cref="Recipes.Tokens"/> when the recipe is called. Going back plays the forward pair reversed, except
/// <see cref="PageFadeThrough"/> and <see cref="PageBloom"/>, which play the same way both ways. Full-size travel is measured from the host's visual
/// parent when the transition starts; a sliding host needs <c>ClipToBounds</c>.
/// </remarks>
public static class Pages
{
    private const double BloomSwap = 0.65;

    private static readonly ConditionalWeakTable<TopLevel, StrongBox<Point>> s_presses = new();
    private static bool s_tracking;

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

    /// <summary>
    /// Blooms the incoming page over the outgoing one in a <see cref="Reveals.CircularReveal(RelativePoint?, double?, double?, EaseValue?)"/>
    /// from the pointer press that led to the navigation, or from the page's center when a key was pressed since, over
    /// <see cref="MotionTokens.Long"/>. At 65% the children of the incoming page's first <see cref="Panel"/> rise in from
    /// the center out. Both directions bloom.
    /// </summary>
    /// <remarks>
    /// An outgoing page still partly revealed, because its bloom was interrupted, finishes its circle over
    /// <see cref="MotionTokens.Short"/> under the new one, so blooms never stack. Reduced, the incoming page fades in
    /// over <see cref="MotionTokens.Short"/> and its children do not move.
    /// </remarks>
    public static PrestoPageTransition PageBloom()
    {
        TrackPresses();
        var tokens = Recipes.Tokens;
        var bloom = Reveals.Circle((FunctionValue)((target, _, _) => TweenValue.Of(BloomOrigin((Visual)target))), null, null, Eases.InOutQuart)
            .OnBegin(timer => Bloomed((Visual)((Animation)timer).Targets[0], tokens));
        var finish = Presto.Build().ClipCircle(1).Duration(tokens.Short).Ease(tokens.Ease);
        return new() { Enter = bloom, Exit = finish, BackEnter = bloom, BackExit = finish, Stacking = PageStacking.IncomingAbove };
    }

    private static void TrackPresses()
    {
        if (s_tracking) return;
        s_tracking = true;
        InputElement.PointerPressedEvent.AddClassHandler<TopLevel>(
            (top, e) => s_presses.AddOrUpdate(top, new StrongBox<Point>(e.GetPosition(top))), RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>((top, _) => s_presses.Remove(top), RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private static RelativePoint BloomOrigin(Visual host) =>
        TopLevel.GetTopLevel(host) is { } top && s_presses.TryGetValue(top, out var press) && top.TranslatePoint(press.Value, host) is { } at
            ? new RelativePoint(at, RelativeUnit.Absolute)
            : RelativePoint.Center;

    private static void Bloomed(Visual host, MotionTokens tokens)
    {
        // The press is spent here rather than in BloomOrigin, which the bloom reads twice, as its center's start and end.
        if (TopLevel.GetTopLevel(host) is { } top) s_presses.Remove(top);
        if (Engine.Current.Defaults.ReducedMotion || host.GetVisualDescendants().OfType<Panel>().FirstOrDefault() is not { } tiles) return;
        Presto.Build()
            .Y(from: tokens.Distance)
            .Opacity(from: 0)
            .Duration(tokens.Medium)
            .Ease(tokens.Ease)
            .Delay(Presto.Stagger(tokens.StaggerStep, new() { From = StaggerFrom.Center, Start = tokens.Long * BloomSwap }))
            .Animate(tiles.Children);
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
