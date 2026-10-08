using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>A propagation source times its followers' state animations with <c>StaggerChildren</c>, <c>DelayChildren</c> and <c>Order</c> (GAPS-PLAN §7.2).</summary>
public sealed class MotionOrchestrationTests : BridgeTestBase
{
    private readonly Dictionary<Control, double> _begins = [];
    private double _start;

    private Border Child()
    {
        var box = Box();
        Motion.SetStates(box,
        [
            new MotionState { When = "Open", Motion = Presto.Build().Duration(100).Ease(Eases.None).Opacity(0).OnBegin(_ => _begins[box] = Frames.Now - _start) },
        ]);
        return box;
    }

    private void Open(Control parent, double run = 400)
    {
        _start = Frames.Now;
        Motion.SetState(parent, "Open");
        Frames.Advance(run, 5);
    }

    // The core begins a delayed animation up to 10 ms early here: a plain 50 ms delay begins at 40.
    private void AssertBegins(Control child, double expected) => Assert.InRange(_begins[child], expected - 10, expected + 5);

    [AvaloniaFact]
    public void StaggerChildrenBeginsEachChildItsAmountAfterThePrevious()
    {
        var (a, b, c) = (Child(), Child(), Child());
        var parent = new StackPanel { Children = { a, b, c } };
        Motion.SetStaggerChildren(parent, 50);
        Show(parent);

        Open(parent);

        AssertBegins(a, 0);
        AssertBegins(b, 50);
        AssertBegins(c, 100);
        Assert.All([a, b, c], child => Assert.Equal(0, child.Opacity, 6));
    }

    [AvaloniaFact]
    public void StaggerFromLastReversesTheOrder()
    {
        var (a, b, c) = (Child(), Child(), Child());
        var parent = new StackPanel { Children = { a, b, c } };
        Motion.SetStaggerChildren(parent, 50);
        Motion.SetStaggerFrom(parent, StaggerFrom.Last);
        Show(parent);

        Open(parent);

        AssertBegins(a, 100);
        AssertBegins(b, 50);
        AssertBegins(c, 0);
    }

    [AvaloniaFact]
    public void TheStaggerFollowsWhereChildrenAreLaidOutRatherThanTreeOrder()
    {
        var (a, b, c) = (Child(), Child(), Child());
        Grid.SetRow(a, 2);
        Grid.SetRow(b, 1);
        var parent = new Grid { RowDefinitions = new("Auto,Auto,Auto"), Children = { a, b, c } };
        Motion.SetStaggerChildren(parent, 50);
        Show(parent);

        Open(parent);

        AssertBegins(c, 0);
        AssertBegins(b, 50);
        AssertBegins(a, 100);
    }

    [AvaloniaFact]
    public void DelayChildrenShiftsEveryChild()
    {
        var (a, b) = (Child(), Child());
        var parent = new StackPanel { Children = { a, b } };
        Motion.SetDelayChildren(parent, 100);
        Motion.SetStaggerChildren(parent, 50);
        Show(parent);

        Open(parent);

        AssertBegins(a, 100);
        AssertBegins(b, 150);
    }

    [AvaloniaFact]
    public void BeforeChildrenStartsTheChildrenWhenTheParentsOwnAnimationEnds()
    {
        var (a, b) = (Child(), Child());
        var parent = new StackPanel { Children = { a, b } };
        Motion.SetStates(parent, [new MotionState { When = "Open", Motion = Presto.Build().Duration(200).Ease(Eases.None).X(10) }]);
        Motion.SetOrder(parent, MotionOrder.BeforeChildren);
        Show(parent);

        Open(parent);

        AssertBegins(a, 200);
        AssertBegins(b, 200);
    }

    [AvaloniaFact]
    public void AfterChildrenStartsTheParentWhenTheLastChildEnds()
    {
        var (a, b) = (Child(), Child());
        var parent = new StackPanel { Children = { a, b } };
        Motion.SetStates(parent,
        [
            new MotionState { When = "Open", Motion = Presto.Build().Duration(100).Ease(Eases.None).X(10).OnBegin(_ => _begins[parent] = Frames.Now - _start) },
        ]);
        Motion.SetStaggerChildren(parent, 50);
        Motion.SetOrder(parent, MotionOrder.AfterChildren);
        Show(parent);

        Open(parent);

        AssertBegins(b, 50);
        AssertBegins(parent, 150);
    }

    [AvaloniaFact]
    public void AChildWithItsOwnStateIsLeftOutOfTheStagger()
    {
        var (a, b, c) = (Child(), Child(), Child());
        Motion.SetState(b, "Closed");
        var parent = new StackPanel { Children = { a, b, c } };
        Motion.SetStaggerChildren(parent, 50);
        Show(parent);

        Open(parent);

        AssertBegins(a, 0);
        AssertBegins(c, 50);
        Assert.False(_begins.ContainsKey(b));
    }

    [AvaloniaFact]
    public void ReturningMidStaggerNeverStartsTheWaitingChildrenAndReturnsTheOthersFromWhereTheyStand()
    {
        var (a, b, c) = (Child(), Child(), Child());
        var parent = new StackPanel { Children = { a, b, c } };
        Motion.SetState(parent, "Closed");
        Motion.SetStaggerChildren(parent, 50);
        Show(parent);

        Open(parent, 70);
        var (aStood, bStood) = (a.Opacity, b.Opacity);
        Assert.True(aStood < 0.5 && bStood < 1);
        Motion.SetState(parent, "Closed");
        Frames.Advance(5);
        Assert.InRange(a.Opacity, aStood, aStood + 0.2);

        Frames.Advance(400, 5);
        Assert.False(_begins.ContainsKey(c));
        Assert.All([a, b, c], child => Assert.Equal(1, child.Opacity, 6));
    }

    [AvaloniaFact]
    public void ClearingTheParentStateStaggersTheChildrenBackToRest()
    {
        var (a, b, c) = (Child(), Child(), Child());
        var parent = new StackPanel { Children = { a, b, c } };
        Motion.SetStaggerChildren(parent, 50);
        Show(parent);
        Open(parent);

        parent.ClearValue(Motion.StateProperty);
        Frames.Advance(30, 5);

        Assert.True(a.Opacity > 0);
        Assert.Equal(0, c.Opacity, 6);
        Frames.Advance(400, 5);
        Assert.All([a, b, c], child => Assert.Equal(1, child.Opacity, 6));
    }

    [AvaloniaFact]
    public void UnderReduceAllEveryChildLandsAtOnce()
    {
        Presto.Defaults.ReduceAll = true;
        var (a, b) = (Child(), Child());
        var parent = new StackPanel { Children = { a, b } };
        Motion.SetDelayChildren(parent, 100);
        Motion.SetStaggerChildren(parent, 50);
        Show(parent);

        Open(parent, 16);

        Assert.All([a, b], child => Assert.Equal(0, child.Opacity, 6));
    }
}
