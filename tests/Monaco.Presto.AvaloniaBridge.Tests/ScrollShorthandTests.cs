using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Covers the <c>"ScrollX"</c> and <c>"ScrollY"</c> shorthands on a 200 by 200 scroller over 1000 units of content, so the
/// offset runs 0 to 800.
/// </summary>
public sealed class ScrollShorthandTests : BridgeTestBase
{
    private static readonly Point Inside = new(100, 100);

    [AvaloniaFact]
    public void AScrollYTweenLandsExactly()
    {
        var (_, scroller, _, _) = ScrollStage.Show(trail: 600);

        Presto.Animate(scroller, new AvaloniaAnimationParams { ["ScrollY"] = 432.5, Duration = 300 });
        Frames.Advance(100, 16);
        Assert.InRange(scroller.Offset.Y, 1, 432);
        Assert.False(scroller.IsAnimating(ScrollViewer.OffsetProperty));

        Frames.Advance(300, 16);
        Assert.Equal(432.5, scroller.Offset.Y);
        Assert.False(Presto.Engine.IsRunning);
    }

    [AvaloniaFact]
    public void AScrollXTweenLandsOnAHorizontalScroller()
    {
        var (_, scroller, _, _) = ScrollStage.Show(Orientation.Horizontal, trail: 600);

        Presto.Animate(scroller, new AvaloniaAnimationParams { ["ScrollX"] = 250, Duration = 300 });
        Frames.Advance(400, 16);

        Assert.Equal(new Vector(250, 0), scroller.Offset);
    }

    [AvaloniaFact]
    public void TheShorthandsResolveOnlyOnAScrollViewer()
    {
        Assert.Throws<ArgumentException>(() => Presto.Animate(new Border(), new AvaloniaAnimationParams { ["ScrollY"] = 100 }));
    }

    [AvaloniaTheory]
    [InlineData("wheel")]
    [InlineData("thumb")]
    public void UserInputMidFlightCancelsOnlyTheScrollTween(string input)
    {
        var (window, scroller, _, _) = ScrollStage.Show(trail: 600);
        var animation = Presto.Animate(scroller, new AvaloniaAnimationParams { ["ScrollY"] = 700, ["Opacity"] = .5, Duration = 400 });
        Frames.Advance(160, 16);

        if (input == "wheel") window.MouseWheel(Inside, new Vector(0, -1));
        else ScrollStage.DragThumb(window, scroller, 10);
        Dispatcher.UIThread.RunJobs();
        var left = scroller.Offset.Y;
        Frames.Advance(400, 16);

        Assert.Equal(left, scroller.Offset.Y);
        Assert.Equal(.5, scroller.Opacity);
        Assert.True(animation.Completed);
    }

    [AvaloniaFact]
    public void AnExtentShrinkMidFlightClampsEveryFrameWithoutCancelling()
    {
        var (_, scroller, _, trail) = ScrollStage.Show(trail: 600);
        Presto.Animate(scroller, new AvaloniaAnimationParams { ["ScrollY"] = 700, Duration = 400, Ease = Eases.None });
        Frames.Advance(240, 16);
        Assert.True(scroller.Offset.Y > 300);

        trail.Height = 100;
        Dispatcher.UIThread.RunJobs();
        for (var i = 0; i < 4; i++)
        {
            Frames.Advance(16);
            Assert.Equal(300, scroller.Offset.Y);
        }
        trail.Height = 600;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);

        Assert.Equal(700, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void ARevertLeavesTheOffsetWhereItIs()
    {
        var (_, scroller, _, _) = ScrollStage.Show(trail: 600);
        var animation = Presto.Animate(scroller, new AvaloniaAnimationParams { ["ScrollY"] = 700, Duration = 400 });
        Frames.Advance(160, 16);
        var left = scroller.Offset.Y;

        animation.Revert();
        Frames.Advance(400, 16);

        Assert.Equal(left, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void TheShorthandRunsInATimelineAndThroughTheBuilder()
    {
        var (_, scroller, _, _) = ScrollStage.Show(trail: 600);
        var visited = 0d;

        var timeline = Presto.CreateTimeline(new TimelineParams())
            .Add(scroller, Presto.Build().ScrollY(500).Duration(200).OnComplete(_ => visited = scroller.Offset.Y))
            .Add(scroller, new AvaloniaAnimationParams { ["ScrollY"] = 120, Duration = 200 });
        Frames.Advance(500, 16);

        Assert.Equal(500, visited);
        Assert.Equal(120, scroller.Offset.Y);
        Assert.True(timeline.Completed);
    }

    [AvaloniaFact]
    public void TheScrollerStillTakesTheWheelAfterwards()
    {
        var (window, scroller, _, _) = ScrollStage.Show(trail: 600);
        Presto.Animate(scroller, new AvaloniaAnimationParams { ["ScrollY"] = 400, Duration = 200 });
        Frames.Advance(300, 16);

        window.MouseWheel(Inside, new Vector(0, -1));

        Assert.Equal(450, scroller.Offset.Y);
        Assert.False(scroller.IsAnimating(ScrollViewer.OffsetProperty));
    }
}
