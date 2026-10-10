using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoSample.Tests;

public sealed class KineticPoemPageTests
{
    private static readonly string[] Phrases = ["Feel", "Stretch", "Ride", "Fall", "Break", "Melt", "Again"];

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
        var watched = Phrases.SelectMany(phrase => Pieces(page, phrase)).Concat(Shards(page)).Append(page.GetControl<Control>("BeatDot")).ToList();
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

    [AvaloniaFact]
    public void BreakShattersIntoVectorShardsOnTheBeatAndASeekBackBeforeItRestoresTheWord()
    {
        var page = new KineticPoemPage();
        var window = SceneHarness.Show(page, 1000, 900, frames: 2);
        var timeline = Timeline(page);
        var word = Pieces(page, "Break");
        var shards = Shards(page);
        Assert.All(word, piece => Assert.IsType<Path>(piece));
        Assert.True(shards.Count >= 5 * 8, $"only {shards.Count} shards for BREAK");

        timeline.Pause().Seek(timeline.Labels["break"] + 1560);
        var flying = shards.Select(Snapshot).ToList();
        Assert.All(word, piece => Assert.Equal(0, piece.Opacity, 3));
        Assert.Contains(flying, shard => shard.Opacity > .1 && Math.Abs(shard.X) + Math.Abs(shard.Y) > 20);

        timeline.Seek(timeline.Labels["break"] + 1000);
        Assert.All(word, piece => Assert.Equal(1, piece.Opacity, 3));
        Assert.All(shards, shard => Assert.Equal(0, shard.Opacity, 3));

        timeline.Seek(timeline.Labels["break"] + 1560);
        Assert.Equal(string.Join("\n", flying), string.Join("\n", shards.Select(Snapshot)));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheMeltPhraseDripsDownwardsMidPhraseAndStraightensWhenSeekedBack()
    {
        var page = new KineticPoemPage();
        var window = SceneHarness.Show(page, 1000, 900, frames: 2);
        var timeline = Timeline(page);
        var melt = Pieces(page, "Melt").Cast<Path>().ToList();

        double Bottom()
        {
            TestFrames.Tick();
            return melt.Max(piece => piece.Data!.Bounds.Bottom);
        }

        timeline.Pause().Seek(timeline.Labels["melt"] + 500);
        var straight = Bottom();
        timeline.Seek(timeline.Labels["melt"] + 1500);
        Assert.True(Bottom() > straight + 50, "the melt phrase did not drip");

        timeline.Seek(timeline.Labels["melt"] + 500);
        Assert.Equal(straight, Bottom(), 3);
        SceneHarness.Close(window);
    }

    private static List<Control> Shards(KineticPoemPage page) => [.. page.GetControl<Canvas>("Shards").Children];

    private static (double X, double Y, double Scale, double Rotate, double Opacity) Snapshot(Control piece)
    {
        double Read(string property) => Math.Round(Convert.ToDouble(Presto.Get(piece, property, withUnit: false)), 3);
        return (Read("X"), Read("Y"), Read("Scale"), Read("Rotate"), Math.Round(piece.Opacity, 3));
    }

    private static List<Control> Pieces(KineticPoemPage page, string phrase) =>
        page.GetControl<TextBlock>(phrase).GetVisualDescendants().OfType<Control>()
            .Where(piece => piece is Path || (piece is TextBlock text && !string.IsNullOrWhiteSpace(text.Text))).ToList();

    private static Timeline Timeline(KineticPoemPage page) =>
        (Timeline)page.GetControl<TransportBar>("Bar").Timer!;
}
