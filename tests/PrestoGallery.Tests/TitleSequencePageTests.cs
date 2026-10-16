using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;
using PrestoGallery.Showcase.Pages;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoGallery.Tests;

public sealed class TitleSequencePageTests
{
    [AvaloniaFact]
    public void CardsFlipInOnceWithoutEverDimmingAndStartHiddenAgainAfterARestart()
    {
        var page = new TitleSequencePage();
        var window = SceneHarness.Show(page, 1000, 900);
        var cards = page.GetControl<StackPanel>("Cards").Children;
        Assert.All(cards, card => Assert.Equal(0, card.Opacity));

        var timeline = Timeline(page);
        var previous = new double[cards.Count];
        for (var frame = 0; frame < 600 && !timeline.Completed; frame++)
        {
            TestFrames.Tick();
            for (var i = 0; i < cards.Count; i++)
            {
                Assert.True(
                    cards[i].Opacity >= previous[i],
                    $"card {i} dimmed from {previous[i]:0.000} to {cards[i].Opacity:0.000} at {timeline.CurrentTime:0} ms");
                previous[i] = cards[i].Opacity;
            }
        }
        Assert.True(timeline.Completed, "the sequence never finished");
        Assert.All(cards, card =>
        {
            Assert.Equal(1, card.Opacity, 3);
            Assert.Equal(0, Read(card, "RotateY"), 3);
            Assert.Equal(0, Read(card, "Y"), 3);
        });

        timeline.Restart();
        TestFrames.Tick();
        Assert.All(cards, card => Assert.Equal(0, card.Opacity));

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheTitleLandsUprightUnblurredAndUnwarpedAndIsHiddenAgainWhenSeekedToOpen()
    {
        var page = new TitleSequencePage();
        var window = SceneHarness.Show(page, 1000, 900);
        var headline = page.GetControl<TextBlock>("Headline");
        var pieces = headline.GetVisualDescendants().OfType<Path>().ToList();
        Assert.Equal("PRESTO".Length, pieces.Count);

        var timeline = Timeline(page);
        for (var frame = 0; frame < 600 && !timeline.Completed; frame++) TestFrames.Tick();
        Assert.True(timeline.Completed, "the sequence never finished");
        Assert.All(pieces, piece =>
        {
            Assert.Equal(1, piece.Opacity, 3);
            Assert.Equal(0, Read(piece, "RotateX"), 3);
            Assert.Equal(0, Read(piece, "Blur"), 3);
        });
        Assert.Equal(0, Read(headline, "Warp"), 3);
        var shine = page.GetControl<TextBlock>("HeadlineShine");
        Assert.Equal(0, ((RectangleGeometry)shine.Clip!).Rect.Width);

        SceneHarness.Click(window, page.GetLogicalDescendants().OfType<Button>().Single(button => button.Tag is "open"));
        Assert.All(pieces, piece => Assert.Equal(0, piece.Opacity));
        Assert.Equal(0, ((RectangleGeometry)shine.Clip!).Rect.Width);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void SeekingToTheCardsLabelLandsOnItAfterTheSubtitleHasLanded()
    {
        var page = new TitleSequencePage();
        var window = SceneHarness.Show(page, 1000, 900, frames: 10);
        var timeline = Timeline(page);
        timeline.Pause();

        SceneHarness.Click(window, page.GetLogicalDescendants().OfType<Button>().Single(button => button.Tag is "cards"));

        Assert.Equal(timeline.Labels["cards"], timeline.CurrentTime, 3);
        Assert.Equal(1, page.GetControl<TextBlock>("Subtitle").Opacity, 3);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void SeekingAFinishedSequenceBackToOpenResetsTheBarToItsStart()
    {
        var page = new TitleSequencePage();
        var window = SceneHarness.Show(page, 1000, 900, frames: 10);
        var timeline = Timeline(page);
        timeline.Seek(timeline.Duration);
        TestFrames.Tick();
        var bar = page.GetControl<TransportBar>("Bar");
        Assert.Equal(1, bar.GetControl<Slider>("Scrubber").Value, 3);

        SceneHarness.Click(window, page.GetLogicalDescendants().OfType<Button>().Single(button => button.Tag is "open"));

        Assert.Equal(0, bar.GetControl<Slider>("Scrubber").Value);
        Assert.Equal("0 ms · 0%", bar.GetControl<TextBlock>("TimeText").Text);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheSequenceLandsOnItsFinalFrameUntilTheBarPlaysIt()
    {
        using var reduced = ReducedMotion.On();
        var page = new TitleSequencePage();
        var window = SceneHarness.Show(page, 1000, 900, frames: 2);
        var cards = page.GetControl<StackPanel>("Cards").Children;
        var dot = page.GetControl<Canvas>("DotField").Children[0];
        var pieces = page.GetControl<TextBlock>("Headline").GetVisualDescendants().OfType<Path>().ToList();
        var x = Presto.Get(dot, "X");

        TestFrames.Tick(60);

        Assert.True(Timeline(page).Completed);
        Assert.All(cards, card => Assert.Equal(1, card.Opacity, 3));
        Assert.All(pieces, piece =>
        {
            Assert.Equal(1, piece.Opacity, 3);
            Assert.Equal(0, Read(piece, "RotateX"), 3);
        });
        Assert.Equal(x, Presto.Get(dot, "X"));

        Timeline(page).Restart();
        TestFrames.Tick();
        Assert.False(Timeline(page).Completed);
        Assert.All(cards, card => Assert.Equal(0, card.Opacity));
        Assert.All(pieces, piece => Assert.Equal(0, piece.Opacity));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ReversedFromItsEndTheCardsAreHiddenBeforeTheirLabel()
    {
        var page = new TitleSequencePage();
        var window = SceneHarness.Show(page, 1000, 900);
        var cards = page.GetControl<StackPanel>("Cards").Children;
        for (var frame = 0; frame < 600 && !Timeline(page).Completed; frame++) TestFrames.Tick();
        var timeline = Timeline(page);
        Assert.True(timeline.Completed);

        timeline.Reverse();
        while (timeline.IterationCurrentTime >= timeline.Labels["cards"]) TestFrames.Tick();

        Assert.All(cards, card => Assert.Equal(0, card.Opacity));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AtTheDrawLabelTheCometHasLandedOnTheMonogramsStartAndItsTrailIsErased()
    {
        var page = new TitleSequencePage();
        var window = SceneHarness.Show(page, 1000, 900, frames: 10);
        Timeline(page).Pause();

        SceneHarness.Click(window, page.GetLogicalDescendants().OfType<Button>().Single(button => button.Tag is "draw"));

        var camera = page.GetControl<Panel>("Camera");
        var comet = page.GetControl<Ellipse>("Comet");
        var landed = comet.TranslatePoint(new Point(comet.Bounds.Width / 2, comet.Bounds.Height / 2), camera)!.Value;
        var start = page.GetControl<Path>("Monogram").TranslatePoint(new Point(44, 110), camera)!.Value;
        Assert.True(Point.Distance(landed, start) < 1.5, $"the comet landed at {landed}, the monogram starts at {start}");
        Assert.Equal([1, 1], (double[])PrestoAvalonia.Get(page.GetControl<Path>("Trail"), "Draw")!);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheDotRippleSettlesBackToFullSizeWhileTheDriftKeepsMoving()
    {
        var page = new TitleSequencePage();
        var window = SceneHarness.Show(page, 1000, 900);
        var timeline = Timeline(page);
        var dots = page.GetControl<Canvas>("DotField").Children;
        while (timeline.CurrentTime < timeline.Labels["mark"] + 1000) TestFrames.Tick();

        Assert.All(dots, dot => Assert.Equal(1, Read(dot, "Scale"), 3));
        var x = Read(dots[0], "X");
        TestFrames.Tick(5);
        Assert.NotEqual(x, Read(dots[0], "X"), 3);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ScrubbingBackAfterTheSequenceEndsTheDotRippleOnlyGrowsTheDots()
    {
        var page = new TitleSequencePage();
        var window = SceneHarness.Show(page, 1000, 900);
        var timeline = Timeline(page);
        while (!timeline.Completed) TestFrames.Tick();
        TestFrames.Tick(5);
        var dots = page.GetControl<Canvas>("DotField").Children;

        for (var time = timeline.Labels["mark"]; time < timeline.Labels["mark"] + 1600; time += 40)
        {
            timeline.Seek(time);
            for (var i = 0; i < dots.Count; i++)
                Assert.True(Read(dots[i], "Scale") > .999, $"dot {i} at {time:0} ms had Scale {Read(dots[i], "Scale")}");
        }
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void SeekingBackThroughTheSameTimesWritesTheSameStateAsSeekingForward()
    {
        var page = new TitleSequencePage();
        var window = SceneHarness.Show(page, 1000, 900, frames: 10);
        var timeline = Timeline(page);
        timeline.Pause();
        var headline = page.GetControl<TextBlock>("Headline");
        var pieces = headline.GetVisualDescendants().OfType<Path>().ToList();
        var shine = page.GetControl<TextBlock>("HeadlineShine");
        var cards = page.GetControl<StackPanel>("Cards").Children;
        var dots = page.GetControl<Canvas>("DotField").Children;
        var times = timeline.Labels.Values
            .SelectMany(label => new[] { label, label + 150, label + 400 })
            .Append(timeline.IterationDuration)
            .Where(time => time <= timeline.IterationDuration)
            .Distinct()
            .Order()
            .ToList();

        var forward = times.Select(Sample).ToList();
        var backward = Enumerable.Reverse(times).Select(Sample).Reverse().ToList();

        for (var t = 0; t < times.Count; t++)
            for (var v = 0; v < forward[t].Count; v++)
                Assert.True(
                    Math.Abs(forward[t][v] - backward[t][v]) < 1e-6,
                    $"value {v} at {times[t]:0} ms was {forward[t][v]} forward and {backward[t][v]} backward");
        SceneHarness.Close(window);

        List<double> Sample(double time)
        {
            timeline.Seek(time);
            var values = new List<double> { Read(headline, "Warp") };
            foreach (var piece in pieces) values.AddRange([piece.Opacity, Read(piece, "RotateX"), Read(piece, "Blur")]);
            var clip = ((RectangleGeometry?)shine.Clip)?.Rect ?? default;
            values.AddRange([clip.X, clip.Width]);
            foreach (var card in cards) values.AddRange([card.Opacity, Read(card, "RotateY"), Read(card, "Y")]);
            foreach (var dot in dots) values.AddRange([dot.Opacity, Read(dot, "Scale"), Read(dot, "X"), Read(dot, "Y")]);
            return values;
        }
    }

    private static double Read(Visual visual, string property) => Convert.ToDouble(Presto.Get(visual, property, withUnit: false));

    private static Timeline Timeline(TitleSequencePage page) =>
        (Timeline)page.GetControl<TransportBar>("Bar").Timer!;
}
