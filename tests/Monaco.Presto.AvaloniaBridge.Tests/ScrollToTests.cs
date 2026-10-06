using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Events;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Covers <see cref="PrestoAvalonia.ScrollTo(ScrollViewer, Vector, AnimationBuilder?)"/>, mostly on <see cref="ScrollStage"/>'s
/// 200 by 200 scroller, whose offset runs 0 to 800 over a 600 unit trail.
/// </summary>
public sealed class ScrollToTests : BridgeTestBase
{
    [AvaloniaFact]
    public void LandsExactlyOnBothAxesAndLetsTheEngineIdle()
    {
        var scroller = new ScrollViewer
        {
            Width = 200,
            Height = 200,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Width = 1000, Height = 1000 },
        };
        Show(scroller);

        var animation = PrestoAvalonia.ScrollTo(scroller, new Vector(333.25, 512.5));
        Frames.Advance(1000, 16);

        Assert.Equal(new Vector(333.25, 512.5), scroller.Offset);
        Assert.True(animation.Completed);
        Assert.False(Presto.Engine.IsRunning);
    }

    [AvaloniaTheory]
    [InlineData(.5, 275)]
    [InlineData(2, 500)]
    [InlineData(10, 650)]
    public void TheDefaultDurationGrowsWithTheViewportsTraveledAndCaps(double viewports, double duration)
    {
        var (_, scroller, _, _) = ScrollStage.Show(trail: 2000);

        var animation = PrestoAvalonia.ScrollTo(scroller, viewports * scroller.Viewport.Height);

        Assert.Equal(duration, animation.Duration);
    }

    [AvaloniaFact]
    public void AnExplicitDurationAndEaseWin()
    {
        var (_, scroller, _, _) = ScrollStage.Show(trail: 600);

        var animation = PrestoAvalonia.ScrollTo(scroller, 400, builder: Presto.Build().Duration(1000).Ease(Eases.None));
        Frames.Advance(496, 16);

        Assert.Equal(1000, animation.Duration);
        Assert.InRange(scroller.Offset.Y, 190, 210);
    }

    [AvaloniaFact]
    public void AWheelMidFlightCancels()
    {
        var (window, scroller, _, _) = ScrollStage.Show(trail: 600);
        PrestoAvalonia.ScrollTo(scroller, 700);
        Frames.Advance(160, 16);

        window.MouseWheel(new Point(100, 100), new Vector(0, -1));
        Dispatcher.UIThread.RunJobs();
        var left = scroller.Offset.Y;
        Frames.Advance(800, 16);

        Assert.NotEqual(700, left);
        Assert.Equal(left, scroller.Offset.Y);
        Assert.Empty(Presto.GetAnimations(scroller, "ScrollY"));
    }

    [AvaloniaFact]
    public void AnExtentShrinkMidFlightClampsAndARegrowthCarriesOnToTheOffset()
    {
        var (_, scroller, _, trail) = ScrollStage.Show(trail: 600);
        PrestoAvalonia.ScrollTo(scroller, 700, b => b.Duration(400).Ease(Eases.None));
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
    public void AnOffsetPastTheEndIsReachedOnceTheExtentGrowsToFitIt()
    {
        var (_, scroller, _, trail) = ScrollStage.Show(trail: 600);
        PrestoAvalonia.ScrollTo(scroller, 1200, b => b.Duration(400).Ease(Eases.None));
        Frames.Advance(160, 16);
        var before = scroller.Offset.Y;

        trail.Height = 1000;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(16);
        Assert.InRange(scroller.Offset.Y - before, 0, 100);
        Frames.Advance(400, 16);

        Assert.Equal(1200, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void ASecondScrollToReplacesTheFirst()
    {
        var (_, scroller, _, _) = ScrollStage.Show(trail: 600);
        PrestoAvalonia.ScrollTo(scroller, 700);
        Frames.Advance(160, 16);
        var left = scroller.Offset.Y;

        var second = PrestoAvalonia.ScrollTo(scroller, 100);
        Frames.Advance(16);
        Assert.Equal([second], Presto.GetAnimations(scroller, "ScrollY"));
        Assert.InRange(scroller.Offset.Y, left - 60, left);
        Frames.Advance(800, 16);

        Assert.Equal(100, scroller.Offset.Y);
        Assert.True(second.Completed);
    }

    [AvaloniaFact]
    public void ASnapWaitsForTheScrollToAndSnapsAfterItsDelayOnceItEnds()
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        PrestoAvalonia.OnScroll(new ScrollObserverParams { Target = target, Snap = .25 });
        Frames.Advance(16);

        PrestoAvalonia.ScrollTo(scroller, 160, b => b.Duration(600).Ease(Eases.None));
        Frames.Advance(400, 16);
        Assert.InRange(scroller.Offset.Y, 100, 115);
        Frames.Advance(220, 16);
        Assert.Equal(160, scroller.Offset.Y);

        Frames.Advance(100, 16);
        Assert.Equal(160, scroller.Offset.Y);
        Frames.Advance(600, 16);
        Assert.Equal(175, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void AScrollToStopsARunningSnapAndTheSnapRestartsAfterItsDelayOnceItEnds()
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        PrestoAvalonia.OnScroll(new ScrollObserverParams { Target = target, Snap = .25 });
        Frames.Advance(16);
        ScrollStage.ScrollTo(scroller, 160);
        Frames.Advance(200, 16);
        Assert.InRange(scroller.Offset.Y, 160.01, 174.99);

        var animation = PrestoAvalonia.ScrollTo(scroller, 300, b => b.Duration(600).Ease(Eases.None));
        var last = scroller.Offset.Y;
        for (var elapsed = 0; elapsed < 620; elapsed += 16)
        {
            Frames.Advance(16);
            Assert.True(scroller.Offset.Y >= last);
            last = scroller.Offset.Y;
        }
        Assert.True(animation.Completed);
        Assert.Equal(300, scroller.Offset.Y);

        Frames.Advance(100, 16);
        Assert.Equal(300, scroller.Offset.Y);
        Frames.Advance(600, 16);
        Assert.Equal(325, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void ASnapDelayElapsingBetweenTheWritesOfALowFrameRateScrollToLeavesItRunning()
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        PrestoAvalonia.OnScroll(new ScrollObserverParams { Target = target, Snap = .25 });
        Frames.Advance(16);
        ScrollStage.ScrollTo(scroller, 160);

        var animation = PrestoAvalonia.ScrollTo(scroller, 300, b => b.Duration(1200).Ease(Eases.None).FrameRate(4));
        Frames.Advance(1300, 16);

        Assert.True(animation.Completed);
        Assert.Equal(300, scroller.Offset.Y);
        Frames.Advance(600, 16);
        Assert.Equal(325, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void ACanceledScrollToLetsTheSnapFollowAfterItsDelay()
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        PrestoAvalonia.OnScroll(new ScrollObserverParams { Target = target, Snap = .25 });
        Frames.Advance(16);

        var animation = PrestoAvalonia.ScrollTo(scroller, 300, b => b.Duration(600).Ease(Eases.None));
        Frames.Advance(304, 16);
        animation.Cancel();
        var left = scroller.Offset.Y;
        Assert.InRange(left, 100, 250);

        Frames.Advance(100, 16);
        Assert.Equal(left, scroller.Offset.Y);
        Frames.Advance(600, 16);
        Assert.Equal(175, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void ReducedMotionJumpsOnTheFirstFrameAndTheEngineIdles()
    {
        var (_, scroller, _, _) = ScrollStage.Show(trail: 600);
        Presto.Defaults.ReducedMotion = true;

        PrestoAvalonia.ScrollTo(scroller, 500);
        Frames.Advance(16);

        Assert.Equal(500, scroller.Offset.Y);
        Frames.Advance(16);
        Assert.False(Presto.Engine.IsRunning);
    }

    [AvaloniaFact]
    public void ReducedMotionPlaysTheBuildersReducedForm()
    {
        var (_, scroller, _, _) = ScrollStage.Show(trail: 600);
        Presto.Defaults.ReducedMotion = true;

        var animation = PrestoAvalonia.ScrollTo(scroller, 500, b => b.WhenReduced(r => r.Duration(100)));
        Frames.Advance(200, 16);

        Assert.Equal(100, animation.Duration);
        Assert.Equal(500, scroller.Offset.Y);
    }
}
