using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Animate.ItemGroupProperty"/>: an item removed from one grouped <see cref="Animate.ItemLayoutProperty"/> list and
/// added to one of the same group within the layout pass flies from where it was drawn into its new slot, and plays neither
/// its <see cref="Presence"/> exit nor its entrance.
/// </summary>
public sealed class ItemTransferTests : CompositorTestBase
{
    private static readonly Rect OldSlot = new(0, RowHeight, 40, RowHeight);

    private int _entered;
    private int _exited;

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARowMovedToAnotherListFliesWithoutExitOrEntranceAndItsArmResumesAfterLanding(bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var (open, done, openItems, doneItems) = Lists("tasks", "tasks", decorate: Lifecycle);

        openItems.Remove("red");
        doneItems.Add("red");
        Tick();
        Assert.Empty(Ghosts(open));
        Frames.Advance(200, 16);
        Tick();

        Assert.Equal(OldSlot, Box(open));
        Frames.Advance(Held + 1);
        Assert.True(Pump(() => Box(done) == SlotOf(Row(done, Brushes.Red)), rounds: 8), "the flight did not land");
        Assert.Equal((0, 0), (_entered, _exited));

        var start = Top(done);
        doneItems.Insert(0, "c");
        Tick();
        Assert.Equal(start, Top(done));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovingAndReinsertingAnItemInOneGroupedListFliesWithoutExitOrEntrance(bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var (open, _, openItems, _) = Lists("tasks", "tasks", decorate: Lifecycle);

        openItems.Remove("red");
        openItems.Add("red");
        Tick();
        Assert.Empty(Ghosts(open));
        Frames.Advance(200, 16);
        Tick();

        Assert.NotEqual(OldSlot, SlotOf(Row(open, Brushes.Red)));
        Assert.Equal(OldSlot, Box(open));
        Assert.Equal((0, 0), (_entered, _exited));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnAddBeforeItsRemovalDoesNotFlyAndExitsAndEnters(bool reduced) => AssertNoTransfer("tasks", "tasks", reduced, addFirst: true);

    [AvaloniaTheory]
    [InlineData(null, null, false)]
    [InlineData("tasks", null, false)]
    [InlineData("tasks", "archive", false)]
    [InlineData(null, null, true)]
    [InlineData("tasks", null, true)]
    [InlineData("tasks", "archive", true)]
    public void ListsWithoutASharedGroupDoNotFlyAndExitAndEnter(string? openGroup, string? doneGroup, bool reduced) =>
        AssertNoTransfer(openGroup, doneGroup, reduced, addFirst: false);

    [AvaloniaFact]
    public void ACollapsedDestinationDeclinesTheTransfer()
    {
        var (_, done, openItems, doneItems) = Lists("tasks", "tasks");
        var collapsed = (Control)done.Parent!;
        collapsed.IsVisible = false;
        Tick();

        openItems.Remove("red");
        doneItems.Add("red");
        Tick();

        Assert.False(LayoutTransition.Live(done.ContainerFromItem("red")!) is { Once: true });
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheFlightLandsAtOnce()
    {
        Presto.Defaults.ReducedMotion = true;
        var (open, done, openItems, doneItems) = Lists("tasks", "tasks", Recipes.Reflow(Held, ease: Eases.None));

        openItems.Remove("red");
        doneItems.Add("red");
        Tick();

        Assert.Equal(SlotOf(Row(done, Brushes.Red)), Box(open));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACollapsedDestinationLetsTheLeavingContainerExitAndTheArrivingOneEnter(bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var (open, done, openItems, doneItems) = Lists("tasks", "tasks");
        // A collapsed list builds no content for its containers, so only a container's own lifecycle shows the decline.
        foreach (var list in new[] { open, done })
        {
            list.Styles.Add(new Style(x => x.OfType<ContentPresenter>())
            {
                Setters = { new Setter(Presence.EnterProperty, EnterBuilder), new Setter(Presence.ExitProperty, ExitBuilder) },
            });
        }
        ((Control)done.Parent!).IsVisible = false;
        Tick();

        openItems.Remove("red");
        doneItems.Add("red");
        Tick();
        Assert.Single(Ghosts(open));
        Frames.Advance(200, 16);

        Assert.Equal((1, 1), (_entered, _exited));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMoveInAGroupedListIsNoTransferAndPlaysNothing(bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var (_, _, openItems, _) = Lists("tasks", "tasks", decorate: Lifecycle);

        openItems.Move(1, 2);
        Tick();
        Frames.Advance(200, 16);

        Assert.Equal((0, 0), (_entered, _exited));
    }

    private void AssertNoTransfer(string? openGroup, string? doneGroup, bool reduced, bool addFirst)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var (open, done, openItems, doneItems) = Lists(openGroup, doneGroup, decorate: Lifecycle);

        if (addFirst) doneItems.Add("red");
        openItems.Remove("red");
        if (!addFirst) doneItems.Add("red");
        Tick();
        Assert.False(LayoutTransition.Live(done.ContainerFromItem("red")!) is { Once: true });
        Assert.Single(Ghosts(open));
        Frames.Advance(200, 16);
        Tick();

        Assert.Equal(SlotOf(Row(done, Brushes.Red)), Box(open));
        Assert.Equal((1, 1), (_entered, _exited));
    }

    private AnimationBuilder EnterBuilder => Presto.Build().Opacity(from: 0, to: 1).Duration(100).OnBegin(_ => _entered++);

    private AnimationBuilder ExitBuilder => Presto.Build().Opacity(to: 0).Duration(100).OnBegin(_ => _exited++);

    private void Lifecycle(Border row)
    {
        Presence.SetEnter(row, EnterBuilder);
        Presence.SetExit(row, ExitBuilder);
    }

    /// <summary>Shows an open list of "a", "red", "c" above a done list of "d", "e", each in its own border, with the given groups.</summary>
    private static (ItemsControl Open, ItemsControl Done, ObservableCollection<string> OpenItems, ObservableCollection<string> DoneItems) Lists(
        string? openGroup, string? doneGroup, AnimationBuilder? layout = null, Action<Border>? decorate = null)
    {
        ObservableCollection<string> openItems = ["a", "red", "c"];
        ObservableCollection<string> doneItems = ["d", "e"];
        var open = List(openItems, openGroup, layout, decorate);
        var done = List(doneItems, doneGroup, layout, decorate);
        ShowInWindow(new StackPanel { Children = { new Border { Child = open }, new Border { Child = done } } });
        return (open, done, openItems, doneItems);
    }

    private static ItemsControl List(ObservableCollection<string> items, string? group, AnimationBuilder? layout, Action<Border>? decorate)
    {
        var list = new ItemsControl { ItemsSource = items, ItemTemplate = Rows(decorate: decorate), VerticalAlignment = VerticalAlignment.Top, ClipToBounds = false };
        Animate.SetItemLayout(list, layout ?? HeldMove);
        Animate.SetItemGroup(list, group);
        return list;
    }
}
