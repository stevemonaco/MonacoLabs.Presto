using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Prebuilt;

/// <summary>
/// Particle recipes: emitters drawn on the render thread into one overlay canvas per window, laid over
/// <c>anchor</c>, each returning a <see cref="ParticleEffect"/>.
/// </summary>
/// <remarks>
/// While <see cref="PrestoDefaults.ReducedMotion"/> is set they emit nothing and play <c>reduced</c> on the anchor instead,
/// by default <see cref="Recipes.Pulse"/>, which reduced motion turns into an opacity blink; the effect is then already
/// complete. An anchor outside a window emits nothing either.
/// </remarks>
public static class Particles
{
    private static readonly PrestoColor[] s_accents =
        [new(0xF4, 0x3F, 0x5E), new(0xF5, 0x9E, 0x0B), new(0x10, 0xB9, 0x81), new(0x3B, 0x82, 0xF6), new(0x8B, 0x5C, 0xF6), new(0xEC, 0x48, 0x99)];

    /// <summary>
    /// Launches <paramref name="count"/> fluttering, spinning rectangles and streaks in <paramref name="colors"/> (default an
    /// accent palette) upward from <paramref name="anchor"/>'s center, or from along the window's <paramref name="from"/> edge,
    /// and lets gravity bring them down.
    /// </summary>
    public static ParticleEffect Confetti(Control anchor, int count = 120, IReadOnlyList<Color>? colors = null, Edge? from = null, AnimationBuilder? reduced = null)
    {
        if (Overlay(anchor, reduced) is not { } overlay) return ParticleEffect.None;
        var window = overlay.Top.Bounds.Size;
        var (origin, area, angle) = from switch
        {
            Edge.Left => (new Point(0, window.Height / 2), (0d, window.Height), -45d),
            Edge.Top => (new Point(window.Width / 2, 0), (window.Width, 0d), 90d),
            Edge.Right => (new Point(window.Width, window.Height / 2), (0d, window.Height), -135d),
            Edge.Bottom => (new Point(window.Width / 2, window.Height), (window.Width, 0d), -90d),
            _ => (Center(anchor, overlay.Top), default, -90d),
        };
        var pieces = new EmitterParams
        {
            Burst = count - (count / 4),
            Lifetime = (1800, 2600),
            Speed = (0.5, 1.1),
            Angle = angle,
            Spread = from is null ? 35 : 20,
            Gravity = 0.0012,
            Drag = 1.5,
            Spin = (-0.4, 0.4),
            Size = new(8, 8),
            Opacity = new(1, 0, Eases.In(4)),
            Colors = colors?.Select(ColorExtensions.ToPresto).ToArray() ?? s_accents,
            Flutter = 2,
            Area = area,
            Shape = ParticleShape.Rect,
        };
        var streaks = pieces with { Burst = count / 4, Size = new(3, 3), Spin = default, Flutter = 0, Shape = ParticleShape.Streak };
        return overlay.Play([(pieces, origin), (streaks, origin)]);
    }

    /// <summary>
    /// Twinkles <paramref name="rate"/> small circles a second in <paramref name="color"/> (default gold) over
    /// <paramref name="anchor"/>'s bounds until <see cref="ParticleEffect.Stop"/>, each drifting a little and fading quickly.
    /// </summary>
    public static ParticleEffect Sparkles(Control anchor, double rate = 30, Color? color = null, AnimationBuilder? reduced = null)
    {
        if (Overlay(anchor, reduced) is not { } overlay) return ParticleEffect.None;
        var tokens = Recipes.Tokens;
        var sparkles = new EmitterParams
        {
            Rate = rate,
            Lifetime = (tokens.Medium, tokens.Long),
            Speed = (0.005, 0.03),
            Spread = 180,
            Size = new(5, 1, tokens.Ease),
            Opacity = new(1, 0, tokens.Ease),
            Colors = [(color ?? Colors.Gold).ToPresto()],
            Area = (anchor.Bounds.Width, anchor.Bounds.Height),
        };
        return overlay.Play([(sparkles, Center(anchor, overlay.Top))]);
    }

    /// <summary>
    /// Bursts <paramref name="count"/> circles in <paramref name="color"/> (default the first accent) out of
    /// <paramref name="anchor"/>'s center in a ring that slows and fades, as for a press or a like.
    /// </summary>
    public static ParticleEffect Burst(Control anchor, int count = 24, Color? color = null, AnimationBuilder? reduced = null)
    {
        if (Overlay(anchor, reduced) is not { } overlay) return ParticleEffect.None;
        var tokens = Recipes.Tokens;
        var burst = new EmitterParams
        {
            Burst = count,
            Lifetime = (tokens.Medium, tokens.Long),
            Speed = (0.35, 0.4),
            Spread = 180,
            Drag = 6,
            Size = new(6, 2, tokens.Ease),
            Opacity = new(1, 0, tokens.Ease),
            Colors = [color?.ToPresto() ?? s_accents[0]],
        };
        return overlay.Play([(burst, Center(anchor, overlay.Top))]);
    }

    private static ParticleOverlay? Overlay(Control anchor, AnimationBuilder? reduced)
    {
        if (!Engine.Current.Defaults.ReducedMotion) return ParticleOverlay.For(anchor);
        (reduced ?? Recipes.Pulse()).Animate(anchor);
        return null;
    }

    private static Point Center(Control anchor, TopLevel top) =>
        anchor.TranslatePoint(new Point(anchor.Bounds.Width / 2, anchor.Bounds.Height / 2), top) ?? default;
}
