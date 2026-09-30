using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Events;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

/// <summary>
/// SAMPLES-PLAN §4.7: the pinned scrub is frame-accurate in both directions, every named threshold reports both
/// crossings each way round, and a resize mid-scroll re-measures without moving the scrub off the scroll position.
/// </summary>
/// <remarks>
/// The scroller is driven the way <c>Monaco.Presto.AvaloniaBridge.Tests.ScrollObserverTests</c> drives it — the offset
/// is set and the frame source stepped — so a sampled offset lands exactly where the assertion expects it. The window
/// is 640 tall so that the first chapter starts a viewport below the top and can therefore be left on the way back up.
/// </remarks>
public sealed class ScrollStoryPageTests
{
    private const double SweepStep = 120;

    [AvaloniaFact]
    public void ThePinnedScrubIsSeekedToTheScrollPositionInBothDirections()
    {
        var (window, page, scroller) = Show();
        var pin = Observer(page, "pin");
        var scrub = pin.Linked!;
        var number = page.GetControl<TextBlock>("PinNumber");

        Assert.True(pin.Distance > 0, "the pinned track is shorter than the viewport, so there is nothing to scrub");

        foreach (var fraction in (double[])[0, .25, .5, .75, 1, .75, .5, .25, 0])
        {
            ScrollTo(scroller, pin.OffsetStart + (fraction * pin.Distance));

            Assert.Equal(fraction, pin.Progress, 2);
            Assert.Equal(pin.Progress, scrub.Progress, 4);
            var drawn = SceneHarness.Number(number);
            Assert.True(
                Math.Abs(drawn - (fraction * 100)) <= 1,
                $"the pinned timeline was drawn at {drawn}% with the scroll {fraction * 100}% through the track");
        }

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void EveryNamedThresholdReportsBothCrossingsInBothDirections()
    {
        var (window, page, scroller) = Show();
        var bottom = scroller.Extent.Height - scroller.Viewport.Height;

        for (var offset = SweepStep; offset < bottom; offset += SweepStep) ScrollTo(scroller, offset);
        ScrollTo(scroller, bottom);
        var down = page.Crossings.ToArray();

        // One step back before the sweep, so the 30 fps direction sampling has the backward flag set.
        ScrollTo(scroller, bottom - 20);
        for (var offset = bottom - SweepStep; offset > 0; offset -= SweepStep) ScrollTo(scroller, offset);
        ScrollTo(scroller, 0);
        var up = page.Crossings.Skip(down.Length).ToArray();

        foreach (var id in (string[])["chapter 1", "chapter 2", "chapter 3", "pin"])
        {
            Assert.Contains($"{id} enter forward", down);
            Assert.Contains($"{id} leave forward", down);
            Assert.Contains($"{id} enter backward", up);
            Assert.Contains($"{id} leave backward", up);
        }

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AResizeMidScrollReMeasuresThePinWithoutMovingTheScrubOffTheScroll()
    {
        const double Shrink = 120;
        var (window, page, scroller) = Show();
        var pin = Observer(page, "pin");
        var scrub = pin.Linked!;
        var number = page.GetControl<TextBlock>("PinNumber");

        ScrollTo(scroller, pin.OffsetStart + (pin.Distance / 2));
        var distance = pin.Distance;
        var before = scrub.Progress;
        var drawnBefore = SceneHarness.Number(number);
        Assert.Equal(.5, before, 2);

        window.Height -= Shrink;
        Dispatcher.UIThread.RunJobs();
        // Fewer frames than the container's own 250 ms resize debounce, so only the page's Refresh has re-measured.
        TestFrames.Tick(2);

        Assert.Equal(distance + Shrink, pin.Distance, 1);
        Assert.Equal(pin.Progress, scrub.Progress, 4);
        var drawnAfter = SceneHarness.Number(number);
        Assert.True(
            Math.Abs(drawnAfter - drawnBefore) < 8,
            $"the scrub jumped from {drawnBefore}% to {drawnAfter}% across a {Shrink}px resize");

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AChapterHeadingMasksInByLineAsItsChapterEntersAndIsRejoinedAfter()
    {
        var (window, page, scroller) = Show();
        var heading = page.GetControl<TextBlock>("Heading1");
        var text = heading.Text;

        for (var offset = SweepStep; offset < scroller.Extent.Height && !page.Crossings.Contains("chapter 1 enter forward"); offset += SweepStep)
        {
            ScrollTo(scroller, offset);
        }

        Assert.Contains("chapter 1 enter forward", page.Crossings);
        Assert.Contains(heading.GetVisualDescendants().OfType<Border>(), mask => mask.ClipToBounds && mask.Child is TextBlock);

        TestFrames.Tick(60);
        Assert.Equal(text, heading.Text);
        Assert.Null(heading.Inlines);
        SceneHarness.Close(window);
    }

    private static (Window Window, ScrollStoryPage Page, ScrollViewer Scroller) Show()
    {
        var page = new ScrollStoryPage();
        var window = SceneHarness.Show(page, 1100, 640, frames: 3);
        return (window, page, page.GetControl<ScrollViewer>("Scroller"));
    }

    private static ScrollObserver Observer(ScrollStoryPage page, string id) =>
        page.Observers.First(observer => observer.Id == id);

    private static void ScrollTo(ScrollViewer scroller, double offset)
    {
        scroller.Offset = new Vector(0, offset);
        Dispatcher.UIThread.RunJobs();
        // Three frames: the observers are ticked on the first, and the direction is sampled at 30 fps.
        TestFrames.Tick(3);
    }
}
