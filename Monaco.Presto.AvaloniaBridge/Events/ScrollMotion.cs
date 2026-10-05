using Avalonia;
using Avalonia.Controls;
using Monaco.Presto.Core;

namespace Monaco.Presto.AvaloniaBridge.Events;

/// <summary>The default motion of an animated scroll (decision 84) and the retargeting tween each moving axis rides.</summary>
internal static class ScrollMotion
{
    private const double PerViewport = 150;

    /// <summary>
    /// Fills what <paramref name="p"/> leaves unset: <see cref="MotionTokens.Short"/> plus 150 ms per viewport travelled,
    /// capped at <see cref="MotionTokens.Long"/>, on <see cref="MotionTokens.Ease"/>.
    /// Under reduced motion, unless <paramref name="p"/> is already a <c>WhenReduced</c> form, the scroll jumps.
    /// </summary>
    public static AnimationParams Apply(AnimationParams p, double viewports)
    {
        if (Engine.Current.Defaults.ReducedMotion && !p.IsReducedMotion) return p with { Duration = 0, Delay = 0 };
        var tokens = Recipes.Tokens;
        return p with
        {
            Duration = p.Duration ?? Math.Min(tokens.Short + PerViewport * viewports, tokens.Long) * Globals.TimeScale,
            Ease = p.Ease ?? tokens.Ease,
        };
    }

    /// <summary>
    /// A <c>"ScrollX"</c>/<c>"ScrollY"</c> tween from <paramref name="from"/> towards <paramref name="requested"/>, aimed each
    /// frame at the part of it the extent holds, so a shrink clamps and a regrowth carries on to the requested offset.
    /// </summary>
    public static TweenParams Tween(ScrollViewer scroller, bool horizontal, double from, double requested) =>
        Tween(scroller, horizontal, from, () => requested);

    /// <inheritdoc cref="Tween(ScrollViewer, bool, double, double)"/>
    /// <remarks>The tween runs its eased progress 0 to 1 and maps it to the offset, rebasing the start when the goal moves so the offset never jumps.</remarks>
    public static TweenParams Tween(ScrollViewer scroller, bool horizontal, double from, Func<double> requested)
    {
        var start = from;
        var goal = Goal();
        return new TweenParams
        {
            From = 0,
            To = 1,
            Modifier = progress =>
            {
                var next = Goal();
                if (next != goal && progress < 1)
                {
                    start = (start + progress * (goal - start) - progress * next) / (1 - progress);
                    goal = next;
                }
                return progress == 1 ? next : start + progress * (goal - start);
            },
        };

        double Goal() => Math.Min(Math.Max(requested(), 0), ScrollWriter.Max(scroller, horizontal));
    }

    /// <summary>How many viewports <paramref name="distance"/> spans, the larger axis when both move.</summary>
    public static double Viewports(Vector distance, Size viewport) =>
        Math.Max(Viewports(distance.X, viewport.Width), Viewports(distance.Y, viewport.Height));

    private static double Viewports(double distance, double viewport) => distance == 0 ? 0 : Math.Abs(distance) / viewport;
}
