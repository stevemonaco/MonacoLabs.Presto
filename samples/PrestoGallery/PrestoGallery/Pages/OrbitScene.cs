using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Compositor;
using PrestoGallery.Showcase;
using Timer = MonacoLabs.Presto.Timer;

namespace PrestoGallery.Pages;

/// <summary>
/// Three staggered rings of dots and a comet orbiting them, ticked and drawn on the render thread, with the render
/// thread's fps and, once the UI thread's <see cref="Heartbeat"/> goes stale, how long the UI thread has been frozen.
/// </summary>
public sealed class OrbitScene : CompositorScene
{
    public const double Extent = 200;
    internal const double DotRadius = 4.5;
    internal const double CometOrbit = 88;
    internal const int TailLength = 14;

    private const double StaleAfterMs = 250;

    /// <summary>Innermost ring first; each ring takes the next brush.</summary>
    internal static readonly IReadOnlyList<ImmutableSolidColorBrush> RingBrushes = [Palette.AccentPale, Palette.AccentBright, Palette.Accent];

    /// <summary>Every dot's center within an <see cref="Extent"/> square and the ring it belongs to, in stagger order.</summary>
    internal static readonly IReadOnlyList<(Point Center, int Ring)> Dots =
    [
        .. new[] { (Radius: 26d, Count: 6), (Radius: 46d, Count: 12), (Radius: 66d, Count: 18) }.SelectMany((ring, r) =>
            Enumerable.Range(0, ring.Count).Select(i =>
            {
                var angle = (i * Math.Tau / ring.Count) - (Math.PI / 2);
                return (new Point((Extent / 2) + (Math.Cos(angle) * ring.Radius), (Extent / 2) + (Math.Sin(angle) * ring.Radius)), r);
            })),
    ];

    private static readonly Point FpsOrigin = new(10, 8);

    private readonly Dot[] _dots = [.. Dots.Select(_ => new Dot())];
    private readonly Segment[] _tail = [.. Enumerable.Range(0, TailLength).Select(_ => new Segment())];
    private readonly SceneText _text = new(12);

    private long _beat;
    private int _frames;
    private double _windowStart;
    private string _fps = FpsMeter.Format("render", 0);
    private int _frozenTenths;
    private string _frozen = "";

    static OrbitScene()
    {
        Presto.RegisterTarget<Dot>();
        Presto.RegisterTarget<Segment>();
    }

    /// <summary>The ripple both panels play over <see cref="Dots"/>.</summary>
    internal static AnimationParams RingAnimation => new()
    {
        ["Opacity"] = new double[] { 1, .2 },
        ["Scale"] = new double[] { 1, .45 },
        Duration = 1000,
        Delay = Presto.Stagger(35),
        Ease = Eases.InOutSine,
        Loop = true,
        Alternate = true,
    };

    /// <summary>The orbit both panels play over the comet's <see cref="TailLength"/> segments, head first; the stagger is what trails the tail.</summary>
    internal static AnimationParams CometAnimation => new()
    {
        ["Rotate"] = 360,
        Duration = 2400,
        Delay = Presto.Stagger(24),
        Ease = Eases.None,
        Loop = true,
    };

    /// <summary>How long the UI thread's heartbeat has been stale, or 0 while it is fresh.</summary>
    public double FrozenFor { get; private set; }

    internal static double TailRadius(int index) => 6 * (1 - (index / (TailLength + 4d)));

    internal static double TailOpacity(int index) => 1 - (index / (double)TailLength);

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        _beat = Stopwatch.GetTimestamp();
        Presto.Animate(_dots, RingAnimation);
        Presto.Animate(_tail, CometAnimation);
        Presto.CreateTimer(new TimerParams { OnUpdate = Step });
    }

    /// <inheritdoc/>
    protected override void OnMessage(object message)
    {
        if (message is Heartbeat(var timestamp)) _beat = timestamp;
    }

    /// <inheritdoc/>
    protected override void OnRender(ImmediateDrawingContext context, Size size)
    {
        for (var i = 0; i < _dots.Length; i++)
        {
            var (center, ring) = Dots[i];
            var radius = DotRadius * _dots[i].Scale;
            context.DrawEllipse(Brush(RingBrushes[ring].Color, _dots[i].Opacity), null, center, radius, radius);
        }

        for (var i = _tail.Length - 1; i >= 0; i--)
        {
            var angle = _tail[i].Rotate * Math.PI / 180;
            var center = new Point((Extent / 2) + (Math.Sin(angle) * CometOrbit), (Extent / 2) - (Math.Cos(angle) * CometOrbit));
            context.DrawEllipse(Brush(Palette.Aqua.Color, TailOpacity(i)), null, center, TailRadius(i), TailRadius(i));
        }

        _text.Draw(context, _fps, FpsOrigin, Brush(Palette.InkDim.Color));
        if (FrozenFor == 0) return;
        var width = _text.Measure(_frozen);
        var pill = new Rect((size.Width - width) / 2 - 10, size.Height - _text.Height - 16, width + 20, _text.Height + 8);
        context.FillRectangle(Brush(Palette.SurfaceHigh.Color), pill, 6);
        _text.Draw(context, _frozen, new Point(pill.X + 10, pill.Y + 4), Brush(Palette.Ink.Color));
    }

    private void Step(Timer timer)
    {
        var stale = Stopwatch.GetElapsedTime(_beat).TotalMilliseconds;
        FrozenFor = stale > StaleAfterMs ? stale : 0;
        var tenths = (int)(FrozenFor / 100);
        if (tenths != _frozenTenths)
        {
            _frozenTenths = tenths;
            _frozen = string.Create(CultureInfo.InvariantCulture, $"UI thread frozen for {tenths / 10d:0.0} s");
        }

        if (FpsMeter.TrySample(timer, ref _frames, ref _windowStart, out var fps)) _fps = FpsMeter.Format("render", fps);
    }

    /// <summary>The UI thread's <see cref="Stopwatch.GetTimestamp"/>, posted once per UI frame.</summary>
    public sealed record Heartbeat(long Timestamp);

    private sealed class Dot
    {
        public double Opacity { get; set; } = 1;

        public double Scale { get; set; } = 1;
    }

    private sealed class Segment
    {
        public double Rotate { get; set; }
    }
}
