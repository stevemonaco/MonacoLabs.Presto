using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Monaco.Presto;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;

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

        // The split re-runs once layout settles and KeepTime rebuilds the whole timeline, so it is read from the bar each frame.
        var timeline = Timeline(page);
        var previous = new double[cards.Count];
        for (var frame = 0; frame < 600 && !timeline.Completed; frame++)
        {
            TestFrames.Tick();
            timeline = Timeline(page);
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

    private static Timeline Timeline(TitleSequencePage page) =>
        (Timeline)page.GetControl<TransportBar>("Bar").Timer!;
}
