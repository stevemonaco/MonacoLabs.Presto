using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Events;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Covers <see cref="ScrollObserverParams.Pin"/> and <see cref="ScrollObserverParams.PinSpacing"/>, which upstream has no counterpart of.</summary>
/// <remarks>
/// The stage is <see cref="ScrollObserverTests"/>'s: a 200 by 200 scroller holding a 100 unit target 300 units in, then 300
/// more. The target is pinned from the moment its top reaches the scroller's top for 200 units, from 300 to 500.
/// </remarks>
public sealed class ScrollPinTests : BridgeTestBase
{
    private const double TargetSize = ScrollStage.TargetSize;
    private const double Lead = ScrollStage.Lead;
    private const double Distance = 200;
    private const double ContentSize = Lead + TargetSize + Lead;

    private ScrollObserver Pin(Border target, bool? spacing = null, ScrollAxis axis = ScrollAxis.Vertical)
    {
        var observer = PrestoAvalonia.OnScroll(new ScrollObserverParams
        {
            Target = target,
            Axis = axis,
            Enter = "start start",
            Leave = "start end+=100",
            Pin = true,
            PinSpacing = spacing,
        });
        Frames.Advance(16);
        Dispatcher.UIThread.RunJobs();
        return observer;
    }

    private static double Position(Control control, ScrollViewer scroller, Orientation orientation = Orientation.Vertical)
    {
        var point = control.TranslatePoint(default, scroller)!.Value;
        return orientation == Orientation.Horizontal ? point.X : point.Y;
    }

    [AvaloniaFact]
    public void APinnedTargetStaysPutBetweenItsThresholdsWithoutAFrameOfLag()
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        var observer = Pin(target);

        Assert.Equal(Lead, observer.OffsetStart);
        Assert.Equal(Distance, observer.Distance);

        foreach (var offset in new[] { 300d, 340.4, 375, 420.6, 499, 500 })
        {
            ScrollStage.ScrollTo(scroller, offset);
            Assert.Equal(0, Position(target, scroller), 1d);
        }
    }

    [AvaloniaFact]
    public void BeforeTheRangeThePinnedTargetScrollsNormally()
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        Pin(target);

        ScrollStage.ScrollTo(scroller, 120);

        Assert.Equal(Lead - 120, Position(target, scroller), 1d);
    }

    [AvaloniaFact]
    public void AfterTheRangeThePinnedTargetRidesAtThePinnedDistance()
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        Pin(target);

        ScrollStage.ScrollTo(scroller, 620);

        Assert.Equal(Lead + Distance - 620, Position(target, scroller), 1d);
    }

    [AvaloniaFact]
    public void PinSpacingPushesTheFollowingContentBackByTheDistance()
    {
        var (_, scroller, target, trail) = ScrollStage.Show();
        Pin(target);

        Assert.Equal(Lead + TargetSize + Distance, Position(trail, scroller), 1d);
        Assert.Equal(ContentSize + Distance, scroller.Extent.Height);
    }

    [AvaloniaFact]
    public void WithoutPinSpacingTheFollowingContentStays()
    {
        var (_, scroller, target, trail) = ScrollStage.Show();
        Pin(target, spacing: false);

        Assert.Equal(Lead + TargetSize, Position(trail, scroller), 1d);
        Assert.Equal(ContentSize, scroller.Extent.Height);

        ScrollStage.ScrollTo(scroller, 400);
        Assert.Equal(0, Position(target, scroller), 1d);
    }

    [AvaloniaFact]
    public void TheHorizontalAxisPinsAlongX()
    {
        var (_, scroller, target, trail) = ScrollStage.Show(Orientation.Horizontal);
        Pin(target, axis: ScrollAxis.Horizontal);

        Assert.Equal(ContentSize + Distance, scroller.Extent.Width);
        Assert.Equal(Lead + TargetSize + Distance, Position(trail, scroller, Orientation.Horizontal), 1d);

        ScrollStage.ScrollTo(scroller, 410, Orientation.Horizontal);

        Assert.Equal(0, Position(target, scroller, Orientation.Horizontal), 1d);
        Assert.Equal(0, target.TranslatePoint(default, scroller)!.Value.Y, 1d);
    }

    [AvaloniaFact]
    public void ARefreshAfterAResizeKeepsThePinWithoutAccumulatingTheSpacer()
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        var observer = Pin(target);
        ScrollStage.ScrollTo(scroller, 400);

        target.Height = TargetSize + 50;
        Dispatcher.UIThread.RunJobs();
        observer.Refresh();
        Dispatcher.UIThread.RunJobs();
        observer.Refresh();
        // Past the resize debounce, which refreshes once more for the extent the spacer grew.
        Frames.Advance(300, 16);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Distance + 50, observer.Distance);
        Assert.Equal(Distance + 50, target.Margin.Bottom);
        Assert.Equal(ContentSize + 50 + Distance + 50, scroller.Extent.Height);
        Assert.Equal(0, Position(target, scroller), 1d);
    }

    [AvaloniaFact]
    public void RevertRestoresTheUsersTransformAndMargin()
    {
        var translate = new TranslateTransform(0, 7);
        var target = new Border { Margin = new Thickness(5), RenderTransform = translate };
        var (_, scroller, _, _) = ScrollStage.Show(target: target);
        var observer = Pin(target);
        ScrollStage.ScrollTo(scroller, 400);

        Assert.NotSame(translate, target.RenderTransform);
        Assert.NotEqual(new Thickness(5), target.Margin);

        observer.Revert();

        Assert.Same(translate, target.RenderTransform);
        Assert.Equal(7, translate.Y);
        Assert.Equal(new Thickness(5), target.Margin);
    }

    [AvaloniaFact]
    public void RevertingTheLinkedAnimationReleasesThePin()
    {
        var child = new Border();
        var target = new Border { Child = child };
        var (_, scroller, _, _) = ScrollStage.Show(target: target);
        var animation = child.Animate(new() { ["Opacity"] = 0, Autoplay = false });
        Pin(target).Link(animation);
        ScrollStage.ScrollTo(scroller, 400);

        animation.Revert();

        Assert.Null(target.RenderTransform);
        Assert.Equal(default, target.Margin);
    }

    [AvaloniaFact]
    public void ALinkedAnimationOfAChildOfThePinnedTargetStillScrubs()
    {
        var child = new Border();
        var target = new Border { Child = child };
        var (_, scroller, _, _) = ScrollStage.Show(target: target);
        var animation = child.Animate(new()
        {
            ["Opacity"] = TweenValue.FromTo(0, 1),
            Duration = 1000,
            Ease = Eases.None,
            Autoplay = false,
        });
        var observer = PrestoAvalonia.OnScroll(new ScrollObserverParams
        {
            Target = target,
            Enter = "start start",
            Leave = "start end+=100",
            Pin = true,
            Sync = true,
        }).Link(animation);
        Frames.Advance(16);
        Dispatcher.UIThread.RunJobs();

        ScrollStage.ScrollTo(scroller, 400);
        Frames.Advance(40, 20);

        Assert.Equal(.5, observer.Progress);
        Assert.Equal(.5, child.Opacity, 3);
        Assert.Equal(0, Position(target, scroller), 1d);
    }
}
