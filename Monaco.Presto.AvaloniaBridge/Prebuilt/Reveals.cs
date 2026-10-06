using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media.Imaging;

namespace Monaco.Presto.AvaloniaBridge.Prebuilt;

/// <summary>
/// Circular reveals over the <c>ClipCircle</c> and <c>ClipCircleCenter</c> shorthands: the element shows inside a circle
/// that grows from an origin until it reaches the farthest corner, where the clip is removed. Each becomes a
/// <see cref="MotionTokens.Short"/> fade while <see cref="PrestoDefaults.ReducedMotion"/> is set.
/// </summary>
/// <remarks>
/// Durations left null are <see cref="MotionTokens.Long"/> and the ease is <see cref="MotionTokens.Ease"/>, read from
/// <see cref="Recipes.Tokens"/> when the recipe is called. UI thread only: the compositor cannot clip.
/// </remarks>
public static class Reveals
{
    private static readonly ConditionalWeakTable<Visual, Action> s_running = new();

    /// <summary>Reveals the element from <paramref name="origin"/>, its centre when null.</summary>
    public static AnimationBuilder CircularReveal(RelativePoint? origin = null, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Circle(TweenValue.Of(origin ?? RelativePoint.Center), duration, delay, ease);

    /// <summary>Reveals the element from the centre of <paramref name="from"/>, such as the button that opened it.</summary>
    public static AnimationBuilder CircularReveal(Visual from, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Circle((FunctionValue)((target, _, _) => TweenValue.Of(
            from.TranslatePoint(new Point(from.Bounds.Width / 2, from.Bounds.Height / 2), (Visual)target) is { } centre
                ? new RelativePoint(centre, RelativeUnit.Absolute)
                : RelativePoint.Center)), duration, delay, ease);

    /// <summary>
    /// Reveals a change to <paramref name="surface"/>, such as a theme switch, in a circle grown from the centre of
    /// <paramref name="from"/> (the surface's centre when null): the surface is drawn before and after
    /// <paramref name="change"/> runs, and the new picture is revealed over the old one in the surface's overlay layer,
    /// both removed when the reveal ends.
    /// </summary>
    /// <remarks>
    /// A reveal started on a surface while another runs there removes the earlier one; a <see cref="TopLevel"/> surface
    /// draws its overlay layer too, so the new reveal starts from what was on screen. Reduced, the new picture fades in.
    /// </remarks>
    /// <exception cref="InvalidOperationException"><paramref name="surface"/> is not in a window with an overlay layer.</exception>
    public static Animation ThemeReveal(Visual surface, Action change, Visual? from = null, double? duration = null, EaseValue? ease = null)
    {
        var layer = OverlayLayer.GetOverlayLayer(surface) ?? throw new InvalidOperationException("The surface is not in a window with an overlay layer.");
        var before = Snapshot(surface);
        if (s_running.TryGetValue(surface, out var running)) running();
        change();
        (surface as Layoutable)?.UpdateLayout();
        var after = Snapshot(surface);

        var size = surface.Bounds.Size;
        var revealed = new Image { Source = after, Width = size.Width, Height = size.Height };
        var pictures = new Panel
        {
            IsHitTestVisible = false,
            Children = { new Image { Source = before, Width = size.Width, Height = size.Height }, revealed },
        };
        var at = surface.TranslatePoint(default, layer) ?? default;
        Canvas.SetLeft(pictures, at.X);
        Canvas.SetTop(pictures, at.Y);
        layer.Children.Add(pictures);

        var origin = from?.TranslatePoint(new Point(from.Bounds.Width / 2, from.Bounds.Height / 2), surface) is { } point
            ? new RelativePoint(point, RelativeUnit.Absolute)
            : RelativePoint.Center;
        var reveal = CircularReveal(origin, duration, ease: ease).Animate(revealed);
        Action remove = null!;
        remove = () =>
        {
            if (!layer.Children.Remove(pictures)) return;
            reveal.Revert();
            before.Dispose();
            after.Dispose();
            if (s_running.TryGetValue(surface, out var current) && current == remove) s_running.Remove(surface);
        };
        s_running.AddOrUpdate(surface, remove);
        _ = reveal.Then(_ => remove());
        return reveal;
    }

    internal static AnimationBuilder Circle(TweenValue centre, double? duration, double? delay, EaseValue? ease)
    {
        var tokens = Recipes.Tokens;
        return Recipes.Start(tokens, duration ?? tokens.Long, delay, ease ?? tokens.Ease, stagger: false)
            .ClipCircle(to: 1, from: 0)
            .ClipCircleCenter(to: centre, from: centre)
            .WhenReduced(reduced => reduced.Opacity(from: 0).Duration(tokens.Short));
    }

    private static RenderTargetBitmap Snapshot(Visual surface)
    {
        var scale = TopLevel.GetTopLevel(surface)?.RenderScaling ?? 1;
        var size = surface.Bounds.Size;
        var bitmap = new RenderTargetBitmap(
            new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width * scale)), Math.Max(1, (int)Math.Ceiling(size.Height * scale))),
            new Vector(96 * scale, 96 * scale));
        bitmap.Render(surface);
        return bitmap;
    }
}
