using Avalonia;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace PrestoSample.Pages;

/// <summary>The anime.js staggering example as a busy indicator: a ring of dots fading out from the centre, ticked and drawn on the render thread.</summary>
public sealed class DotRingScene : CompositorScene
{
    internal const int DotCount = 12;
    internal const double RingRadius = 22;
    internal const double DotRadius = 4;

    private static readonly Color DotColor = Color.Parse("#4c8dff");

    private readonly Dot[] _dots = [.. Enumerable.Range(0, DotCount).Select(_ => new Dot())];

    /// <summary>The anime.js staggering parameters, shared with the page so the UI-thread ring is animated identically.</summary>
    internal static AnimationParams RingAnimation => new()
    {
        ["Opacity"] = new double[] { 1, 0.15 },
        Duration = 900,
        Delay = Presto.Stagger(75, new() { From = StaggerFrom.Center }),
        Ease = Eases.InOutSine,
        Loop = true,
    };

    /// <summary>The angle of the dot at <paramref name="index"/>, measured from the top of the ring.</summary>
    internal static double AngleOf(int index) => index * Math.Tau / DotCount - Math.PI / 2;

    protected override void OnAttached() => Presto.Animate(_dots, RingAnimation);

    protected override void OnRender(ImmediateDrawingContext context, Size size)
    {
        for (var i = 0; i < _dots.Length; i++)
        {
            var angle = AngleOf(i);
            var center = new Point(
                size.Width / 2 + Math.Cos(angle) * RingRadius,
                size.Height / 2 + Math.Sin(angle) * RingRadius);
            context.DrawEllipse(Brush(DotColor, _dots[i].Opacity), null, center, DotRadius, DotRadius);
        }
    }

    private sealed class Dot
    {
        public double Opacity { get; set; } = 1;
    }
}
