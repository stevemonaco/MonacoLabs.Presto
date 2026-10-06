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

namespace Monaco.Presto.AvaloniaBridge.Tests;

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
}
