using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Animate.ItemLayoutProperty"/> (DECLARATIVE-LAYOUT-PLAN §5): every item container eases its layout moves,
/// a container realized or recycled lands in one frame, and scrolling a virtualizing list eases nothing.
/// </summary>
/// <remarks>As in <see cref="AnimateLayoutAttachedTests"/>, a held move is still drawn in its old slot the frame after layout moved it.</remarks>
public sealed class ItemLayoutTests : CompositorTestBase
{
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    public void InsertingAnItemEasesTheContainersBelowAndTheNewOneDoesNotFly(int index)
    {
        var items = new ObservableCollection<string> { "a", "red", "c" };
        var list = Show(new ItemsControl { ItemsSource = items, ItemTemplate = Rows(), VerticalAlignment = VerticalAlignment.Top });
        var start = Top(list);

        items.Insert(index, "green");
        Tick();

        Assert.Equal(start, Top(list));
        // The row held in the new one's slot hides all but its extra width.
        var slot = SlotOf(Row(list, Brushes.Lime));
        Assert.Equal(new Rect(40, slot.Y, 20, slot.Height), Extent(list, Colors.Lime));
    }

    [AvaloniaFact]
    public void ClearingItRevertsEveryArmedContainer()
    {
        var items = new ObservableCollection<string> { "a", "red" };
        var list = Show(new ItemsControl { ItemsSource = items, ItemTemplate = Rows(), VerticalAlignment = VerticalAlignment.Top });
        items.Insert(0, "c");
        Tick();
        Assert.NotEqual(SlotOf(Row(list, Brushes.Red)), Box(list));

        Animate.SetItemLayout(list, null);
        Tick();

        Assert.Equal(SlotOf(Row(list, Brushes.Red)), Box(list));
        Assert.All(list.GetRealizedContainers(), container => Assert.Null(LayoutTransition.Live(container)));
    }

    [AvaloniaFact]
    public void ScrollingAVirtualizedListEasesNothing()
    {
        // Scrolling back up uneven rows after a jump makes the panel correct its estimates, repositioning realized rows.
        var items = new ObservableCollection<string>(Enumerable.Range(0, 80).Select(i => $"row{i}"));
        var list = Show(VirtualizedList(items, item => int.Parse(item[3..]) % 3 == 0 ? 90 : 10));

        for (var y = 2000; y >= 0; y -= 150)
        {
            list.Scroll!.Offset = new Vector(0, y);
            Tick();
            Assert.All(list.GetRealizedContainers(), container => Assert.True(LayoutTransition.Live(container) is { Completed: true }, $"a row eased at offset {y}"));
        }
    }

    [AvaloniaFact]
    public void ScrollingEasesNothingWhenTheContainersSetTheLayoutPropertyThemselves()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 80).Select(i => $"row{i}"));
        var list = VirtualizedList(items, item => int.Parse(item[3..]) % 3 == 0 ? 90 : 10);
        list.Styles.Add(new Style(x => x.OfType<ListBoxItem>()) { Setters = { new Setter(Animate.LayoutProperty, HeldMove) } });
        ShowInWindow(list);

        for (var y = 2000; y >= 0; y -= 150)
        {
            list.Scroll!.Offset = new Vector(0, y);
            Tick();
            Assert.All(list.GetRealizedContainers(), container => Assert.True(LayoutTransition.Live(container) is { Completed: true }, $"a row eased at offset {y}"));
        }
    }

    [AvaloniaFact]
    public void ARecycledContainerLandsAtItsNewIndexInOneFrame()
    {
        // Scrolling from ten short rows to tall ones leaves spare containers in the recycle pool.
        var items = new ObservableCollection<string>(Enumerable.Range(0, 50).Select(i => $"row{i}"));
        var list = Show(VirtualizedList(items, item => int.Parse(item[3..]) < 10 ? 10 : 50));
        var shortRows = list.GetRealizedContainers().ToList();
        for (var y = 50; y <= 200; y += 50)
        {
            list.Scroll!.Offset = new Vector(0, y);
            Tick();
        }
        Tick();

        items.Insert(13, "red");
        Tick();

        Assert.Contains(list.ContainerFromItem("red"), shortRows);
        Assert.Equal(SlotOf(Row(list, Brushes.Red)), Box(list));
    }

    [AvaloniaFact]
    public void RemovingARowEasesTheRowsBelowWhileItsExitPlays()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 50).Select(i => i == 3 ? "red" : $"row{i}"));
        var exit = Presto.Build().Opacity(to: 0).Duration(Held).Ease(Eases.None);
        var list = Show(VirtualizedList(items, decorate: row => Presence.SetExit(row, exit)));
        var start = Top(list);

        items.RemoveAt(1);
        Tick();

        Assert.Single(OverlayLayer.GetOverlayLayer(list)!.Children.OfType<Canvas>());
        Assert.Equal(start, Top(list));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArmingAContainerThroughBothItsOwnLayoutAndItsListsItemLayoutThrows(bool itemLayoutFirst)
    {
        var items = new ObservableCollection<string> { "a", "red" };
        var list = new ItemsControl { ItemsSource = items, ItemTemplate = Rows(), VerticalAlignment = VerticalAlignment.Top };
        if (itemLayoutFirst) Show(list);
        else ShowInWindow(list);
        var container = list.ContainerFromIndex(0)!;

        if (itemLayoutFirst) Assert.Throws<InvalidOperationException>(() => Animate.SetLayout(container, HeldMove));
        else
        {
            Animate.SetLayout(container, HeldMove);
            Assert.Throws<InvalidOperationException>(() => Animate.SetItemLayout(list, HeldMove));
        }
    }

    [AvaloniaFact]
    public void AClearedContainerIsRevertedAndReleased()
    {
        var items = new ObservableCollection<string> { "a", "red", "c" };
        var list = Show(new ItemsControl { ItemsSource = items, ItemTemplate = Rows(), VerticalAlignment = VerticalAlignment.Top });
        var container = list.ContainerFromItem("red")!;
        Assert.NotNull(Animate.ItemArmOf(container));

        items.Remove("red");
        Tick();

        Assert.Null(Animate.ItemArmOf(container));
        Assert.Null(LayoutTransition.Live(container));
    }

    [AvaloniaFact]
    public void ABuilderALayoutTransitionCannotHonorThrowsWhenSetOnAnEmptyList() =>
        Assert.Throws<NotSupportedException>(() => Animate.SetItemLayout(new ItemsControl(), Presto.Build().X(10)));

    [AvaloniaFact]
    public void ChangingTheListsLayoutModeRearmsEveryContainer()
    {
        var items = new ObservableCollection<string> { "a", "red" };
        var list = Show(new ItemsControl { ItemsSource = items, ItemTemplate = Rows(), VerticalAlignment = VerticalAlignment.Top });
        var red = Row(list, Brushes.Red);
        red.Height = 30;
        Tick();
        Assert.Equal(30, Box(list).Height);

        Animate.SetLayoutMode(list, LayoutAnimateMode.PositionAndSize);
        red.Height = 40;
        Tick();

        Assert.Equal(40, red.Bounds.Height);
        Assert.Equal(30, Box(list).Height);
    }

    private static T Show<T>(T list) where T : ItemsControl
    {
        Animate.SetItemLayout(list, HeldMove);
        return ShowInWindow(list);
    }
}
