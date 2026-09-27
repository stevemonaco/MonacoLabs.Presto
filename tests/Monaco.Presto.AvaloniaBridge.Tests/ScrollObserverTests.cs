using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Events;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Covers <c>PrestoAvalonia.OnScroll</c>, porting tests/suites/scroll.test.js — where upstream scrolls the document,
/// these set a headless <see cref="ScrollViewer"/>'s offset and tick the frame source.
/// </summary>
/// <remarks>
/// The stage is a 200 by 200 scroller holding a 100 unit target 300 units in, so with the default thresholds the target
/// enters at an offset of 100, leaves at 400 and travels 300 in between.
/// </remarks>
public sealed class ScrollObserverTests : BridgeTestBase
{
    private const double ViewportSize = 200;
    private const double TargetSize = 100;
    private const double Lead = 300;
    private const double WakeWindow = 500;

    private static (Window Window, ScrollViewer Scroller, Border Target) Show(
        Orientation orientation = Orientation.Vertical, double lead = Lead, double trail = Lead)
    {
        var stage = Stage(orientation, lead, trail);
        stage.Window.Show();
        Dispatcher.UIThread.RunJobs();
        return stage;
    }

    private static (Window Window, ScrollViewer Scroller, Border Target) Stage(
        Orientation orientation, double leadSize = Lead, double trailSize = Lead)
    {
        var target = new Border { Background = Brushes.Red };
        var lead = new Border();
        var trail = new Border();
        foreach (var (control, size) in new[] { (target, TargetSize), (lead, leadSize), (trail, trailSize) })
        {
            if (orientation == Orientation.Horizontal) control.Width = size; else control.Height = size;
        }

        var scroller = new ScrollViewer
        {
            Width = ViewportSize,
            Height = ViewportSize,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel { Orientation = orientation, Children = { lead, target, trail } },
        };
        return (new Window { Content = scroller, Width = 300, Height = 300 }, scroller, target);
    }

    private ScrollObserver Observe(ScrollObserverParams parameters)
    {
        var observer = PrestoAvalonia.OnScroll(parameters);
        Frames.Advance(16);
        return observer;
    }

    private void ScrollTo(ScrollViewer scroller, double offset, Orientation orientation = Orientation.Vertical)
    {
        scroller.Offset = orientation == Orientation.Horizontal ? new Vector(offset, 0) : new Vector(0, offset);
        Dispatcher.UIThread.RunJobs();
        // Two frames: the observers are ticked on the first, and the direction is sampled at 30 fps.
        Frames.Advance(40, 20);
    }

    [AvaloniaFact]
    public void TheObserverKeepsTheSuppliedId()
    {
        var (_, _, target) = Show();
        var observer = Observe(new ScrollObserverParams { Target = target, Id = "my-scroll-observer" });

        Assert.Equal("my-scroll-observer", observer.Id);
    }

    [AvaloniaFact]
    public void TheHorizontalAxisWatchesTheHorizontalOffset()
    {
        var (_, scroller, target) = Show(Orientation.Horizontal);
        var observer = Observe(new ScrollObserverParams { Target = target, Axis = ScrollAxis.Horizontal });

        Assert.True(observer.Horizontal);
        Assert.Equal(100d, observer.OffsetStart);

        ScrollTo(scroller, 250, Orientation.Horizontal);

        Assert.Equal(.5, observer.Progress);
        Assert.True(observer.IsInView);
    }

    [AvaloniaFact]
    public void AKeywordThresholdIsMeasuredAgainstTheContainerSize()
    {
        var (_, _, target) = Show();
        var observer = Observe(new ScrollObserverParams { Target = target, Enter = "center start" });

        Assert.Equal(200d, observer.OffsetStart);
        Assert.Equal(400d, observer.OffsetEnd);
        Assert.Equal(200d, observer.Distance);
    }

    [AvaloniaFact]
    public void APercentageThresholdIsThatFractionOfTheContainerSize()
    {
        var (_, _, target) = Show();
        var observer = Observe(new ScrollObserverParams { Target = target, Enter = "75% top" });

        Assert.Equal(150d, observer.OffsetStart);
        Assert.Equal(250d, observer.Distance);
    }

    [AvaloniaFact]
    public void ABareNumberThresholdIsPixels()
    {
        var (_, _, target) = Show();
        var observer = Observe(new ScrollObserverParams { Target = target, Enter = 120d });

        Assert.Equal(180d, observer.OffsetStart);
        Assert.Equal(220d, observer.Distance);
    }

    [AvaloniaFact]
    public void ARelativeOperatorMovesTheThresholdOffTheKeyword()
    {
        var (_, _, target) = Show();
        var observer = Observe(new ScrollObserverParams { Target = target, Enter = "bottom-=120 top" });

        Assert.Equal(220d, observer.OffsetStart);
        Assert.Equal(180d, observer.Distance);
    }

    /// <summary>A target near the start of the content would enter before scroll 0, which <c>"max"</c> clamps away.</summary>
    [AvaloniaFact]
    public void MaxClampsTheEnterThresholdToWhatTheContainerCanScroll()
    {
        var (_, _, target) = Show(lead: 100);
        var observer = Observe(new ScrollObserverParams { Target = target, Enter = "max start" });

        Assert.Equal(0d, observer.OffsetStart);
        Assert.Equal(200d, observer.OffsetEnd);
    }

    /// <summary>A target near the end of the content would leave past the last scroll position, which <c>"min"</c> clamps away.</summary>
    [AvaloniaFact]
    public void MinClampsTheLeaveThresholdToWhatTheContainerCanScroll()
    {
        var (_, _, target) = Show(trail: 100);
        var observer = Observe(new ScrollObserverParams { Target = target, Leave = "min end" });

        Assert.Equal(100d, observer.OffsetStart);
        Assert.Equal(300d, observer.OffsetEnd);
    }

    [AvaloniaFact]
    public void SyncTrueIsASmoothSyncOfOne()
    {
        var (_, _, target) = Show();
        var observer = Observe(new ScrollObserverParams { Target = target, Sync = true });

        Assert.True(observer.Sync);
        Assert.Equal(1d, observer.SyncSmooth);
    }

    [AvaloniaFact]
    public void SyncANumberIsThatSmoothing()
    {
        var (_, _, target) = Show();
        var observer = Observe(new ScrollObserverParams { Target = target, Sync = 0.5 });

        Assert.True(observer.Sync);
        Assert.Equal(.5, observer.SyncSmooth);
    }

    [AvaloniaFact]
    public void SyncLinearIsASmoothSyncOfOne()
    {
        var (_, _, target) = Show();
        var observer = Observe(new ScrollObserverParams { Target = target, Sync = "linear" });

        Assert.True(observer.Sync);
        Assert.Equal(1d, observer.SyncSmooth);
    }

    [AvaloniaFact]
    public void SyncAnEaseNameIsAnEasedSync()
    {
        var (_, _, target) = Show();
        var observer = Observe(new ScrollObserverParams { Target = target, Sync = "inOutQuad" });

        Assert.True(observer.Sync);
        Assert.Null(observer.SyncSmooth);
        Assert.NotNull(observer.SyncEase);
    }

    /// <summary>Upstream "ScrollObserver callbacks are assigned", asserted by driving them.</summary>
    [AvaloniaFact]
    public void TheEnterAndLeaveCallbacksFireInBothDirections()
    {
        var (_, scroller, target) = Show();
        var log = new List<string>();
        var updates = 0;
        var observer = Observe(new ScrollObserverParams
        {
            Target = target,
            OnEnter = _ => log.Add("enter"),
            OnLeave = _ => log.Add("leave"),
            OnEnterForward = _ => log.Add("enterForward"),
            OnLeaveForward = _ => log.Add("leaveForward"),
            OnEnterBackward = _ => log.Add("enterBackward"),
            OnLeaveBackward = _ => log.Add("leaveBackward"),
            OnUpdate = _ => updates++,
        });

        Assert.Empty(log);

        ScrollTo(scroller, 250);
        Assert.Equal(["enter", "enterForward"], log);
        Assert.True(observer.IsInView);
        Assert.True(updates > 0);

        ScrollTo(scroller, 450);
        Assert.Equal(["enter", "enterForward", "leave", "leaveForward"], log);
        Assert.False(observer.IsInView);

        // One step back before the next threshold, so the 30 fps direction sampling has the backward flag set.
        ScrollTo(scroller, 430);
        ScrollTo(scroller, 250);
        Assert.Equal(["enter", "enterForward", "leave", "leaveForward", "enter", "enterBackward"], log);
        Assert.True(observer.Backward);

        ScrollTo(scroller, 50);
        Assert.Equal(
            ["enter", "enterForward", "leave", "leaveForward", "enter", "enterBackward", "leave", "leaveBackward"],
            log);
    }

    [AvaloniaFact]
    public void LinkPausesWhatItIsGivenAndKeepsIt()
    {
        var (_, _, target) = Show();
        var animation = target.Animate(new() { ["X"] = 100 });
        var observer = Observe(new ScrollObserverParams { Target = target }).Link(animation);

        Assert.Same(animation, observer.Linked);
        Assert.True(animation.Paused);
    }

    /// <summary>The default <c>"play pause"</c> sync, upstream's method-based mode.</summary>
    [AvaloniaFact]
    public void TheDefaultSyncPlaysTheLinkedObjectOnEnterAndPausesItOnLeave()
    {
        var (_, scroller, target) = Show();
        var animation = target.Animate(new() { ["X"] = 100, Duration = 1000, Autoplay = false });
        Observe(new ScrollObserverParams { Target = target }).Link(animation);

        Assert.True(animation.Paused);

        ScrollTo(scroller, 250);
        Assert.False(animation.Paused);

        ScrollTo(scroller, 450);
        Assert.True(animation.Paused);
    }

    /// <summary>Upstream's four-name sync, whose second pair replaces the first once the scroll turns around.</summary>
    [AvaloniaFact]
    public void AFourNameSyncRunsItsBackwardPairWhenTheScrollTurnsAround()
    {
        var (_, scroller, target) = Show();
        var animation = target.Animate(new() { ["X"] = 100, Duration = 1000, Autoplay = false });
        var observer = Observe(new ScrollObserverParams { Target = target, Sync = "play pause reverse play" }).Link(animation);

        ScrollTo(scroller, 250);
        Assert.False(animation.Paused);
        Assert.False(animation.Reversed);

        ScrollTo(scroller, 450);
        Assert.True(animation.Paused);

        // One step back before the next threshold, so the 30 fps direction sampling has the backward flag set.
        ScrollTo(scroller, 430);
        ScrollTo(scroller, 250);

        Assert.True(observer.Backward);
        Assert.True(animation.Reversed);
    }

    /// <summary>Upstream "ScrollObserver linked via autoplay", where the observer takes its target from the animation.</summary>
    [AvaloniaFact]
    public void AnObserverWithoutATargetTakesTheLinkedAnimationsControl()
    {
        var (_, scroller, target) = Show();
        var animation = target.Animate(new() { ["X"] = 100, Autoplay = false });
        var observer = PrestoAvalonia.OnScroll().Link(animation);
        Frames.Advance(16);

        Assert.Same(animation, observer.Linked);
        Assert.Same(target, observer.Target);
        Assert.Same(scroller, observer.Container!.Scroller);
    }

    /// <summary>Upstream "ScrollObserver linked timeline via autoplay".</summary>
    [AvaloniaFact]
    public void AnObserverWithoutATargetTakesTheLinkedTimelinesFirstControl()
    {
        var (_, _, target) = Show();
        var timeline = Presto.CreateTimeline(new TimelineParams { Autoplay = false })
            .Add(target, new AvaloniaAnimationParams { ["X"] = 100 })
            .Add(target, new AvaloniaAnimationParams { ["Y"] = 100 });
        var observer = PrestoAvalonia.OnScroll().Link(timeline);
        Frames.Advance(16);

        Assert.Same(timeline, observer.Linked);
        Assert.Same(target, observer.Target);
    }

    [AvaloniaFact]
    public void RepeatFalseRevertsTheObserverAfterCompletion()
    {
        var (_, scroller, target) = Show();
        var observer = Observe(new ScrollObserverParams { Target = target, Repeat = false });

        Assert.False(observer.Repeat);

        ScrollTo(scroller, 250);
        Assert.False(observer.Reverted);

        ScrollTo(scroller, 450);
        Assert.True(observer.Reverted);
        Assert.False(ScrollContainer.Containers.ContainsKey(scroller));
    }

    [AvaloniaFact]
    public void RefreshUpdatesTheBounds()
    {
        var (_, _, target) = Show();
        var observer = Observe(new ScrollObserverParams { Target = target });

        Assert.Equal(Lead, observer.Offset);
        Assert.Equal(100d, observer.OffsetStart);
        Assert.Equal(400d, observer.OffsetEnd);
        Assert.Equal(300d, observer.Distance);

        target.Height = TargetSize * 2;
        Dispatcher.UIThread.RunJobs();
        observer.Refresh();

        Assert.Equal(500d, observer.OffsetEnd);
        Assert.Equal(400d, observer.Distance);
    }

    [AvaloniaFact]
    public void RefreshBeforeTheObserverResolvesDoesNothing()
    {
        var (_, _, target) = Show();
        var observer = PrestoAvalonia.OnScroll(new ScrollObserverParams { Target = target });

        observer.Refresh();
        Assert.False(observer.Ready);

        Frames.Advance(16);
        Assert.True(observer.Ready);
    }

    [AvaloniaFact]
    public void RefreshScrollRemeasuresEveryObserverOfTheScrollerAtOnce()
    {
        var (_, scroller, target) = Show();
        var first = Observe(new ScrollObserverParams { Target = target });
        var second = Observe(new ScrollObserverParams { Target = target, Enter = "center start" });

        target.Height = TargetSize * 2;
        Dispatcher.UIThread.RunJobs();
        PrestoAvalonia.RefreshScroll(scroller);

        Assert.Equal(500d, first.OffsetEnd);
        Assert.Equal(500d, second.OffsetEnd);
    }

    /// <summary>Upstream "resizeTicker completing before target is resolved should not throw".</summary>
    [AvaloniaFact]
    public void CompletingTheResizeTickerBeforeTheObserverResolvesDoesNotThrow()
    {
        var (_, _, target) = Show();
        var observer = PrestoAvalonia.OnScroll(new ScrollObserverParams { Target = target });

        Assert.False(observer.Ready);
        observer.Container!.ResizeTicker.Complete();
        observer.Revert();
    }

    /// <summary>A target created during a layout pass has no ScrollViewer ancestor and no bounds until it has been through one.</summary>
    [AvaloniaFact]
    public void AnObserverOfAnUnlaidOutTargetResolvesOnceItLoads()
    {
        var (window, _, target) = Stage(Orientation.Vertical);
        var observer = PrestoAvalonia.OnScroll(new ScrollObserverParams { Target = target });
        Frames.Advance(16);

        Assert.False(observer.Ready);
        Assert.Null(observer.Container);

        window.Show();
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(16);

        Assert.True(observer.Ready);
        Assert.Equal(100d, observer.OffsetStart);
    }

    [AvaloniaFact]
    public void OnResizeFiresOnEveryContainerRefresh()
    {
        var (_, _, target) = Show();
        var resizes = 0;
        var observer = Observe(new ScrollObserverParams { Target = target, OnResize = _ => resizes++ });

        Assert.Equal(0, resizes);

        observer.Container!.RefreshObservers();
        Assert.Equal(1, resizes);

        observer.Container.RefreshObservers();
        Assert.Equal(2, resizes);
    }

    /// <summary>Upstream "Reverting an animation with onScroll should also revert the ScrollObserver".</summary>
    [AvaloniaFact]
    public void RevertingTheLinkedAnimationRevertsTheObserver()
    {
        var (_, scroller, target) = Show();
        var animation = target.Animate(new() { ["Rotate"] = 360, Autoplay = false });
        var observer = Observe(new ScrollObserverParams { Target = target }).Link(animation);

        Assert.True(ScrollContainer.Containers.ContainsKey(scroller));

        animation.Revert();

        Assert.True(observer.Reverted);
        Assert.False(ScrollContainer.Containers.ContainsKey(scroller));
    }

    /// <summary>The data timer pauses with the wake window, unlike upstream's, which is what lets the engine idle.</summary>
    [AvaloniaFact]
    public void TheEngineIdlesAfterTheWakeWindowAndWakesOnTheNextScroll()
    {
        var (_, scroller, target) = Show();
        var observer = Observe(new ScrollObserverParams { Target = target });

        ScrollTo(scroller, 250);
        Assert.True(Presto.Engine.IsRunning);

        Frames.Advance(WakeWindow * 2, 16);

        Assert.False(Presto.Engine.IsRunning);
        Assert.False(observer.Reverted);
        Assert.True(ScrollContainer.Containers.ContainsKey(scroller));
        Assert.Equal(0d, observer.Velocity);

        ScrollTo(scroller, 300);
        ScrollTo(scroller, 260);

        Assert.True(Presto.Engine.IsRunning);
        Assert.True(observer.Backward);
        Assert.True(observer.Velocity > 0);
    }

    [AvaloniaFact]
    public void ASyncedObserverSeeksTheLinkedAnimationToTheScrollProgress()
    {
        var (_, scroller, target) = Show();
        var animation = target.Animate(new()
        {
            ["Opacity"] = TweenValue.FromTo(0, 1),
            Duration = 1000,
            Ease = Eases.None,
            Autoplay = false,
        });
        Observe(new ScrollObserverParams { Target = target, Sync = true }).Link(animation);

        ScrollTo(scroller, 250);

        Assert.Equal(500d, animation.CurrentTime);
        Assert.Equal(.5, target.Opacity, 3);

        ScrollTo(scroller, 400);

        Assert.Equal(1000d, animation.CurrentTime);
        Assert.Equal(1d, target.Opacity, 3);
    }
}
