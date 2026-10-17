using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Path = Avalonia.Controls.Shapes.Path;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="Motion"/> states: entering and leaving, layering, token sources, arrival snaps and detach (MOTION-KIT-PLAN §2.5).</summary>
public sealed class MotionStateTests : BridgeTestBase
{
    private enum FormState
    {
        Idle,
        Error,
        Busy,
    }

    private static readonly Point Inside = new(25, 25);
    private static readonly Point Outside = new(150, 150);

    private static AnimationBuilder Linear() => Presto.Build().Duration(100).Ease(Eases.None);

    private static MotionStates States(string when, AnimationBuilder motion, AnimationBuilder? leave = null) =>
        [new MotionState { When = when, Motion = motion, Leave = leave }];

    private static double Get(Visual visual, string property) => (double)PrestoAvalonia.Get(visual, property)!;

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    [AvaloniaFact]
    public void HoverEasesToTheTargetAndBackToRest()
    {
        var box = Box();
        var window = Show(box);
        Motion.SetHover(box, Linear().Scale(1.2));

        window.MouseMove(Inside);
        Frames.Advance(50, 10);
        Assert.InRange(Get(box, "Scale"), 1.05, 1.15);
        Frames.Advance(100, 10);
        Assert.Equal(1.2, Get(box, "Scale"), 6);

        window.MouseMove(Outside);
        Frames.Advance(50, 10);
        Assert.InRange(Get(box, "Scale"), 1.05, 1.15);
        Frames.Advance(100, 10);
        Assert.Equal(1, Get(box, "Scale"), 6);
        Assert.Empty(Presto.GetAnimations(box));
        Assert.Null(box.RenderTransform);
    }

    [AvaloniaFact]
    public void AHoverLiftingTheElementFromUnderThePointerHoldsUntilThePointerLeavesItsSlot()
    {
        var box = Box();
        var window = Show(box);
        Motion.SetHover(box, Linear().Y(-10));

        window.MouseMove(new Point(25, 45));
        Frames.Advance(150, 10);
        window.MouseMove(new Point(26, 46));
        Frames.Advance(150, 10);

        Assert.False(box.IsPointerOver);
        Assert.Equal(-10, Get(box, "Y"), 6);

        window.MouseMove(new Point(26, 60));
        Frames.Advance(150, 10);
        Assert.Equal(0, Get(box, "Y"), 6);
    }

    [AvaloniaFact]
    public void TheReturnUsesLeaveTiming()
    {
        var box = Box();
        var window = Show(box);
        Motion.SetStates(box, States(":pointerover", Linear().Opacity(0.5), Presto.Build().Duration(400).Ease(Eases.None)));
        window.MouseMove(Inside);
        Frames.Advance(200, 10);
        Assert.Equal(0.5, box.Opacity, 6);

        window.MouseMove(Outside);
        Frames.Advance(200, 10);
        Assert.InRange(box.Opacity, 0.7, 0.8);
        Frames.Advance(250, 10);
        Assert.Equal(1, box.Opacity, 6);
    }

    [AvaloniaFact]
    public void RestFollowsALocalValueChangedMidHover()
    {
        var box = Box();
        var window = Show(box);
        Motion.SetHover(box, Linear().Opacity(0.5));
        window.MouseMove(Inside);
        Frames.Advance(200, 10);

        box.Opacity = 0.8;
        Assert.Equal(0.5, box.Opacity, 6);

        window.MouseMove(Outside);
        Frames.Advance(200, 10);
        Assert.Equal(0.8, box.Opacity, 6);
        Assert.Empty(Presto.GetAnimations(box));
        Assert.Equal(0.8, (double)box.GetBaseValue((AvaloniaProperty)Visual.OpacityProperty)!, 6);
    }

    [AvaloniaFact]
    public void PressLayersOverHoverOnASharedScale()
    {
        var box = Box();
        var window = Show(box);
        Motion.SetHover(box, Linear().Scale(1.2).Opacity(0.5));
        Motion.SetPress(box, Linear().Scale(0.9));
        window.MouseMove(Inside);
        Frames.Advance(200, 10);

        window.MouseDown(Inside, MouseButton.Left);
        Frames.Advance(200, 10);
        Assert.Equal(0.9, Get(box, "Scale"), 6);
        Assert.Equal(0.5, box.Opacity, 6);

        window.MouseUp(Inside, MouseButton.Left);
        Frames.Advance(200, 10);
        Assert.Equal(1.2, Get(box, "Scale"), 6);

        Click(window, Inside);
        Frames.Advance(200, 10);
        window.MouseMove(Outside);
        Frames.Advance(200, 10);
        Assert.Equal(1, Get(box, "Scale"), 6);
        Assert.Equal(1, box.Opacity, 6);
        Assert.Empty(Presto.GetAnimations(box));
    }

    [AvaloniaFact]
    public void LeavingMidEnterReturnsFromTheCurrentValue()
    {
        var box = Box();
        var window = Show(box);
        Motion.SetHover(box, Linear().X(100));
        window.MouseMove(Inside);
        Frames.Advance(50, 10);
        var midway = Get(box, "X");
        Assert.InRange(midway, 30, 70);

        window.MouseMove(Outside);
        Frames.Advance(10);
        Assert.InRange(Get(box, "X"), midway - 15, midway - 1);
    }

    [AvaloniaFact]
    public void AClassAddedAndRemovedFromCodeEntersAndLeaves()
    {
        var box = Box();
        Show(box);
        Motion.SetStates(box, States(".selected", Linear().Opacity(0.4)));

        box.Classes.Add("selected");
        Frames.Advance(200, 10);
        Assert.Equal(0.4, box.Opacity, 6);

        box.Classes.Remove("selected");
        Frames.Advance(200, 10);
        Assert.Equal(1, box.Opacity, 6);
    }

    // A plain geometry tween resamples a two-figure shape as one polyline, which loses the bar until the return settles.
    [AvaloniaFact]
    public void AGeometryMorphedByAStateMorphsBackToRest()
    {
        var rest = Geometry.Parse("M6,5 L15,12 L6,19 Z M17,5 L19,5 L19,19 L17,19 Z");
        var path = new Path { Data = rest, Fill = Brushes.Black };
        Show(path);
        Motion.SetStates(path, States(".on", Linear().Data(PrestoAvalonia.MorphTo(Geometry.Parse("M6,5 L12.5,12 L6,19 Z M12.5,5 L19,12 L12.5,19 Z")))));
        path.Classes.Add("on");
        Frames.Advance(200, 10);

        path.Classes.Remove("on");
        Frames.Advance(50, 10);
        Assert.True(path.Data!.FillContains(new Point(18, 12)));
        Assert.True(path.Data.FillContains(new Point(7, 12)));
        Frames.Advance(200, 10);
        Assert.Same(rest, path.Data);
    }

    [AvaloniaFact]
    public void AGeometryMorphedByAStateReturnsToRestAtOnceWithAZeroDurationLeave()
    {
        var rest = Geometry.Parse("M6,5 L15,12 L6,19 Z M17,5 L19,5 L19,19 L17,19 Z");
        var path = new Path { Data = rest, Fill = Brushes.Black };
        Show(path);
        Motion.SetStates(path, States(".on", Linear().Data(PrestoAvalonia.MorphTo(Geometry.Parse("M6,5 L12.5,12 L6,19 Z M12.5,5 L19,12 L12.5,19 Z"))), Presto.Build().Duration(0)));
        path.Classes.Add("on");
        Frames.Advance(200, 10);

        path.Classes.Remove("on");
        Frames.Advance(16);

        Assert.Same(rest, path.Data);
    }

    [AvaloniaFact]
    public void AZeroDurationReturnToRestSettles()
    {
        var box = Box();
        Show(box);
        Motion.SetStates(box, States(".on", Linear().Opacity(0.4), Presto.Build().Duration(0)));
        box.Classes.Add("on");
        Frames.Advance(200, 10);

        box.Classes.Remove("on");
        Frames.Advance(16);
        box.Opacity = 0.8;

        Assert.Equal(0.8, box.Opacity, 6);
    }

    [AvaloniaFact]
    public void AMorphingStateActiveAtAttachSnapsToItsShape()
    {
        var panel = new StackPanel();
        Show(panel);
        var path = new Path { Data = Geometry.Parse("M0,0 L10,0 L10,10 Z"), Fill = Brushes.Black };
        path.Classes.Add("on");
        Motion.SetStates(path, States(".on", Linear().Data(PrestoAvalonia.MorphTo(Geometry.Parse("M0,0 L20,0 L20,20 L0,20 Z")))));

        panel.Children.Add(path);

        Assert.True(path.Data!.FillContains(new Point(2, 18)));
        Frames.Advance(16);
        Assert.Empty(Presto.GetAnimations(path));
    }

    [AvaloniaFact]
    public void CheckedOnAToggleButtonEnters()
    {
        var toggle = new ToggleButton { Content = "t" };
        Show(toggle);
        Motion.SetStates(toggle, States(":checked", Linear().X(10)));

        toggle.IsChecked = true;
        Frames.Advance(200, 10);
        Assert.Equal(10, Get(toggle, "X"), 6);

        toggle.IsChecked = false;
        Frames.Advance(200, 10);
        Assert.Equal(0, Get(toggle, "X"), 6);
    }

    [AvaloniaFact]
    public void StateTokensMatchEnumNamesAndSeveralAtOnce()
    {
        var box = Box();
        Show(box);
        Motion.SetStates(box,
        [
            new MotionState { When = "Error", Motion = Linear().X(10) },
            new MotionState { When = "Busy", Motion = Linear().Y(5) },
        ]);

        Motion.SetState(box, FormState.Error);
        Frames.Advance(200, 10);
        Assert.Equal(10, Get(box, "X"), 6);
        Assert.Equal(0, Get(box, "Y"), 6);

        Motion.SetState(box, "Error, Busy");
        Frames.Advance(200, 10);
        Assert.Equal(10, Get(box, "X"), 6);
        Assert.Equal(5, Get(box, "Y"), 6);

        Motion.SetState(box, FormState.Idle);
        Frames.Advance(200, 10);
        Assert.Equal(0, Get(box, "X"), 6);
        Assert.Equal(0, Get(box, "Y"), 6);
    }

    [AvaloniaFact]
    public void AReattachedElementSnapsToItsActiveStates()
    {
        var panel = new StackPanel();
        Show(panel);
        var box = Box();
        panel.Children.Add(box);
        Motion.SetStates(box, States(".selected", Linear().Opacity(0.4)));
        box.Classes.Add("selected");
        Frames.Advance(200, 10);

        panel.Children.Remove(box);
        Assert.Equal(1, box.Opacity, 6);
        panel.Children.Add(box);

        Assert.Equal(0.4, box.Opacity, 6);
        Frames.Advance(16);
        Assert.Empty(Presto.GetAnimations(box));
        Assert.Equal(0.4, box.Opacity, 6);
    }

    [AvaloniaFact]
    public void ALoopingStateActiveAtAttachKeepsLooping()
    {
        var panel = new StackPanel();
        Show(panel);
        var box = Box();
        box.Classes.Add("busy");
        Motion.SetStates(box, States(".busy", Presto.Build().Ease(Eases.None).Loop()
            .Keyframes(k => k.Frame(f => f.X(20).Duration(100)).Frame(f => f.X(0).Duration(100)))));

        panel.Children.Add(box);
        Frames.Advance(50, 10);

        Assert.InRange(Get(box, "X"), 5, 15);
        Assert.Single(Presto.GetAnimations(box));
    }

    [AvaloniaFact]
    public void DetachRevertsEveryStateAnimation()
    {
        var panel = new StackPanel();
        Show(panel);
        var box = Box();
        panel.Children.Add(box);
        Motion.SetStates(box,
        [
            new MotionState { When = ".selected", Motion = Linear().Opacity(0.4) },
            new MotionState { When = ".busy", Motion = Linear().X(20) },
        ]);
        box.Classes.Add("selected");
        Frames.Advance(200, 10);
        box.Classes.Add("busy");
        Frames.Advance(50, 10);

        panel.Children.Remove(box);

        Assert.Empty(Presto.GetAnimations(box));
        Assert.Equal(1, box.Opacity, 6);
        Assert.Equal(0, Get(box, "X"), 6);
    }

    [AvaloniaFact]
    public void AKeyframedLoopingStateLoopsWhileActiveAndReturnsToRest()
    {
        var box = Box();
        Show(box);
        var wobble = Presto.Build().Ease(Eases.None).Loop()
            .Keyframes(k => k.Frame(f => f.X(20).Duration(100)).Frame(f => f.X(0).Duration(100)));
        Motion.SetStates(box, States(".busy", wobble, Presto.Build().Duration(100)));

        box.Classes.Add("busy");
        Frames.Advance(100, 10);
        Assert.InRange(Get(box, "X"), 15, 20);
        Frames.Advance(750, 10);
        Assert.InRange(Get(box, "X"), 5, 15);
        Assert.Single(Presto.GetAnimations(box));

        box.Classes.Remove("busy");
        Frames.Advance(200, 10);
        Assert.Equal(0, Get(box, "X"), 6);
        Assert.Empty(Presto.GetAnimations(box));
    }

    [AvaloniaFact]
    public void ReturningToALowerLoopingStateResumesItsLoop()
    {
        var box = Box();
        Show(box);
        Motion.SetStates(box,
        [
            new MotionState { When = ".busy", Motion = Linear().Loop().Alternate().X(20) },
            new MotionState { When = ".lifted", Motion = Linear().X(40) },
        ]);
        box.Classes.Add("busy");
        Frames.Advance(50, 10);
        box.Classes.Add("lifted");
        Frames.Advance(200, 10);
        Assert.Equal(40, Get(box, "X"), 6);

        box.Classes.Remove("lifted");
        Frames.Advance(250, 10);
        var x = Get(box, "X");
        Frames.Advance(50, 10);

        Assert.NotEqual(x, Get(box, "X"), 3);
        Assert.Single(Presto.GetAnimations(box));
    }

    [AvaloniaFact]
    public void AHeldLoopingStateReplaysInItsNewFormWhenTheFlagFlips()
    {
        var box = Box();
        Show(box);
        Motion.SetStates(box, States(".busy", Linear().Loop().Alternate().X(20).WhenReduced(reduced => reduced.Opacity(0.5).Loop(false).Alternate(false))));
        box.Classes.Add("busy");
        Frames.Advance(50, 10);
        Assert.InRange(Get(box, "X"), 1, 19);

        Presto.Defaults.ReducedMotion = true;
        Frames.Advance(200, 10);

        Assert.Equal(0, Get(box, "X"), 6);
        Assert.Equal(0.5, box.Opacity, 6);

        Presto.Defaults.ReducedMotion = false;
        Frames.Advance(150, 10);
        var x = Get(box, "X");
        Frames.Advance(30, 10);

        Assert.Equal(1, box.Opacity, 6);
        Assert.NotEqual(x, Get(box, "X"), 3);
    }

    [AvaloniaFact]
    public void AHeldFiniteStateKeepsItsValuesUntilLeft()
    {
        var box = Box();
        Show(box);
        Motion.SetStates(box, States(".on", Linear().X(20).WhenReduced(reduced => reduced.Opacity(0.5))));
        box.Classes.Add("on");
        Frames.Advance(150, 10);

        Presto.Defaults.ReducedMotion = true;
        Frames.Advance(150, 10);

        Assert.Equal(20, Get(box, "X"), 6);
        Assert.Equal(1, box.Opacity, 6);

        box.Classes.Remove("on");
        Frames.Advance(150, 10);
        box.Classes.Add("on");
        Frames.Advance(150, 10);

        Assert.Equal(0, Get(box, "X"), 6);
        Assert.Equal(0.5, box.Opacity, 6);
    }

    [AvaloniaFact]
    public void AReplayLeavesAHeldFiniteStateOnTheSameElementInItsForm()
    {
        var box = Box();
        Show(box);
        Motion.SetStates(box,
        [
            new MotionState { When = ".busy", Motion = Linear().Loop().Alternate().X(20).WhenReduced(reduced => reduced.X(10).Loop(false).Alternate(false)) },
            new MotionState { When = ".on", Motion = Linear().Y(30).WhenReduced(reduced => reduced.Opacity(0.5)) },
        ]);
        box.Classes.Add("busy");
        box.Classes.Add("on");
        Frames.Advance(150, 10);

        Presto.Defaults.ReducedMotion = true;
        Frames.Advance(200, 10);

        Assert.Equal(10, Get(box, "X"), 6);
        Assert.Equal(30, Get(box, "Y"), 6);
        Assert.Equal(1, box.Opacity, 6);
    }

    [AvaloniaFact]
    public void ReturningToALowerKeyframedStateReplaysItsKeyframes()
    {
        var box = Box();
        Show(box);
        Motion.SetStates(box,
        [
            new MotionState
            {
                When = ".busy",
                Motion = Presto.Build().Ease(Eases.None).Keyframes(k => k.Frame(f => f.X(20).Duration(100)).Frame(f => f.X(0).Duration(100))),
            },
            new MotionState { When = ".lifted", Motion = Linear().X(40) },
        ]);
        box.Classes.Add("busy");
        box.Classes.Add("lifted");
        Frames.Advance(300, 10);
        Assert.Equal(40, Get(box, "X"), 6);

        box.Classes.Remove("lifted");
        Frames.Advance(100, 10);
        Assert.InRange(Get(box, "X"), 15, 25);
        Frames.Advance(150, 10);
        Assert.Equal(0, Get(box, "X"), 6);
    }

    [AvaloniaFact]
    public void NothingIsTrackedInDesignMode()
    {
        var isDesignMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!; // The setter is internal.
        var box = Box();
        var window = Show(box);
        isDesignMode.SetValue(null, true);
        IDisposable handle;
        try
        {
            Motion.SetHover(box, Linear().Scale(1.2));
            handle = box.WhileHover(Linear().Opacity(0.5));
        }
        finally
        {
            isDesignMode.SetValue(null, false);
        }

        window.MouseMove(Inside);
        Frames.Advance(200, 10);
        Assert.Empty(Presto.GetAnimations(box));
        handle.Dispose();
    }

    [AvaloniaFact]
    public void SettingTheAttachedPropertyValidatesTheState() =>
        Assert.Throws<NotSupportedException>(() => Motion.SetHover(Box(), Presto.Build().X("+=4")));

    [AvaloniaFact]
    public void PressIsSynthesizedOnABorder()
    {
        var box = Box();
        var window = Show(box);
        Motion.SetPress(box, Linear().Opacity(0.5));

        window.MouseDown(Inside, MouseButton.Left);
        Frames.Advance(200, 10);
        Assert.Equal(0.5, box.Opacity, 6);
        Assert.DoesNotContain(":pressed", box.Classes);

        window.MouseUp(Inside, MouseButton.Left);
        Frames.Advance(200, 10);
        Assert.Equal(1, box.Opacity, 6);
    }

    [AvaloniaFact]
    public void ARightButtonPressDoesNotCount()
    {
        var box = Box();
        var window = Show(box);
        Motion.SetStates(box, States(":pressed", Linear().Opacity(0.5)));

        window.MouseDown(Inside, MouseButton.Right);
        Frames.Advance(200, 10);
        Assert.Equal(1, box.Opacity, 6);
    }

    [AvaloniaFact]
    public void AButtonsOwnHandledPressStillCounts()
    {
        var button = new Button { Content = "press", Width = 80, Height = 30 };
        var window = Show(button);
        Motion.SetPress(button, Linear().Opacity(0.5));

        window.MouseDown(Center(button, window), MouseButton.Left);
        Frames.Advance(200, 10);
        Assert.Equal(0.5, button.Opacity, 6);

        window.MouseUp(Center(button, window), MouseButton.Left);
        Frames.Advance(200, 10);
        Assert.Equal(1, button.Opacity, 6);
    }

    [AvaloniaFact]
    public void CaptureLossEndsThePress()
    {
        var box = Box();
        var window = Show(box);
        Motion.SetPress(box, Linear().Opacity(0.5));
        IPointer? pointer = null;
        box.PointerPressed += (_, e) => pointer = e.Pointer;

        window.MouseDown(Inside, MouseButton.Left);
        Frames.Advance(200, 10);
        Assert.Equal(0.5, box.Opacity, 6);

        pointer!.Capture(window);
        Frames.Advance(200, 10);
        Assert.Equal(1, box.Opacity, 6);
    }

    [AvaloniaFact]
    public void FocusSugarPlaysOnKeyboardFocusButNotOnAClick()
    {
        var first = new Button { Content = "one", Width = 80, Height = 30 };
        var second = new Button { Content = "two", Width = 80, Height = 30 };
        var window = Show(new StackPanel { Children = { first, second } });
        Motion.SetFocus(first, Linear().Opacity(0.5));

        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "	");
        Frames.Advance(200, 10);
        Assert.Equal(0.5, first.Opacity, 6);

        Click(window, Center(second, window));
        Frames.Advance(200, 10);
        Assert.Equal(1, first.Opacity, 6);

        Click(window, Center(first, window));
        Frames.Advance(200, 10);
        Assert.True(first.IsFocused);
        Assert.Equal(1, first.Opacity, 6);
    }

    [AvaloniaFact]
    public void AFocusConditionPlaysOnKeyboardFocusAndOnAClick()
    {
        var first = new Button { Content = "one", Width = 80, Height = 30 };
        var second = new Button { Content = "two", Width = 80, Height = 30 };
        var window = Show(new StackPanel { Children = { first, second } });
        Motion.SetStates(first, States(":focus", Linear().Opacity(0.5)));

        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "	");
        Frames.Advance(200, 10);
        Assert.Equal(0.5, first.Opacity, 6);

        Click(window, Center(second, window));
        Frames.Advance(200, 10);
        Assert.Equal(1, first.Opacity, 6);

        Click(window, Center(first, window));
        Frames.Advance(200, 10);
        Assert.Equal(0.5, first.Opacity, 6);
    }

    [AvaloniaFact]
    public void WhileHoverPlaysOnHoverAndMatchesItsBuilderTwin()
    {
        var card = Box();
        var twin = Box();
        var cardWindow = Show(card);
        var twinWindow = Show(twin);
        card.WhileHover(b => b.Y(-4).Scale(1.02));
        twin.WhileHover(Presto.Build().Y(-4).Scale(1.02));

        cardWindow.MouseMove(Inside);
        twinWindow.MouseMove(Inside);
        Frames.Advance(100, 10);
        Assert.InRange(Get(card, "Y"), -3.99, -0.01);
        Assert.Equal(Get(twin, "Y"), Get(card, "Y"), 6);
        Assert.Equal(Get(twin, "Scale"), Get(card, "Scale"), 6);

        Frames.Advance(2000, 10);
        Assert.Equal(-4, Get(card, "Y"), 6);
        Assert.Equal(1.02, Get(card, "Scale"), 6);
    }

    [AvaloniaFact]
    public void ACodeStateWinsOverADeclaredStateForASharedProperty()
    {
        var box = Box();
        Show(box);
        box.While(".selected", Linear().Opacity(0.7));
        Motion.SetStates(box, States(".selected", Linear().Opacity(0.4).X(10)));

        box.Classes.Add("selected");
        Frames.Advance(200, 10);
        Assert.Equal(0.7, box.Opacity, 6);
        Assert.Equal(10, Get(box, "X"), 6);
    }

    [AvaloniaFact]
    public void DisposingMidStateReturnsToRest()
    {
        var box = Box();
        Show(box);
        var handle = box.While(".selected", Linear().Opacity(0.5));
        box.Classes.Add("selected");
        Frames.Advance(50, 10);

        handle.Dispose();
        handle.Dispose();
        Frames.Advance(200, 10);
        Assert.Equal(1, box.Opacity, 6);
        Assert.Empty(Presto.GetAnimations(box));

        box.Classes.Remove("selected");
        box.Classes.Add("selected");
        Frames.Advance(200, 10);
        Assert.Equal(1, box.Opacity, 6);
    }

    [AvaloniaFact]
    public void AStyleSharedStatesListKeepsSeparateStatePerControl()
    {
        var view = new MotionXamlView();
        Show(view);
        var first = view.FindControl<Border>("First")!;
        var second = view.FindControl<Border>("Second")!;
        var third = view.FindControl<Border>("Third")!;
        Assert.Same(Motion.GetStates(first), Motion.GetStates(second));
        Assert.Same(Motion.GetStates(first), Motion.GetStates(third));

        first.Classes.Add("selected");
        Frames.Advance(200, 10);
        second.Classes.Add("selected");
        Frames.Advance(50, 10);
        Assert.Equal(0.4, first.Opacity, 6);
        Assert.InRange(second.Opacity, 0.6, 0.8);
        Assert.Equal(1, third.Opacity, 6);

        first.Classes.Remove("selected");
        Frames.Advance(200, 10);
        Assert.Equal(1, first.Opacity, 6);
        Assert.Equal(0.4, second.Opacity, 6);
        Assert.Equal(1, third.Opacity, 6);
    }

    [AvaloniaFact]
    public void ADescendantSelectorSettingStateDrivesTheChild()
    {
        var view = new MotionXamlView();
        var window = Show(view);
        var card = view.FindControl<Border>("Card")!;
        var arrow = view.FindControl<Path>("Arrow")!;

        window.MouseMove(Center(card, window));
        Frames.Advance(200, 10);
        Assert.Equal("hot", Motion.GetState(arrow));
        Assert.Equal(10, Get(arrow, "X"), 6);

        window.MouseMove(Outside);
        Frames.Advance(200, 10);
        Assert.Null(Motion.GetState(arrow));
        Assert.Equal(0, Get(arrow, "X"), 6);
    }

    [AvaloniaFact]
    public void ALeavePerPropertyDelayHoldsThatPropertyBeforeItReturns()
    {
        var box = Box();
        var window = Show(box);
        Motion.SetStates(box, States(":pointerover", Linear().Opacity(0.5).X(20), Linear().Property("Opacity", delay: 100)));
        window.MouseMove(Inside);
        Frames.Advance(200, 10);

        window.MouseMove(Outside);
        Frames.Advance(80, 10);
        Assert.Equal(0.5, box.Opacity, 6);
        Assert.InRange(Get(box, "X"), 1, 8);
        Frames.Advance(170, 10);
        Assert.Equal(1, box.Opacity, 6);
        Assert.Empty(Presto.GetAnimations(box));
    }

    [AvaloniaFact]
    public void AStatePropertyWithNoRestThrowsWhenTheStateIsSet()
    {
        var box = Box();

        Assert.Throws<NotSupportedException>(() => Motion.SetStates(box, States(".busy", Presto.Build().Property("NoSuchProperty", 1))));
        Assert.Throws<NotSupportedException>(() => Box().While(".busy", Presto.Build().Property("NoSuchProperty", 1)));
    }
}