using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Events;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Covers <see cref="ScrollObserverParams.Snap"/>, which upstream has no counterpart of.</summary>
/// <remarks>
/// The stage is <see cref="ScrollObserverTests"/>'s: a 200 by 200 scroller holding a 100 unit target 300 units in, so the
/// default thresholds run from an offset of 100 to 400. Each snap waits 150 ms for the scroll to settle, then takes 400 ms.
/// </remarks>
public sealed class ScrollSnapTests : BridgeTestBase
{
    private const double SettleAndSnap = 600;

    private ScrollObserver Observe(ScrollObserverParams parameters)
    {
        var observer = PrestoAvalonia.OnScroll(parameters);
        Frames.Advance(16);
        return observer;
    }

    [AvaloniaFact]
    public void AnIncrementSnapEasesToTheNearestStepOnceSettledLandsExactlyAndLetsTheEngineIdle()
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        Observe(new ScrollObserverParams { Target = target, Snap = .25 });

        ScrollStage.ScrollTo(scroller, 160);
        Frames.Advance(100, 16);
        Assert.Equal(160, scroller.Offset.Y);

        Frames.Advance(250, 16);
        Assert.InRange(scroller.Offset.Y, 160.5, 174.5);

        Frames.Advance(SettleAndSnap, 16);
        Assert.Equal(175, scroller.Offset.Y);

        Frames.Advance(1000, 16);
        Assert.False(Presto.Engine.IsRunning);
        Assert.Equal(175, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void TheSnapWaitsWhileAPointerIsPressedOnTheScroller()
    {
        var (window, scroller, target, _) = ScrollStage.Show();
        Observe(new ScrollObserverParams { Target = target, Snap = .25 });

        window.MouseDown(new Point(100, 100), MouseButton.Left);
        ScrollStage.ScrollTo(scroller, 130);
        Frames.Advance(SettleAndSnap, 16);
        Assert.Equal(130, scroller.Offset.Y);

        window.MouseUp(new Point(100, 100), MouseButton.Left);
        Frames.Advance(SettleAndSnap, 16);
        Assert.Equal(100, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void AFunctionSnapLandsOnWhatTheFunctionReturns()
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        Observe(new ScrollObserverParams { Target = target, Snap = Utils.Snap([0, .3, 1]) });

        ScrollStage.ScrollTo(scroller, 230);
        Frames.Advance(SettleAndSnap, 16);

        Assert.Equal(190, scroller.Offset.Y);
    }

    [AvaloniaTheory]
    [InlineData(160, 190, 300)]
    [InlineData(220, 190, 300)]
    [InlineData(280, 310, 700)]
    [InlineData(350, 310, 700)]
    public void LabelSnappingLandsOnTheLinkedTimelinesLabelsFromEitherSide(double from, double offset, double time)
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        var timeline = Presto.CreateTimeline(new TimelineParams { Autoplay = false })
            .Add(target, new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = 1000, Ease = Eases.None })
            .Label("first", 300)
            .Label("second", 700);
        Observe(new ScrollObserverParams { Target = target, Sync = true, Snap = ScrollSnap.Labels }).Link(timeline);

        ScrollStage.ScrollTo(scroller, from);
        Frames.Advance(SettleAndSnap, 16);

        Assert.Equal(offset, scroller.Offset.Y);
        Assert.Equal(time, timeline.CurrentTime, 6);
    }

    [AvaloniaFact]
    public void AUserScrollMidSnapStopsItAndANewSnapStartsFromThereOnceSettled()
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        Observe(new ScrollObserverParams { Target = target, Snap = .25 });
        ScrollStage.ScrollTo(scroller, 130);
        Frames.Advance(350, 16);
        Assert.InRange(scroller.Offset.Y, 100.5, 129.5);

        ScrollStage.ScrollTo(scroller, 240);
        Frames.Advance(100, 16);
        Assert.Equal(240, scroller.Offset.Y);

        Frames.Advance(SettleAndSnap, 16);
        Assert.Equal(250, scroller.Offset.Y);
    }

    [AvaloniaTheory]
    [InlineData(50)]
    [InlineData(450)]
    public void NothingSnapsOutsideTheThresholds(double offset)
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        Observe(new ScrollObserverParams { Target = target, Snap = .25 });

        ScrollStage.ScrollTo(scroller, offset);
        Frames.Advance(SettleAndSnap, 16);

        Assert.Equal(offset, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void RevertMidSnapStopsIt()
    {
        var (_, scroller, target, _) = ScrollStage.Show();
        var observer = Observe(new ScrollObserverParams { Target = target, Snap = .25 });
        ScrollStage.ScrollTo(scroller, 130);
        Frames.Advance(350, 16);

        observer.Revert();
        var offset = scroller.Offset.Y;
        Frames.Advance(SettleAndSnap, 16);

        Assert.Equal(offset, scroller.Offset.Y);
        Assert.NotEqual(100, offset);
    }
}
