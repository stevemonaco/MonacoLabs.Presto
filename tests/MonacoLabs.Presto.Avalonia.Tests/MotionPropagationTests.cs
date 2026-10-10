using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="Motion.StateProperty"/> set on an element drives the states of descendants that set none (GAPS-PLAN §7.2).</summary>
public sealed class MotionPropagationTests : BridgeTestBase
{
    private static AnimationBuilder Linear() => Presto.Build().Duration(100).Ease(Eases.None);

    private static double Get(Visual visual, string property) => (double)PrestoAvalonia.Get(visual, property)!;

    private static Border Follower()
    {
        var box = Box();
        Motion.SetStates(box,
        [
            new MotionState { When = "Open", Motion = Linear().Opacity(0.4) },
            new MotionState { When = "Closed", Motion = Linear().X(10) },
        ]);
        return box;
    }

    [AvaloniaFact]
    public void AParentStatePlaysTheChildrensStatesAndClearingItReturnsThemToRest()
    {
        var first = Follower();
        var second = Follower();
        var parent = new StackPanel { Children = { first, second } };
        Show(parent);

        Motion.SetState(parent, "Open");
        Frames.Advance(50, 10);
        Assert.InRange(first.Opacity, 0.6, 0.8);
        Frames.Advance(100, 10);
        Assert.Equal(0.4, first.Opacity, 6);
        Assert.Equal(0.4, second.Opacity, 6);

        parent.ClearValue(Motion.StateProperty);
        Frames.Advance(200, 10);
        Assert.Equal(1, first.Opacity, 6);
        Assert.Equal(1, second.Opacity, 6);
        Assert.Empty(Presto.GetAnimations(first));
    }

    [AvaloniaFact]
    public void AChildsOwnStateWinsAndItsChildrenFollowIt()
    {
        var grandchild = Follower();
        var child = Follower();
        child.Child = grandchild;
        var parent = new StackPanel { Children = { child } };
        Show(parent);
        Motion.SetState(child, "Closed");

        Motion.SetState(parent, "Open");
        Frames.Advance(200, 10);
        Assert.Equal(1, child.Opacity, 6);
        Assert.Equal(10, Get(child, "X"), 6);
        Assert.Equal(1, grandchild.Opacity, 6);
        Assert.Equal(10, Get(grandchild, "X"), 6);

        child.ClearValue(Motion.StateProperty);
        Frames.Advance(200, 10);
        Assert.Equal(0.4, child.Opacity, 6);
        Assert.Equal(0, Get(child, "X"), 6);
        Assert.Equal(0.4, grandchild.Opacity, 6);
        Assert.Equal(0, Get(grandchild, "X"), 6);
    }

    [AvaloniaFact]
    public void AGrandchildFollowsThroughAnElementWithoutMotion()
    {
        var grandchild = Follower();
        var parent = new StackPanel { Children = { new Border { Child = grandchild } } };
        Show(parent);

        Motion.SetState(parent, "Open");
        Frames.Advance(200, 10);
        Assert.Equal(0.4, grandchild.Opacity, 6);
    }

    [AvaloniaFact]
    public void AChildAddedUnderAnOpenParentArrivesSnapped()
    {
        var parent = new StackPanel();
        Show(parent);
        Motion.SetState(parent, "Open");

        var child = Follower();
        parent.Children.Add(child);

        Assert.Equal(0.4, child.Opacity, 6);
        Frames.Advance(16);
        Assert.Empty(Presto.GetAnimations(child));
        Assert.Equal(0.4, child.Opacity, 6);
    }

    [AvaloniaFact]
    public void DetachingTheParentRevertsTheChildren()
    {
        var child = Follower();
        var parent = new StackPanel { Children = { child } };
        var host = new StackPanel { Children = { parent } };
        Show(host);
        Motion.SetState(parent, "Open");
        Frames.Advance(50, 10);

        host.Children.Remove(parent);

        Assert.Empty(Presto.GetAnimations(child));
        Assert.Equal(1, child.Opacity, 6);
    }

    [AvaloniaFact]
    public void ItemsOfAnItemsControlFollowThroughTheirContainers()
    {
        var items = new List<Border>();
        var list = new ItemsControl
        {
            ItemsSource = new[] { "a", "b" },
            ItemTemplate = new FuncDataTemplate<string>((_, _) =>
            {
                var item = Follower();
                items.Add(item);
                return item;
            }),
            VerticalAlignment = VerticalAlignment.Top,
        };
        Show(list);

        Motion.SetState(list, "Open");
        Frames.Advance(200, 10);
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(0.4, item.Opacity, 6));
    }

    [AvaloniaFact]
    public void AChildsClassesAndPseudoClassesComeFromItself()
    {
        var child = new ToggleButton { Content = "child" };
        var parent = new ToggleButton { Content = child, IsChecked = true, Classes = { "selected" } };
        Motion.SetStates(child,
        [
            new MotionState { When = "Open.selected", Motion = Linear().Opacity(0.4) },
            new MotionState { When = "Open:checked", Motion = Linear().X(10) },
        ]);
        Show(parent);

        Motion.SetState(parent, "Open");
        Frames.Advance(200, 10);
        Assert.Equal(1, child.Opacity, 6);
        Assert.Equal(0, Get(child, "X"), 6);

        child.Classes.Add("selected");
        child.IsChecked = true;
        Frames.Advance(200, 10);
        Assert.Equal(0.4, child.Opacity, 6);
        Assert.Equal(10, Get(child, "X"), 6);
    }
}
