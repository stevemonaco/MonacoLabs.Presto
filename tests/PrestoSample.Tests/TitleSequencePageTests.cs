using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoSample.Tests;

public sealed class TitleSequencePageTests
{
    [AvaloniaFact]
    public void CardsFadeUpOnceWithoutEverDimmingAndStartHiddenAgainAfterARestart()
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
        Assert.All(cards, card => Assert.Equal(1, card.Opacity, 3));

        timeline.Restart();
        TestFrames.Tick();
        Assert.All(cards, card => Assert.Equal(0, card.Opacity));

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheTitleIsWrittenInAsUnfilledOutlinesEndsAsPlainTextAndHidesWhenSeekedBackToTheStart()
    {
        var page = new TitleSequencePage();
        var window = SceneHarness.Show(page, 1000, 900);
        var headline = page.GetControl<TextBlock>("Headline");
        var outlines = headline.GetVisualDescendants().OfType<Path>().ToList();
        Assert.Equal("PRESTO".Length, outlines.Count);
        Assert.All(outlines, piece => Assert.Equal(Colors.Transparent, ((ISolidColorBrush)piece.Fill!).Color));

        var timeline = Timeline(page);
        for (var frame = 0; frame < 600 && !timeline.Completed; frame++) TestFrames.Tick();
        Assert.True(timeline.Completed, "the sequence never finished");
        Assert.Null(headline.Inlines);
        Assert.Equal("PRESTO", headline.Text);
        Assert.Equal(1, headline.Opacity, 3);
        Assert.All(page.GetControl<StackPanel>("Cards").Children, card => Assert.Equal(1, card.Opacity, 3));

        SceneHarness.Click(window, page.GetLogicalDescendants().OfType<Button>().Single(button => button.Tag is "draw"));
        Assert.Equal(0, headline.Opacity);
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
    public void SeekingAFinishedSequenceBackToDrawResetsTheBarToItsStart()
    {
        var page = new TitleSequencePage();
        var window = SceneHarness.Show(page, 1000, 900, frames: 10);
        var timeline = Timeline(page);
        timeline.Seek(timeline.Duration);
        TestFrames.Tick();
        var bar = page.GetControl<TransportBar>("Bar");
        Assert.Equal(1, bar.GetControl<Slider>("Scrubber").Value, 3);

        SceneHarness.Click(window, page.GetLogicalDescendants().OfType<Button>().Single(button => button.Tag is "draw"));

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
        var headline = page.GetControl<TextBlock>("Headline");
        var x = Presto.Get(dot, "X");

        TestFrames.Tick(60);

        Assert.True(Timeline(page).Completed);
        Assert.All(cards, card => Assert.Equal(1, card.Opacity, 3));
        Assert.Empty(headline.GetVisualDescendants().OfType<Path>());
        Assert.Equal(1, headline.Opacity, 3);
        Assert.Equal(x, Presto.Get(dot, "X"));

        Timeline(page).Restart();
        TestFrames.Tick();
        Assert.False(Timeline(page).Completed);
        Assert.All(cards, card => Assert.Equal(0, card.Opacity));
        Assert.Equal(0, headline.Opacity, 3);
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

    private static Timeline Timeline(TitleSequencePage page) =>
        (Timeline)page.GetControl<TransportBar>("Bar").Timer!;
}
