using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Text;

namespace PrestoSample.Showcase.Pages;

public partial class TitleSequencePage : ShowcasePage
{
    private const int DotCount = 28;

    private readonly double[] _drifts = new double[DotCount];
    private Timeline? _timeline;
    private int _crossings;

    public TitleSequencePage() => InitializeComponent();

    protected override void OnAttached()
    {
        BuildDotField();
        PrestoAvalonia.CreateDrawable(Monogram);
        // The character pieces only exist once the block has been measured, and a re-split replaces them, so the whole
        // sequence is built from the split effect: KeepTime hands the playhead to the rebuilt one (SAMPLES-PLAN §8).
        var split = PrestoAvalonia.SplitText(Headline, new SplitTextParams { Chars = new SplitPieceParams { Class = "wordmark-char" } });
        if (Presto.Defaults.ReducedMotion) split.AddEffect(LandSequence);
        else split.AddEffect(BuildSequence);
    }

    // One jump from the start would render the cards' Set at 0 after their fade-up, so the playhead passes the cards label first.
    private Action LandSequence(TextSplitter split)
    {
        var timeline = BuildSequence(split);
        timeline.Pause().Seek(timeline.Labels["cards"]).Seek(timeline.IterationDuration);
        return () => timeline.Revert();
    }

    private void BuildDotField()
    {
        var random = Utils.CreateSeededRandom(19);
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
            Canvas.SetLeft(dot, random(0, DotField.Width));
            Canvas.SetTop(dot, random(0, DotField.Height));
            _drifts[i] = random(-70, 70);
            DotField.Children.Add(dot);
        }
    }

    private Timeline BuildSequence(TextSplitter split)
    {
        #region snippet TitleSequenceTimeline
        var timeline = Presto.CreateTimeline()
            .Label("draw", 0)
            .Add(Monogram, new()
            {
                ["Draw"] = TweenValue.FromTo("0 0", "0 1"),
                Duration = 1200,
                Ease = Eases.InOutQuad,
            }, "draw")
            .Label("mark", "+=100")
            .Add(Monogram, new()
            {
                ["Data"] = PrestoAvalonia.MorphTo((Geometry)this.FindResource("MarkGeometry")!),
                ["Fill"] = "#554C8DFF",
                Duration = 800,
                Ease = Eases.InOutQuad,
            }, "mark")
            .Label("title", "<")
            .Add(split.Chars, new()
            {
                ["Y"] = TweenValue.FromTo(48, 0),
                ["Scale"] = TweenValue.FromTo(0.6, 1),
                ["Opacity"] = TweenValue.FromTo(0, 1),
                Duration = 760,
                Ease = Eases.OutExpo,
                Delay = Presto.Stagger(70, new() { From = StaggerFrom.Center }),
            }, "title")
            .Add(Subtitle, new()
            {
                ["Text"] = PrestoAvalonia.ScrambleText(new() { Chars = "lowercase", From = ScrambleFrom.Center }),
                ["Opacity"] = TweenValue.FromTo(0, 1),
                Duration = 1200,
                Ease = Eases.OutQuad,
            }, "<<")
            .Label("cards", "+=200")
            .Add(Cards.Children, Recipes.FadeUp(distance: 28, duration: 900, ease: Eases.OutQuart).Delay(Presto.Stagger(120)), "cards")
            // Added after the fade-up, which would otherwise land on this 0 rather than the cards' resting opacity.
            .Set(Cards.Children, b => b.Opacity(0), 0)
            .Label("out", "+=400")
            .Add(new TimerParams { Duration = 800 }, "out")
            .Sync(BuildDrift(), 0);
        #endregion

        timeline.Call(_ => BeatText.Text = $"out · {++_crossings} crossings", "out");
        _timeline = timeline;
        Bar.Timer = timeline;
        return timeline;
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
                ["X"] = (FunctionValue)((_, i, _) => _drifts[i] * 0.45),
                ["Y"] = (FunctionValue)((_, i, _) => _drifts[i]),
                Duration = 2000,
                Ease = Eases.InOutSine,
                Loop = 1,
                Alternate = true,
                Delay = Presto.Stagger(28),
            }, 0);

    private void SeekLabel(object? sender, RoutedEventArgs e)
    {
        if (_timeline is not { } timeline || sender is not Button { Tag: string label }) return;
        timeline.Seek(timeline.Labels[label]);
        Bar.Refresh();
    }
}
