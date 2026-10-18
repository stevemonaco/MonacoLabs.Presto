using System.ComponentModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Text;

namespace MonacoLabs.Presto.Tests;

/// <summary>The <see cref="Animate"/> triggers: OnAttached plays on attach, OnTapped on each tap and OnChanged on each later Watch change.</summary>
public sealed class AnimateAttachedTests : BridgeTestBase
{
    private static StackPanel ShowPanel()
    {
        var panel = new StackPanel();
        new Window { Content = panel, Width = 100, Height = 100 }.Show();
        Dispatcher.UIThread.RunJobs();
        return panel;
    }

    private static Border AddBorder(Panel panel)
    {
        var border = new Border();
        panel.Children.Add(border);
        return border;
    }

    private static AnimationBuilder SlideX(double to) => Presto.Build().X(to).Duration(100).Ease(Eases.None);

    [AvaloniaFact]
    public void SettingOnAnAttachedControlPlaysOneAnimationToItsEnd()
    {
        var border = AddBorder(ShowPanel());

        Animate.SetOnAttached(border, SlideX(100));

        Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(200, 16);
        Assert.Equal(100d, PrestoAvalonia.Get(border, "X"));
    }

    [AvaloniaFact]
    public void SettingBeforeAttachmentWaitsForTheAttach()
    {
        var panel = ShowPanel();
        var border = new Border();

        Animate.SetOnAttached(border, SlideX(100));
        Assert.Empty(Presto.GetAnimations(border));

        panel.Children.Add(border);
        Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(200, 16);
        Assert.Equal(100d, PrestoAvalonia.Get(border, "X"));
    }

    [AvaloniaFact]
    public void DetachingRevertsAndReattachingPlaysFromTheStart()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        Animate.SetOnAttached(border, SlideX(100));
        Frames.Advance(50, 10);
        var midway = (double)PrestoAvalonia.Get(border, "X")!;
        Assert.InRange(midway, 1, 99);

        panel.Children.Remove(border);
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));
        Assert.Empty(Presto.GetAnimations(border));

        panel.Children.Add(border);
        Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(20, 10);
        Assert.InRange((double)PrestoAvalonia.Get(border, "X")!, 1, midway - 1);
    }

    [AvaloniaFact]
    public void ChangingTheValueRevertsTheFirstAnimationAndPlaysTheSecond()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetOnAttached(border, SlideX(100));
        Frames.Advance(50, 10);
        var first = Assert.Single(Presto.GetAnimations(border));

        Animate.SetOnAttached(border, Presto.Build().Opacity(0).Duration(100).Ease(Eases.None));

        Assert.True(first.Canceled);
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));
        Assert.Single(Presto.GetAnimations(border, "Opacity"));
        Frames.Advance(200, 16);
        Assert.Equal(0, border.Opacity);
    }

    [AvaloniaFact]
    public void SettingNullRevertsAndStopsListening()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        Animate.SetOnAttached(border, SlideX(100));
        Frames.Advance(50, 10);

        Animate.SetOnAttached(border, null);
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));

        panel.Children.Remove(border);
        panel.Children.Add(border);
        Assert.Empty(Presto.GetAnimations(border));
    }

    [AvaloniaFact]
    public void OneBuilderOnTwoControlsGivesIndependentAnimations()
    {
        var panel = ShowPanel();
        var builder = SlideX(100);
        var a = AddBorder(panel);
        var b = AddBorder(panel);

        Animate.SetOnAttached(a, builder);
        Animate.SetOnAttached(b, builder);
        Assert.Single(Presto.GetAnimations(a)).Cancel();
        Frames.Advance(200, 16);

        Assert.Empty(Presto.GetAnimations(a));
        Assert.Equal(100d, PrestoAvalonia.Get(b, "X"));
    }

    [AvaloniaFact]
    public void EachXamlSpellingAnimates()
    {
        var view = new AttachedXamlView { DataContext = new AttachedXamlViewModel() };
        new Window { Content = view }.Show(); // A UserControl's content joins the tree when its template applies, on the first layout.

        Frames.Advance(200, 16);

        Assert.Equal(100d, PrestoAvalonia.Get(view.StaticBorder, "X"));
        Assert.Equal(40d, PrestoAvalonia.Get(view.ExtensionBorder, "X"));
        Assert.Equal(60d, PrestoAvalonia.Get(view.BindingBorder, "X"));
    }

    private static Border AddHitBorder(Panel panel)
    {
        var border = new Border { Height = 50, Background = Brushes.Red };
        panel.Children.Add(border);
        Dispatcher.UIThread.RunJobs();
        return border;
    }

    private static void Tap(Border border)
    {
        var window = (Window)TopLevel.GetTopLevel(border)!;
        var point = border.TranslatePoint(new Point(10, 10), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    [AvaloniaFact]
    public void OnTappedPlaysOnATapAndRevertsWhenItCompletes()
    {
        var border = AddHitBorder(ShowPanel());
        Animate.SetOnTapped(border, SlideX(100));
        Assert.Empty(Presto.GetAnimations(border));

        Tap(border);
        Frames.Advance(50, 10);
        Assert.InRange((double)PrestoAvalonia.Get(border, "X")!, 1, 99);

        Frames.Advance(100, 10);
        Assert.Empty(Presto.GetAnimations(border));
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));
    }

    [AvaloniaFact]
    public void OnTappedRestartsOnEachTap()
    {
        var border = AddHitBorder(ShowPanel());
        Animate.SetOnTapped(border, SlideX(100));
        Tap(border);
        Frames.Advance(60, 10);
        var first = Assert.Single(Presto.GetAnimations(border));

        Tap(border);

        Assert.True(first.Canceled);
        Assert.NotSame(first, Assert.Single(Presto.GetAnimations(border)));
        Frames.Advance(20, 10);
        Assert.InRange((double)PrestoAvalonia.Get(border, "X")!, 1, 50);
    }

    [AvaloniaFact]
    public void ClearingOnTappedStopsListening()
    {
        var border = AddHitBorder(ShowPanel());
        Animate.SetOnTapped(border, SlideX(100));
        Tap(border);
        Frames.Advance(50, 10);

        Animate.SetOnTapped(border, null);
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));

        Tap(border);
        Assert.Empty(Presto.GetAnimations(border));
    }

    [AvaloniaFact]
    public void ChangingOnTappedPlaysTheNewBuilderOnTheNextTapAndRestartsARunningOneWithIt()
    {
        var border = AddHitBorder(ShowPanel());
        Animate.SetOnTapped(border, SlideX(100));
        Animate.SetOnTapped(border, SlideX(-100));
        Assert.Empty(Presto.GetAnimations(border));

        Tap(border);
        Frames.Advance(50, 10);
        var first = Assert.Single(Presto.GetAnimations(border));
        Assert.InRange((double)PrestoAvalonia.Get(border, "X")!, -99, -1);

        Animate.SetOnTapped(border, SlideX(100));

        Assert.True(first.Canceled);
        Frames.Advance(20, 10);
        Assert.InRange((double)PrestoAvalonia.Get(border, "X")!, 1, 49);
    }

    [AvaloniaFact]
    public void ChangingOnChangedMidPlayRevertsItAndPlaysTheNewBuilder()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetOnChanged(border, SlideX(100));
        Animate.SetWatch(border, 1);
        Animate.SetWatch(border, 2);
        Frames.Advance(50, 10);
        var first = Assert.Single(Presto.GetAnimations(border));

        Animate.SetOnChanged(border, SlideX(-100));

        Assert.True(first.Canceled);
        Frames.Advance(50, 10);
        Assert.InRange((double)PrestoAvalonia.Get(border, "X")!, -99, -1);
    }

    [AvaloniaFact]
    public void OnTappedOnAVisualThatIsNotAnInputElementThrows()
    {
        var visual = new Visual();

        Assert.Throws<InvalidOperationException>(() => visual.SetValue(Animate.OnTappedProperty, SlideX(100)));
    }

    [AvaloniaFact]
    public void OnChangedSkipsTheInitialBindingAndPlaysOnEachLaterChange()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        var model = new CountModel { Count = 1 };
        Animate.SetOnChanged(border, SlideX(100));
        border.Bind(Animate.WatchProperty, new Binding(nameof(CountModel.Count)));

        panel.DataContext = model;
        Assert.Equal(1, Animate.GetWatch(border));
        Assert.Empty(Presto.GetAnimations(border));

        model.Count = 2;
        var first = Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(50, 10);

        model.Count = 3;
        Assert.True(first.Canceled);
        Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(200, 16);
        Assert.Empty(Presto.GetAnimations(border));
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));
    }

    [AvaloniaFact]
    public void OnChangedSetAfterTheFirstValuePlaysOnTheNextChange()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetWatch(border, 1);

        Animate.SetOnChanged(border, SlideX(100));
        Assert.Empty(Presto.GetAnimations(border));

        Animate.SetWatch(border, 2);
        Assert.Single(Presto.GetAnimations(border));
    }

    [AvaloniaFact]
    public void ClearingOnChangedStopsListening()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetOnChanged(border, SlideX(100));
        Animate.SetWatch(border, 1);
        Animate.SetWatch(border, 2);
        Frames.Advance(50, 10);

        Animate.SetOnChanged(border, null);
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));

        Animate.SetWatch(border, 3);
        Assert.Empty(Presto.GetAnimations(border));
    }

    [AvaloniaFact]
    public void DetachingRevertsTheTriggers()
    {
        var panel = ShowPanel();
        var tapped = AddHitBorder(panel);
        var changed = AddBorder(panel);
        Animate.SetOnTapped(tapped, SlideX(100));
        Animate.SetOnChanged(changed, SlideX(100));
        Animate.SetWatch(changed, 1);
        Tap(tapped);
        Animate.SetWatch(changed, 2);
        Frames.Advance(50, 10);

        panel.Children.Clear();

        Assert.Empty(Presto.GetAnimations(tapped));
        Assert.Empty(Presto.GetAnimations(changed));
        Assert.Equal(0d, PrestoAvalonia.Get(tapped, "X"));
        Assert.Equal(0d, PrestoAvalonia.Get(changed, "X"));
    }

    private static AnimationBuilder LoopX() => SlideX(100).Loop();

    private static double X(Visual visual) => (double)PrestoAvalonia.Get(visual, "X")!;

    [AvaloniaFact]
    public void AHiddenVisualsLoopIsPausedAndResumesWhereItWasWhenShown()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetOnAttached(border, LoopX());
        Frames.Advance(50, 10);
        var animation = Assert.Single(Presto.GetAnimations(border));
        var hiddenAt = X(border);

        border.IsVisible = false;
        Frames.Advance(500, 10);

        Assert.True(animation.Paused);
        Assert.Equal(hiddenAt, X(border));

        border.IsVisible = true;
        Frames.Advance(20, 10);

        Assert.False(animation.Paused);
        Assert.InRange(X(border), hiddenAt + 1, hiddenAt + 40);
    }

    [AvaloniaFact]
    public void HidingAnAncestorPausesTheLoop()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        Animate.SetOnAttached(border, LoopX());
        Frames.Advance(50, 10);
        var animation = Assert.Single(Presto.GetAnimations(border));

        panel.IsVisible = false;
        Assert.True(animation.Paused);

        panel.IsVisible = true;
        Assert.False(animation.Paused);
    }

    [AvaloniaFact]
    public void AttachingHiddenStartsPausedAndPlaysWhenFirstShown()
    {
        var panel = ShowPanel();
        var border = new Border { IsVisible = false };
        Animate.SetOnAttached(border, SlideX(100));

        panel.Children.Add(border);
        Assert.True(Assert.Single(Presto.GetAnimations(border)).Paused);
        Frames.Advance(200, 16);
        Assert.Equal(0d, X(border));

        border.IsVisible = true;
        Frames.Advance(50, 10);
        Assert.InRange(X(border), 1, 99);
    }

    [AvaloniaFact]
    public void ACompletedAnimationIsLeftAloneWhenHidden()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetOnAttached(border, SlideX(100));
        var animation = Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(200, 16);

        border.IsVisible = false;
        border.IsVisible = true;
        Frames.Advance(50, 10);

        Assert.True(animation.Completed);
        Assert.True(animation.Paused);
        Assert.Equal(100d, X(border));
    }

    [AvaloniaFact]
    public void ShowingDoesNotResumeAPauseTheTriggerDidNotMake()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetOnAttached(border, LoopX());
        Frames.Advance(50, 10);
        var animation = Assert.Single(Presto.GetAnimations(border));
        animation.Pause();
        var pausedAt = X(border);

        border.IsVisible = false;
        border.IsVisible = true;
        Frames.Advance(50, 10);

        Assert.True(animation.Paused);
        Assert.Equal(pausedAt, X(border));
    }

    [AvaloniaFact]
    public void DetachingWhileHiddenRevertsAsBefore()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        Animate.SetOnAttached(border, LoopX());
        Frames.Advance(50, 10);
        border.IsVisible = false;

        panel.Children.Remove(border);

        Assert.Equal(0d, X(border));
        Assert.Empty(Presto.GetAnimations(border));
    }

    [AvaloniaFact]
    public void VisibilityDoesNotAffectOnTappedOrOnChanged()
    {
        var panel = ShowPanel();
        var tapped = AddHitBorder(panel);
        var changed = AddBorder(panel);
        Animate.SetOnTapped(tapped, SlideX(100));
        Animate.SetOnChanged(changed, SlideX(100));
        Animate.SetWatch(changed, 1);
        Tap(tapped);
        Animate.SetWatch(changed, 2);

        panel.IsVisible = false;
        Frames.Advance(50, 10);

        Assert.False(Assert.Single(Presto.GetAnimations(tapped)).Paused);
        Assert.False(Assert.Single(Presto.GetAnimations(changed)).Paused);
        Assert.InRange(X(tapped), 1, 99);
        Assert.InRange(X(changed), 1, 99);
    }

    [AvaloniaFact]
    public void ClearingOnAttachedRemovesTheVisibilityHandler()
    {
        var border = AddBorder(ShowPanel());
        var handlers = typeof(Visual).GetField("IsEffectivelyVisibleChanged", BindingFlags.Instance | BindingFlags.NonPublic)!; // The event is internal.
        Animate.SetOnAttached(border, LoopX());
        Assert.NotNull(handlers.GetValue(border));

        Animate.SetOnAttached(border, null);

        Assert.Null(handlers.GetValue(border));
    }

    [AvaloniaFact]
    public void ATextRevealOnAHiddenBlockWaitsUntilShown()
    {
        var block = TextRevealTests.Block(TextPieces.Words);
        block.IsVisible = false;
        Animate.SetOnAttached(block, Presto.Build().Opacity(from: 0).Duration(2000).Ease("linear"));
        TextRevealTests.Show(block);
        Frames.Advance(500, 50);

        Assert.Equal(0, block.Opacity);

        block.IsVisible = true;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(500, 50);
        var word = block.GetVisualDescendants().OfType<TextBlock>().First(piece => !string.IsNullOrWhiteSpace(piece.Text));
        var animation = Assert.Single(Presto.GetAnimations(word));
        Assert.InRange(word.Opacity, 0.1, 0.4);

        block.IsVisible = false;
        Frames.Advance(500, 50);
        Assert.True(animation.Paused);

        block.IsVisible = true;
        Assert.False(animation.Paused);
    }

    [AvaloniaFact]
    public void AReducedLoopPausesWhenHidden()
    {
        Presto.Defaults.ReducedMotion = true;
        var border = AddBorder(ShowPanel());
        Animate.SetOnAttached(border, LoopX().WhenReduced(reduced => reduced.Opacity(0).Duration(100).Loop()));
        Frames.Advance(50, 10);
        var animation = Assert.Single(Presto.GetAnimations(border, "Opacity"));

        border.IsVisible = false;
        Assert.True(animation.Paused);

        border.IsVisible = true;
        Assert.False(animation.Paused);
    }

    private static AnimationBuilder BounceOrFade() => LoopX().WhenReduced(reduced => reduced.Opacity(0).Loop(false));

    [AvaloniaFact]
    public void ALoopingOnAttachedReplaysItsReducedFormWhenTheFlagIsSet()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetOnAttached(border, BounceOrFade());
        Frames.Advance(50, 10);
        var loop = Assert.Single(Presto.GetAnimations(border));

        Presto.Defaults.ReducedMotion = true;

        Assert.True(loop.Canceled);
        Assert.Equal(0d, X(border));
        Frames.Advance(200, 16);
        Assert.Equal(0, border.Opacity);
        Assert.Equal(0d, X(border));
    }

    [AvaloniaFact]
    public void ASettledReducedFormLoopsAgainWhenTheFlagClears()
    {
        Presto.Defaults.ReducedMotion = true;
        var border = AddBorder(ShowPanel());
        Animate.SetOnAttached(border, BounceOrFade());
        Frames.Advance(200, 16);
        Assert.Equal(0, border.Opacity);

        Presto.Defaults.ReducedMotion = false;

        Assert.Equal(1, border.Opacity);
        Frames.Advance(250, 10);
        Assert.False(Assert.Single(Presto.GetAnimations(border)).Completed);
        Assert.InRange(X(border), 1, 99);
    }

    [AvaloniaFact]
    public void AFiniteOnAttachedFinishesInItsFormWhenTheFlagFlips()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetOnAttached(border, SlideX(100).WhenReduced(reduced => reduced.Opacity(0)));
        Frames.Advance(50, 10);
        var animation = Assert.Single(Presto.GetAnimations(border));

        Presto.Defaults.ReducedMotion = true;

        Assert.Same(animation, Assert.Single(Presto.GetAnimations(border)));
        Frames.Advance(200, 16);
        Assert.Equal(100d, X(border));
        Assert.Equal(1, border.Opacity);
    }

    [AvaloniaFact]
    public void ABuilderWithoutAReducedFormDoesNotRestartOnAReducedMotionFlip()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetOnAttached(border, LoopX());
        Frames.Advance(50, 10);
        var loop = Assert.Single(Presto.GetAnimations(border));

        Presto.Defaults.ReducedMotion = true;

        Assert.False(loop.Canceled);
        Assert.Same(loop, Assert.Single(Presto.GetAnimations(border)));
    }

    [AvaloniaFact]
    public void AReduceAllFlipReplaysALoopCollapsed()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetOnAttached(border, LoopX());
        Frames.Advance(50, 10);
        var loop = Assert.Single(Presto.GetAnimations(border));

        Presto.Defaults.ReduceAll = true;
        Frames.Advance(16);

        Assert.True(loop.Canceled);
        Assert.Equal(100d, X(border));

        Presto.Defaults.ReduceAll = false;
        Frames.Advance(50, 10);

        Assert.InRange(X(border), 1, 99);
    }

    [AvaloniaFact]
    public void ADetachedTriggerIsNotRegistered()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        var engine = new Engine(new ManualFrameSource());
        using (engine.MakeCurrent()) Animate.SetOnAttached(border, BounceOrFade());
        Assert.Single(Followers(engine));

        panel.Children.Remove(border);

        Assert.Empty(Followers(engine));
    }

    [AvaloniaFact]
    public void ARunningLoopingTapAnimationFollowsTheFlag()
    {
        var border = AddHitBorder(ShowPanel());
        Animate.SetOnTapped(border, BounceOrFade());
        Tap(border);
        Frames.Advance(50, 10);
        var loop = Assert.Single(Presto.GetAnimations(border));

        Presto.Defaults.ReducedMotion = true;

        Assert.True(loop.Canceled);
        Assert.Equal(0d, X(border));
        Frames.Advance(50, 10);
        Assert.InRange(border.Opacity, 0.01, 0.99);
        Frames.Advance(100, 10);
        Assert.Empty(Presto.GetAnimations(border));
        Assert.Equal(1, border.Opacity);
    }

    [AvaloniaFact]
    public void DoesNothingInDesignMode()
    {
        var isDesignMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!; // The setter is internal.
        var border = AddBorder(ShowPanel());
        isDesignMode.SetValue(null, true);
        var cue = new Cue();
        var action = new RecordingAction();
        try
        {
            Animate.SetOnAttached(border, SlideX(100));
            Animate.SetCue(border, cue);
            Animate.SetOnCue(border, SlideX(100));
            Animate.SetDoOnCue(border, action);
            Animate.SetDoOnChanged(border, action);
            Animate.SetWatch(border, 1);
        }
        finally
        {
            isDesignMode.SetValue(null, false);
        }
        cue.Raise();

        Assert.Empty(Presto.GetAnimations(border));
        Assert.Empty(action.Calls);
    }

    [AvaloniaFact]
    public void OnCuePlaysOnEachRaiseRestartingAndRevertsWhenItCompletes()
    {
        var border = AddBorder(ShowPanel());
        var cue = new Cue();
        Animate.SetCue(border, cue);
        Animate.SetOnCue(border, SlideX(100));
        Assert.Empty(Presto.GetAnimations(border));

        cue.Raise();
        var first = Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(50, 10);

        cue.Raise();
        Assert.True(first.Canceled);
        Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(200, 16);
        Assert.Empty(Presto.GetAnimations(border));
        Assert.Equal(0d, X(border));
    }

    [AvaloniaFact]
    public void ARaiseWhileDetachedPlaysAndInvokesNothing()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        var cue = new Cue<int>();
        var action = new RecordingAction();
        Animate.SetCue(border, cue);
        Animate.SetOnCue(border, SlideX(100));
        Animate.SetDoOnCue(border, action);
        panel.Children.Remove(border);

        cue.Raise(1);

        Assert.Empty(Presto.GetAnimations(border));
        Assert.Empty(action.Calls);
        panel.Children.Add(border);
        Assert.Empty(action.Calls);
        cue.Raise(2);
        Assert.Equal([(2, false)], action.Calls);
    }

    [AvaloniaFact]
    public void DetachingUnsubscribesFromTheCueAndAttachingSubscribesAgain()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        var source = new CountingObservable();
        Animate.SetCue(border, source);
        Assert.Equal(1, source.Count);

        panel.Children.Remove(border);
        Assert.Equal(0, source.Count);

        panel.Children.Add(border);
        Assert.Equal(1, source.Count);
    }

    [AvaloniaFact]
    public void ChangingOrClearingTheCueUnsubscribesFromTheOldOne()
    {
        var border = AddBorder(ShowPanel());
        var first = new CountingObservable();
        var second = new CountingObservable();
        Animate.SetCue(border, first);

        Animate.SetCue(border, second);
        Assert.Equal(0, first.Count);
        Assert.Equal(1, second.Count);

        Animate.SetCue(border, null);
        Assert.Equal(0, second.Count);
    }

    [AvaloniaFact]
    public void AnObservableSourceDrivesOnCueAndDoOnCue()
    {
        var border = AddBorder(ShowPanel());
        var source = new CountingObservable();
        var action = new RecordingAction();
        Animate.SetCue(border, source);
        Animate.SetOnCue(border, SlideX(100));
        Animate.SetDoOnCue(border, action);

        source.Raise("saved");

        Assert.Single(Presto.GetAnimations(border));
        Assert.Equal([("saved", false)], action.Calls);
    }

    [AvaloniaFact]
    public void ACueThatIsNeitherACueNorAnObservableThrows()
    {
        var border = AddBorder(ShowPanel());

        Assert.Throws<ArgumentException>(() => Animate.SetCue(border, "not a cue"));
    }

    [AvaloniaFact]
    public void DoOnCueGetsTheRaisedValueAsNotFirst()
    {
        var border = AddBorder(ShowPanel());
        var cue = new Cue<string>();
        var action = new RecordingAction();
        Animate.SetCue(border, cue);
        Animate.SetDoOnCue(border, action);

        cue.Raise("a");
        cue.Raise("b");

        Assert.Equal([("a", false), ("b", false)], action.Calls);
        Assert.All(action.Targets, target => Assert.Same(border, target));
    }

    [AvaloniaFact]
    public void DoOnChangedSeesTheFirstValueAsFirstAndLaterValuesAsNot()
    {
        var border = AddBorder(ShowPanel());
        var action = new RecordingAction();
        Animate.SetDoOnChanged(border, action);
        Animate.SetWatch(border, 1);
        Animate.SetWatch(border, 2);

        Assert.Equal([(null, true), (1, false), (2, false)], action.Calls);
    }

    [AvaloniaFact]
    public void DoOnChangedCatchesUpOnAValueThatChangedWhileDetached()
    {
        var panel = ShowPanel();
        var border = new Border();
        var action = new RecordingAction();
        Animate.SetWatch(border, 1);
        Animate.SetDoOnChanged(border, action);
        Assert.Empty(action.Calls);

        panel.Children.Add(border);
        panel.Children.Remove(border);
        Animate.SetWatch(border, 2);
        panel.Children.Add(border);
        panel.Children.Remove(border);
        panel.Children.Add(border);

        Assert.Equal([(1, true), (2, false)], action.Calls);
    }

    [AvaloniaFact]
    public void ADataContextDropAndRestoreInvokesDoOnChangedAsFirst()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        var model = new CountModel { Count = 1 };
        var action = new RecordingAction();
        border.Bind(Animate.WatchProperty, new Binding(nameof(CountModel.Count)));
        panel.DataContext = model;
        Animate.SetDoOnChanged(border, action);

        panel.DataContext = null;
        panel.DataContext = model;
        model.Count = 2;

        Assert.Equal([(1, true), (null, true), (1, true), (2, false)], action.Calls);
    }

    private sealed class RecordingAction : CueAction
    {
        public List<(object? Value, bool First)> Calls { get; } = [];

        public List<Visual> Targets { get; } = [];

        protected internal override void Invoke(Visual target, object? value, bool first)
        {
            Targets.Add(target);
            Calls.Add((value, first));
        }
    }

    private sealed class CountingObservable : IObservable<object?>
    {
        private readonly List<IObserver<object?>> _observers = [];

        public int Count => _observers.Count;

        public void Raise(object? value)
        {
            foreach (var observer in _observers.ToArray()) observer.OnNext(value);
        }

        public IDisposable Subscribe(IObserver<object?> observer)
        {
            _observers.Add(observer);
            return new Unsubscriber(() => _observers.Remove(observer));
        }

        private sealed class Unsubscriber(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }

    private sealed class CountModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Count
        {
            get;
            set
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            }
        }
    }
}
