using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// <see cref="Animate.ItemLayoutProperty"/> (DECLARATIVE-LAYOUT-PLAN §5): every item container eases its layout moves,
/// a container realized or recycled lands in one frame, and scrolling a virtualizing list eases nothing.
/// </summary>
/// <remarks>As in <see cref="AnimateLayoutAttachedTests"/>, a held move is still drawn in its old slot the frame after layout moved it.</remarks>
public sealed class ItemLayoutTests : CompositorTestBase
{
    private const double RowHeight = 20;

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
        new Window { Content = list, Background = Brushes.White, Width = 100, Height = 200 }.Show();
        Tick();

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
        var list = Show(VirtualizedList(items, exit: Presto.Build().Opacity(to: 0).Duration(Held).Ease(Eases.None)));
        var start = Top(list);

        items.RemoveAt(1);
        Tick();

        Assert.Single(OverlayLayer.GetOverlayLayer(list)!.Children.OfType<Canvas>());
        Assert.Equal(start, Top(list));
    }

    private static T Show<T>(T list) where T : ItemsControl
    {
        Animate.SetItemLayout(list, HeldMove);
        new Window { Content = list, Background = Brushes.White, Width = 100, Height = 200 }.Show();
        Tick();
        return list;
    }

    private static Border Row(ItemsControl list, IBrush background) =>
        list.GetVisualDescendants().OfType<Border>().Single(row => row.Background == background && row.IsEffectivelyVisible);

    /// <summary>A 100-high list box whose containers are exactly as high as their rows.</summary>
    private static ListBox VirtualizedList(ObservableCollection<string> items, Func<string, double>? height = null, AnimationBuilder? exit = null) => new()
    {
        Height = 100,
        VerticalAlignment = VerticalAlignment.Top,
        ItemsSource = items,
        ItemTemplate = Rows(height, exit),
        Styles =
        {
            new Style(x => x.OfType<ListBoxItem>())
            {
                Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(0)), new Setter(Layoutable.MinHeightProperty, 0d) },
            },
        },
    };

    /// <summary>Blue 40-wide rows, but red for the item "red" and a lime 60-wide one for "green".</summary>
    private static FuncDataTemplate<string> Rows(Func<string, double>? height = null, AnimationBuilder? exit = null) => new((item, _) =>
    {
        var row = new Border
        {
            Width = item == "green" ? 60 : 40,
            Height = item is null or "red" ? RowHeight : height?.Invoke(item) ?? RowHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = item switch { "red" => Brushes.Red, "green" => Brushes.Lime, _ => Brushes.Blue },
        };
        Presence.SetExit(row, exit);
        return row;
    }, supportsRecycling: false);
}
