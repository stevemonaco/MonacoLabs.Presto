using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Monaco.Presto;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class KineticPoemPageTests
{
    private static readonly string[] Phrases = ["Feel", "Stretch", "Ride", "Fall", "Break", "Again"];

    [AvaloniaFact]
    public void EachSeekButtonShowsItsPhraseAndHidesTheOthers()
    {
        var page = new KineticPoemPage();
        var window = SceneHarness.Show(page, 1000, 900, frames: 2);
        var timeline = Timeline(page);
        timeline.Pause();

        for (var p = 0; p < Phrases.Length; p++)
        {
            var label = Phrases[p].ToLowerInvariant();
            SceneHarness.Click(window, page.GetLogicalDescendants().OfType<Button>().Single(button => button.Tag as string == label));
            Assert.Equal(timeline.Labels[label], timeline.CurrentTime, 3);
            timeline.Seek(timeline.Labels[label] + 1000);
            for (var other = 0; other < Phrases.Length; other++)
            {
                var pieces = Pieces(page, Phrases[other]);
                if (other == p) Assert.Contains(pieces, piece => piece.Opacity > .5);
                else Assert.All(pieces, piece => Assert.Equal(0, piece.Opacity, 3));
            }
        }
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ReversedFromItsEndEveryPieceReturnsToItsStartValues()
    {
        var page = new KineticPoemPage();
        var window = SceneHarness.Show(page, 1000, 900, frames: 2);
        var timeline = Timeline(page);
        timeline.Pause().Seek(0);
        var pieces = Phrases.SelectMany(phrase => Pieces(page, phrase)).ToList();
        var start = pieces.Select(Snapshot).ToList();

        timeline.Seek(timeline.Duration);
        timeline.Reverse();
        for (var frame = 0; frame < 1000 && timeline.IterationCurrentTime > 0; frame++) TestFrames.Tick();

        Assert.Equal(0, timeline.IterationCurrentTime, 3);
        Assert.Equal(string.Join("\n", start), string.Join("\n", pieces.Select(Snapshot)));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionOnlyOpacityChangesWhilePlaying()
    {
        using var reduced = ReducedMotion.On();
        var page = new KineticPoemPage();
        var window = SceneHarness.Show(page, 1000, 900, frames: 2);
        var timeline = Timeline(page);
        var watched = Phrases.SelectMany(phrase => Pieces(page, phrase)).Append(page.GetControl<Control>("BeatDot")).ToList();
        var shown = new HashSet<Control>();

        for (var frame = 0; frame < 900 && !timeline.Completed; frame++)
        {
            TestFrames.Tick();
            foreach (var control in watched)
            {
                var s = Snapshot(control);
                Assert.Equal((0d, 0d, 1d, 0d), (s.X, s.Y, s.Scale, s.Rotate));
                if (control.Opacity > .5) shown.Add(control);
            }
        }

        Assert.True(timeline.Completed, "the poem never finished");
        Assert.All(Phrases, phrase => Assert.Contains(Pieces(page, phrase), shown.Contains));
        SceneHarness.Close(window);
    }

    private static (double X, double Y, double Scale, double Rotate, double Opacity) Snapshot(Control piece)
    {
        double Read(string property) => Math.Round(Convert.ToDouble(Presto.Get(piece, property, withUnit: false)), 3);
        return (Read("X"), Read("Y"), Read("Scale"), Read("Rotate"), Math.Round(piece.Opacity, 3));
    }

    private static List<Control> Pieces(KineticPoemPage page, string phrase) =>
        page.GetControl<TextBlock>(phrase).GetVisualDescendants().OfType<TextBlock>().Where(piece => !string.IsNullOrWhiteSpace(piece.Text)).Cast<Control>().ToList();

    private static Timeline Timeline(KineticPoemPage page) =>
        (Timeline)page.GetControl<TransportBar>("Bar").Timer!;
}
