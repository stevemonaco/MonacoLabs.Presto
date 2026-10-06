using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge.Tests;

public sealed class WhenDetachedTests : BridgeTestBase
{
    private static readonly AnimationBuilder Fade = Presto.Build().Opacity(0).Duration(200).Ease("linear");

    private readonly StackPanel _panel = new();
    private readonly Window _window;

    public WhenDetachedTests() => _window = Show(_panel);

    [AvaloniaFact]
    public void ADetachedLoopPausesAndTheEngineIdles()
    {
        var box = Attached();
        var animation = Loop(box);
        Frames.Advance(200, 16);

        Detach(box);

        Assert.True(animation.Paused);
        Assert.False(animation.IsScheduled);
        Assert.Null(Presto.Engine._head);
        Assert.False(Frames.IsRunning);
    }

    [AvaloniaFact]
    public void ReattachingResumesAtTheSameProgress()
    {
        var box = Attached();
        var animation = Loop(box);
        Frames.Advance(200, 16);
        Detach(box);
        var stopped = animation.CurrentTime;
        var opacity = box.Opacity;

        _panel.Children.Add(box);
        Frames.Advance(500, 16);
        Assert.False(animation.Paused);
        Assert.InRange(animation.CurrentTime, stopped + 480, stopped + 530);
        Assert.NotEqual(opacity, box.Opacity);
    }

    [AvaloniaFact]
    public void AControlDetachedForGoodIsCollectedWithItsAnimation()
    {
        var (box, animation) = AnimateAndRemove();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(box.IsAlive);
        Assert.False(animation.IsAlive);
    }

    [AvaloniaFact]
    public void RevertWritesTheBaseValuesBack()
    {
        var box = Attached();
        var animation = Loop(box, WhenDetached.Revert);
        Frames.Advance(200, 16);
        Assert.NotEqual(1, box.Opacity);

        Detach(box);

        Assert.Equal(1, box.Opacity);
        Assert.True(animation.Canceled);
    }

    [AvaloniaFact]
    public void ContinueKeepsPlaying()
    {
        PrestoAvalonia.WhenDetached = WhenDetached.Continue;
        var box = Attached();
        var animation = Loop(box);
        Frames.Advance(200, 16);

        Detach(box);
        var detachedAt = animation.CurrentTime;
        Frames.Advance(100, 16);

        Assert.True(animation.IsScheduled);
        Assert.True(animation.CurrentTime > detachedAt);
    }

    [AvaloniaFact]
    public void AnAnimationOnABrushTheBridgeMadeFollowsItsVisual()
    {
        var box = Attached();
        Seeked(box, new AvaloniaAnimationParams { ["Background"] = "#0000ff" });
        var brush = (SolidColorBrush)box.Background!;
        var animation = Presto.Animate(brush, new AnimationParams { ["Opacity"] = 0, Duration = 1000, Loop = true });
        Frames.Advance(200, 16);

        Detach(box);

        Assert.True(animation.Paused);
    }

    [AvaloniaFact]
    public void AVisualAndAPlainObjectTogetherContinue()
    {
        var box = Attached();
        var animation = Presto.Animate(new object[] { box, new Plain() }, new AnimationParams { ["Opacity"] = 0, Duration = 1000, Loop = true });
        Frames.Advance(200, 16);

        Detach(box);

        Assert.False(animation.Paused);
        Assert.True(animation.IsScheduled);
    }

    [AvaloniaFact]
    public void AnAnimationCreatedBeforeAttachKeepsRunningAndPausesOnALaterDetach()
    {
        var box = Box();
        var animation = Loop(box);
        Frames.Advance(200, 16);
        Assert.False(animation.Paused);

        _panel.Children.Add(box);
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(100, 16);
        Assert.False(animation.Paused);

        Detach(box);
        Assert.True(animation.Paused);
    }

    [AvaloniaFact]
    public void AUserPauseBeforeDetachIsNotResumedOnReattach()
    {
        var box = Attached();
        var animation = Loop(box);
        Frames.Advance(200, 16);
        animation.Pause();

        Detach(box);
        _panel.Children.Add(box);
        Frames.Advance(100, 16);

        Assert.True(animation.Paused);
    }

    [AvaloniaFact]
    public void AUserResumeWhileDetachedResumes()
    {
        var box = Attached();
        var animation = Loop(box);
        Frames.Advance(200, 16);
        Detach(box);

        animation.Resume();
        Frames.Advance(100, 16);

        Assert.True(animation.IsScheduled);
        Assert.False(animation.Paused);
    }

    [AvaloniaFact]
    public void TheParamsOverrideWinsOverTheDefault()
    {
        PrestoAvalonia.WhenDetached = WhenDetached.Continue;
        var box = Attached();
        var animation = Loop(box, WhenDetached.Pause);
        Frames.Advance(200, 16);

        Detach(box);

        Assert.True(animation.Paused);
    }

    [AvaloniaFact]
    public void TheBuilderOverrideWinsOverTheDefault()
    {
        var box = Attached();
        var animation = box.Animate(b => b.Opacity(0).Duration(1000).Loop().WhenDetached(WhenDetached.Continue));
        Frames.Advance(200, 16);

        Detach(box);

        Assert.False(animation.Paused);
    }

    [AvaloniaFact]
    public void TheReducedFormKeepsTheOverride()
    {
        Presto.Defaults.ReducedMotion = true;
        var box = Attached();
        var animation = Loop(box, WhenDetached.Continue, new() { ["Opacity"] = 0.5, Duration = 1000 });
        Frames.Advance(200, 16);

        Detach(box);

        Assert.False(animation.Paused);
    }

    [AvaloniaFact]
    public void AnAwaitedThenCompletesAfterReattach()
    {
        var box = Attached();
        var done = Presto.Animate(box, new AnimationParams { ["Opacity"] = 0, Duration = 1000 }).Then();
        Frames.Advance(200, 16);

        Detach(box);
        Frames.Advance(2000, 16);
        Dispatcher.UIThread.RunJobs();
        Assert.False(done.IsCompleted);

        _panel.Children.Add(box);
        Frames.Advance(1000, 16);
        Dispatcher.UIThread.RunJobs();
        Assert.True(done.IsCompleted);
    }

    [AvaloniaFact]
    public void ATimelinePausesOnlyOnceEveryChildsVisualHasDetachedAndResumesOnReattach()
    {
        var (first, second) = (Attached(), Attached());
        var timeline = LoopOver(first, second);
        Frames.Advance(200, 16);

        Detach(first);
        Assert.False(timeline.Paused);
        Assert.True(timeline.IsScheduled);

        Detach(second);
        Assert.True(timeline.Paused);
        var stopped = timeline.CurrentTime;

        _panel.Children.Add(first);
        Frames.Advance(500, 16);
        Assert.False(timeline.Paused);
        Assert.InRange(timeline.CurrentTime, stopped + 480, stopped + 530);
    }

    [AvaloniaFact]
    public void ATimelineWithAPlainObjectChildContinues()
    {
        var box = Attached();
        var timeline = Presto.CreateTimeline(new TimelineParams { Loop = true })
            .Add(box, new AnimationParams { ["Opacity"] = 0, Duration = 1000 })
            .Add(new Plain(), new AnimationParams { ["Opacity"] = 0, Duration = 1000 }, 0);
        Frames.Advance(200, 16);

        Detach(box);

        Assert.False(timeline.Paused);
        Assert.True(timeline.IsScheduled);
    }

    [AvaloniaFact]
    public void RevertRevertsEveryChild()
    {
        PrestoAvalonia.WhenDetached = WhenDetached.Revert;
        var (first, second) = (Attached(), Attached());
        var timeline = LoopOver(first, second);
        Frames.Advance(200, 16);

        Detach(first);
        Detach(second);

        Assert.True(timeline.Canceled);
        Assert.Equal(1, first.Opacity);
        Assert.Equal(1, second.Opacity);
    }

    [AvaloniaFact]
    public void AnExitGhostRunsToCompletionWhenItsWindowCloses()
    {
        var box = Attached();
        var ghost = PrestoAvalonia.AnimateExit(box, Fade)!;
        _panel.Children.Remove(box);
        Frames.Advance(50, 16);

        _window.Close();
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(300, 16);

        Assert.True(ghost.Completed);
    }

    [AvaloniaFact]
    public void APresenceEnterOnADetachingControlIsRevertedByPresenceNotHeldPaused()
    {
        Timer? enter = null;
        var box = Box();
        Presence.SetEnter(box, Presto.Build().Opacity(from: 0).Duration(200).Ease("linear").OnBegin(t => enter = t));
        Attach(box);
        Frames.Advance(50, 16);

        Detach(box);

        Assert.True(enter!.Canceled);
        Assert.Equal(1, box.Opacity);
    }

    [AvaloniaFact]
    public void AMotionStateAnimationIsLeftToMotion()
    {
        var box = Attached();
        Motion.SetStates(box, [new MotionState { When = ".selected", Motion = Presto.Build().Opacity(0.4).Duration(100).Ease("linear") }]);
        box.Classes.Add("selected");
        Frames.Advance(50, 10);

        Detach(box);
        Attach(box);
        Frames.Advance(200, 10);

        Assert.Equal(0.4, box.Opacity, 6);
    }

    [AvaloniaFact]
    public void APageTransitionFinishesWhenItsPagesDetach()
    {
        var (from, to) = (Attached(), Attached());
        var transition = new PrestoPageTransition { Enter = Presto.Build().Opacity(from: 0).Duration(100).Ease("linear"), Exit = Fade, UseCompositor = false };
        var done = transition.Start(from, to, true, CancellationToken.None);
        Frames.Advance(50, 16);

        Detach(from);
        Detach(to);
        Frames.Advance(300, 16);
        Dispatcher.UIThread.RunJobs();

        Assert.True(done.IsCompleted);
    }

    [AvaloniaFact]
    public void ALayoutTransitionFinishesWhenItsControlDetaches()
    {
        var spacer = new Border { Height = 10 };
        _panel.Children.Add(spacer);
        var box = Attached();
        var transition = box.AnimateLayout(200, Eases.None);
        spacer.Height += 40;
        _window.UpdateLayout();
        Frames.Advance(50, 16);
        Assert.False(transition.Completed);

        Detach(box);
        var detachedAt = transition.Progress;
        Frames.Advance(16);
        Assert.True(transition.Progress > detachedAt);

        Frames.Advance(300, 16);
        Assert.True(transition.Completed);
    }

    private Timeline LoopOver(Border first, Border second) =>
        Presto.CreateTimeline(new TimelineParams { Loop = true })
            .Add(first, new AnimationParams { ["Opacity"] = 0, Duration = 1000, Ease = "linear" })
            .Add(second, new AnimationParams { ["Opacity"] = 0, Duration = 1000, Ease = "linear" }, 0);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private (WeakReference Box, WeakReference Animation) AnimateAndRemove()
    {
        var box = Attached();
        var animation = Loop(box);
        Frames.Advance(200, 16);
        Detach(box);
        return (new WeakReference(box), new WeakReference(animation));
    }

    private Border Attached()
    {
        var box = Box();
        Attach(box);
        return box;
    }

    private void Attach(Border box)
    {
        _panel.Children.Add(box);
        Dispatcher.UIThread.RunJobs();
    }

    private void Detach(Border box)
    {
        _panel.Children.Remove(box);
        Dispatcher.UIThread.RunJobs();
        // The renderer holds a removed visual until its next pass, which would keep it alive past the collection test's GC.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(16);
        Frames.Advance(16);
    }

    private static Animation Loop(Border box, WhenDetached? whenDetached = null, AvaloniaAnimationParams? whenReduced = null) =>
        Presto.Animate(box, new AvaloniaAnimationParams
        {
            [Visual.OpacityProperty] = 0,
            Duration = 1000,
            Loop = true,
            Ease = "linear",
            WhenDetached = whenDetached,
            WhenReduced = whenReduced,
        });

    private sealed class Plain
    {
        public double Opacity { get; set; } = 1;
    }
}
