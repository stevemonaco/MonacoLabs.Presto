using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Events;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

/// <summary>
/// SAMPLES-PLAN §4.7 and GAPS-PLAN §5.3: the pinned scrub is frame-accurate in both directions, holds its stage still and
/// snaps to its labels, every named threshold reports both crossings each way round, the hero layers move at their speeds,
/// and a resize mid-scroll re-measures without moving the scrub off the scroll position.
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
        ReachThePin(scroller, pin);

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

        var stage = page.GetControl<Border>("PinStage");
        ReachThePin(scroller, pin);
        ScrollTo(scroller, pin.OffsetStart + (pin.Distance / 2));
        var distance = pin.Distance;
        var start = pin.OffsetStart;
        var before = scrub.Progress;
        var drawnBefore = SceneHarness.Number(number);
        Assert.Equal(.5, before, 2);

        window.Height -= Shrink;
        Dispatcher.UIThread.RunJobs();
        // Fewer frames than the container's own 250 ms resize debounce, so only the page's Refresh has re-measured.
        TestFrames.Tick(2);

        Assert.Equal(distance, pin.Distance, 1);
        Assert.Equal(start + (Shrink / 2), pin.OffsetStart, 1);
        Assert.Equal((scroller.Viewport.Height - stage.Bounds.Height) / 2, Position(stage, scroller), 1d);
        Assert.Equal(pin.Progress, scrub.Progress, 4);
        var drawnAfter = SceneHarness.Number(number);
        Assert.True(
            Math.Abs(drawnAfter - drawnBefore) < 8,
            $"the scrub jumped from {drawnBefore}% to {drawnAfter}% across a {Shrink}px resize");

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ThePinnedStageStaysPutWhileItsRangeScrollsAndWhatFollowsArrivesAfterIt()
    {
        var (window, page, scroller) = Show();
        var pin = Observer(page, "pin");
        var stage = page.GetControl<Border>("PinStage");
        var article = page.GetControl<StackPanel>("Article");
        var next = article.Children[article.Children.IndexOf(stage) + 1];

        ReachThePin(scroller, pin);
        ScrollTo(scroller, pin.OffsetStart);
        var pinned = Position(stage, scroller);
        Assert.Equal((scroller.Viewport.Height - stage.Bounds.Height) / 2, pinned, 1d);

        foreach (var fraction in (double[])[.13, .37, .5, .81, 1])
        {
            ScrollTo(scroller, pin.OffsetStart + (fraction * pin.Distance));
            Assert.Equal(pinned, Position(stage, scroller), 1d);
        }

        Assert.True(
            Position(next, scroller) >= pinned + stage.Bounds.Height,
            "the content after the pinned stage scrolled in underneath it");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ScrollingThatStopsBetweenTwoLabelsSettlesOnTheNearerOne()
    {
        var (window, page, scroller) = Show();
        var pin = Observer(page, "pin");
        var scrub = (Timeline)pin.Linked!;

        ReachThePin(scroller, pin);
        ScrollTo(scroller, pin.OffsetStart + (.42 * pin.Distance));
        TestFrames.Tick(60);

        Assert.Equal(scrub.Labels["step2"], scrub.CurrentTime, 1);
        Assert.Equal(pin.OffsetStart + (pin.Distance / 3), scroller.Offset.Y, 1);
        Assert.Equal("step2", page.GetControl<TextBlock>("PinLabel").Text);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheHeroLayersAreTranslatedByTheirSpeeds()
    {
        var (window, page, scroller) = Show();

        ScrollTo(scroller, 200);

        foreach (var (name, speed) in ((string, double)[])[("HeroSky", .6), ("HeroRidge", .8), ("HeroTitle", 1.3)])
        {
            Assert.Equal((1 - speed) * 200, Convert.ToDouble(PrestoAvalonia.Get(page.GetControl<Control>(name), "Y")), 3);
        }

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheHeroLayersScrollTogether()
    {
        using var _ = ReducedMotion.On();
        var (window, page, scroller) = Show();

        ScrollTo(scroller, 200);

        foreach (var name in (string[])["HeroSky", "HeroRidge", "HeroTitle"])
        {
            Assert.Equal(0, Convert.ToDouble(PrestoAvalonia.Get(page.GetControl<Control>(name), "Y")), 3);
        }

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionAChapterFadesInWithoutRising()
    {
        using var _ = ReducedMotion.On();
        var (window, page, scroller) = Show();
        var chapter = page.GetControl<Control>("Chapter1");

        for (var offset = SweepStep; offset < scroller.Extent.Height && !page.Crossings.Contains("chapter 1 enter forward"); offset += SweepStep)
        {
            ScrollTo(scroller, offset);
        }
        for (var frame = 0; frame < 60; frame++)
        {
            Assert.Equal(0, Convert.ToDouble(PrestoAvalonia.Get(chapter, "Y")), 3);
            TestFrames.Tick();
        }

        Assert.Equal(1, chapter.Opacity, 3);
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

    [AvaloniaFact]
    public void ScrollingHalfwayFillsHalfTheRail()
    {
        var (window, page, scroller) = Show();

        ScrollTo(scroller, (scroller.Extent.Height - scroller.Viewport.Height) / 2);

        Assert.Equal(.5, Convert.ToDouble(PrestoAvalonia.Get(page.GetControl<Border>("RailFill"), "ScaleX")), 2);
        Assert.StartsWith("50%", page.GetControl<TextBlock>("RailText").Text);
        var half = scroller.Offset.Y;
        for (var step = 1; step <= 4; step++) ScrollTo(scroller, half - (step * 40));
        Assert.EndsWith("backward", page.GetControl<TextBlock>("RailText").Text);
        TestFrames.Tick(60);
        var rotate = Convert.ToString(PrestoAvalonia.Get(page.GetControl<Control>("Vane"), "Rotate"), CultureInfo.InvariantCulture)!;
        Assert.Equal(180, double.Parse(rotate.Replace("deg", ""), CultureInfo.InvariantCulture), 1);
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

    private static void ReachThePin(ScrollViewer scroller, ScrollObserver pin)
    {
        ScrollTo(scroller, pin.OffsetStart);
        // Lets the heading reveals rejoin and the resize debounce re-measure.
        TestFrames.Tick(60);
    }

    private static double Position(Control control, ScrollViewer scroller) => control.TranslatePoint(default, scroller)!.Value.Y;

    private static void ScrollTo(ScrollViewer scroller, double offset)
    {
        scroller.Offset = new Vector(0, offset);
        Dispatcher.UIThread.RunJobs();
        // Three frames: the observers are ticked on the first, and the direction is sampled at 30 fps.
        TestFrames.Tick(3);
    }
}
