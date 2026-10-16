using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Text;
using MonacoLabs.Presto.Values;
using static MonacoLabs.Presto.PrestoKeys;

namespace PrestoGallery.Showcase.Pages;

public partial class TitleSequencePage : ShowcasePage
{
    private const int Columns = 7;
    private const int Rows = 4;
    private const int DotCount = Columns * Rows;
    private const double CometFlight = 1100;

    private readonly double[] _drifts = new double[DotCount];
    private Timeline? _timeline;
    private int _crossings;

    public TitleSequencePage() => InitializeComponent();

    protected override void OnAttached()
    {
        BuildDotField();
        PrestoAvalonia.CreateDrawable(Monogram);
        PrestoAvalonia.CreateDrawable(Trail);
        AfterFirstArrange(Headline, Build);
    }

    // One jump from the start would render the cards' Set at 0 after their flip, so the playhead passes the cards label first.
    private void Build()
    {
        var timeline = BuildSequence();
        if (Presto.Defaults.ReducedMotion) timeline.Pause().Seek(timeline.Labels["cards"]).Seek(timeline.IterationDuration);
    }

    private void BuildDotField()
    {
        var random = Utils.CreateSeededRandom(19);
        var (cellWidth, cellHeight) = (DotField.Width / Columns, DotField.Height / Rows);
        for (var i = 0; i < DotCount; i++)
        {
            var size = random(3, 8);
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Opacity = 0,
                Fill = Palette.AccentBrushRamp[(int)random(0, Palette.AccentBrushRamp.Count - 1)],
            };
            Canvas.SetLeft(dot, (i % Columns + .5) * cellWidth + random(-cellWidth / 3, cellWidth / 3));
            Canvas.SetTop(dot, (i / Columns + .5) * cellHeight + random(-cellHeight / 3, cellHeight / 3));
            _drifts[i] = random(-70, 70);
            DotField.Children.Add(dot);
        }
    }

    private Timeline BuildSequence()
    {
        #region snippet TitleSequenceTimeline
        var chars = PrestoAvalonia.SplitText(Headline, new SplitTextParams { Words = null, Chars = new SplitPieceParams(), Outlines = true }).Chars;
        var flip = Eases.Spring(bounce: .35, duration: 700);
        var timeline = Presto.CreateTimeline()
            .Label("open", 0)
            .Add(new[] { TopBar, BottomBar }, new() { [ScaleY] = TweenValue.FromTo(0, 1), Duration = 700, Ease = Eases.OutCubic }, "open")
            .Sync(BuildComet(), "open")
            .Label("draw", CometFlight)
            .Add(Monogram, new()
            {
                [Draw] = TweenValue.FromTo("0 0", "0 1"),
                [Scale] = new TweenParams { From = .6, To = 1, Ease = Eases.Spring(bounce: .5, duration: 700) },
                Duration = 1200,
                Ease = Eases.InOutQuad,
            }, "draw")
            .Label("mark", "draw+=1300")
            .Add(Monogram, new()
            {
                [Data] = PrestoAvalonia.MorphTo((Geometry)this.FindResource("MarkGeometry")!),
                Duration = 800,
                Ease = Eases.InOutQuad,
            }, "mark")
            .Add(Monogram, b => b.Property("Fill", k => k.Color("#FF6B7A00", "#4C8DFF88", ColorSpace.Oklch, HueInterpolation.Longer,
                                                                 duration: 1000, ease: Eases.InOutQuad)), "mark")
            // Additive, so the pulse and the push sum with the drift's X and Y instead of replacing them; each returns to where it began.
            .Add(DotField.Children, new()
            {
                [Scale] = new TweenParams[] { new() { To = 2.2, Duration = 260, Ease = Eases.OutQuad }, new() { To = 1, Duration = 560, Ease = Eases.InOutSine } },
                [X] = new TweenParams[] { new() { To = (FunctionValue)((_, i, _) => (i % Columns - 3) * 6), Duration = 260 }, new() { To = 0, Duration = 560 } },
                [Y] = new TweenParams[] { new() { To = (FunctionValue)((_, i, _) => (i / Columns - 1.5) * 8), Duration = 260 }, new() { To = 0, Duration = 560 } },
                Delay = Presto.Stagger(45, new() { Grid = [Columns, Rows], From = StaggerFrom.Center }),
                Composition = Composition.Additive,
            }, "mark")
            .Label("title", "<")
            .Add(chars, new()
            {
                [RotateX] = TweenValue.FromTo(-80, 0),
                [Blur] = TweenValue.FromTo(12, 0),
                ["Opacity"] = TweenValue.FromTo(0, 1),
                Duration = 900,
                Ease = Eases.OutCubic,
                Delay = Presto.Stagger(70, new() { From = StaggerFrom.Center }),
            }, "title")
            .Add(Headline, new()
            {
                [Warp] = new TweenParams[] { new() { To = .4 }, new() { To = 0 } },
                [WarpPhase] = TweenValue.FromTo(0, 2 * Math.PI),
                Duration = 1000,
                Ease = Eases.InOutSine,
            }, "title+=900")
            .Label("subtitle", "title+=900")
            .Add(Subtitle, new()
            {
                [Text] = Presto.ScrambleText(new() { Chars = "lowercase", From = ScrambleFrom.Center }),
                ["Opacity"] = new TweenParams { From = 0, To = 1, Ease = Eases.Rough(points: 8, clamp: true, randomize: false, taper: RoughTaper.Out) },
                Duration = 1200,
                Ease = Eases.OutQuad,
            }, "subtitle")
            .Label("shine", "subtitle+=1000")
            .Add(HeadlineShine, new()
            {
                [ClipInset] = new TweenParams[]
                {
                    new() { From = "0% 0 100% 0", To = "0% 0 85% 0", Duration = 120 },
                    new() { To = "85% 0 0% 0", Duration = 660 },
                    new() { To = "100% 0 0% 0", Duration = 120 },
                },
                Ease = Eases.None,
            }, "shine")
            .Label("cards", "+=200")
            .Add(Cards.Children, new()
            {
                [RotateY] = new TweenParams { From = 70, To = 0, Ease = flip },
                [Y] = new TweenParams { From = 28, To = 0, Ease = flip },
                ["Opacity"] = TweenValue.FromTo(0, 1),
                Duration = 900,
                Ease = Eases.OutQuart,
                Delay = Presto.Stagger(120),
            }, "cards")
            // Added after the tweens they hold before, which would otherwise land on these rather than their resting values;
            // an explicit from is written only once its tween starts, so without them a scrub back would differ from a scrub forward.
            .Set(Cards.Children, new() { ["Opacity"] = 0, [RotateY] = 70, [Y] = 28, [Perspective] = 800 }, 0)
            .Set(chars, new() { ["Opacity"] = 0, [RotateX] = -80, [Blur] = 12, [Perspective] = 600 }, 0)
            .Set(HeadlineShine, new() { [ClipInset] = "0% 0 100% 0" }, 0)
            .Label("out", "+=400")
            .Add(new TimerParams { Duration = 800 }, "out")
            .Sync(BuildDrift(), 0);
        timeline.Add(Camera, new() { [Scale] = TweenValue.FromTo(1.08, 1), Duration = timeline.Duration, Ease = Eases.OutSine }, "open");
        #endregion

        timeline.Call(_ => BeatText.Text = $"out · {++_crossings} crossings", "out");
        _timeline = timeline;
        Bar.Timer = timeline;
        return timeline;
    }

    private Timeline BuildComet()
    {
        var flight = PrestoAvalonia.CreateMotionPath(Trail);
        return Presto.CreateTimeline()
            .Add(Comet, new()
            {
                [X] = flight.X,
                [Y] = flight.Y,
                [Rotate] = flight.Rotate,
                ["Opacity"] = new TweenParams { From = 0, To = 1, Duration = 150 },
                [ShadowColor] = "#82B4FF",
                [ShadowBlur] = 16,
                [ShadowOpacity] = 1,
                Duration = CometFlight,
                Ease = Eases.InOutSine,
            }, 0)
            .Add(Trail, new()
            {
                [Draw] = new TweenParams[]
                {
                    new() { From = "0 0", To = "0 .5", Duration = CometFlight * .45 },
                    new() { To = "0.6 1", Duration = CometFlight * .3 },
                    new() { To = "1 1", Duration = CometFlight * .25 },
                },
                Ease = Eases.InOutSine,
            }, 0)
            .Add(Comet, new() { [Scale] = 0, ["Opacity"] = 0, Duration = 300, Ease = Eases.OutQuad }, CometFlight);
    }

    private Timeline BuildDrift() =>
        Presto.CreateTimeline()
            .Add(DotField.Children, new()
            {
                ["Opacity"] = TweenValue.FromTo(0, 0.7),
                Duration = 900,
                Delay = Presto.Stagger(30),
            }, 0)
            .Add(DotField.Children, new()
            {
                [X] = new TweenParams[] { new() { To = (FunctionValue)((_, i, _) => _drifts[i] * 0.45) }, new() { To = 0 } },
                [Y] = new TweenParams[] { new() { To = (FunctionValue)((_, i, _) => _drifts[i]) }, new() { To = 0 } },
                Duration = 4000,
                Ease = Eases.InOutSine,
                Delay = Presto.Stagger(28),
                Composition = Composition.Additive,
            }, 0);

    private void SeekLabel(object? sender, RoutedEventArgs e)
    {
        if (_timeline is not { } timeline || sender is not Button { Tag: string label }) return;
        timeline.Seek(timeline.Labels[label]);
        Bar.Refresh();
    }
}
