using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Covers <see cref="Reorder"/> through headless mouse input over 20-high rows "a", "red", "green", "d", "e"; theories run a
/// non-virtualizing <see cref="ItemsControl"/> and a virtualizing <see cref="ListBox"/>. GAPS-PLAN §6.2 has the design.
/// </summary>
public sealed class ReorderTests : CompositorTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void DraggingAcrossSeveralRowsMovesOneStepPerRowInOrderDownAndUp(bool virtualized)
    {
        var (list, items, moves) = Show(virtualized);
        var window = WindowOf(list);

        window.MouseDown(OnRed, MouseButton.Left);
        window.MouseMove(OnRed + new Point(0, 50));
        Assert.Equal([(1, 2), (2, 3)], moves);

        window.MouseMove(OnRed + new Point(0, -15));
        Assert.Equal([(1, 2), (2, 3), (3, 2), (2, 1)], moves);
        Assert.Equal(["a", "red", "green", "d", "e"], items);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheDraggedItemIsDrawnUnderThePointerAcrossEveryMove(bool virtualized)
    {
        var (list, _, moves) = Show(virtualized, itemLayout: true);
        var window = WindowOf(list);
        window.MouseDown(OnRed, MouseButton.Left);
        window.MouseMove(OnRed + new Point(0, 5));
        Frames.Advance(1000);
        Tick();
        var start = Top(list);

        for (var dy = 10; dy <= 60; dy += 5)
        {
            window.MouseMove(OnRed + new Point(0, dy));
            Tick();
            Assert.Equal(start + dy - 5, Top(list));
        }
        Assert.Equal([(1, 2), (2, 3)], moves);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void NeighborsEaseWithItemLayoutAndSnapWithout(bool itemLayout)
    {
        var (list, _, moves) = Show(virtualized: false, itemLayout);
        var window = WindowOf(list);

        window.MouseDown(OnRed, MouseButton.Left);
        window.MouseMove(OnRed + new Point(0, 35));
        Tick();

        Assert.Equal([(1, 2)], moves);
        Assert.Equal(20, SlotOf(Row(list, Brushes.Lime)).Y);
        Assert.Equal(itemLayout ? 40 : 20, Extent(list, Colors.Lime)!.Value.Y);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReleaseSpringsTheItemIntoItsSlotAndLeavesNothingBehind(bool virtualized)
    {
        var (list, items, _) = Show(virtualized);
        var window = WindowOf(list);
        window.MouseDown(OnRed, MouseButton.Left);
        window.MouseMove(OnRed + new Point(0, 35));
        var dragged = list.ContainerFromItem("red")!;
        Assert.NotNull(dragged.RenderTransform);
        Assert.NotNull(dragged.Effect);

        window.MouseUp(OnRed + new Point(0, 35), MouseButton.Left);
        Assert.True(Presto.Engine.IsRunning);
        Land();

        var container = list.ContainerFromItem("red")!;
        Assert.Equal(["a", "green", "red", "d", "e"], items);
        Assert.Equal(SlotOf(Row(list, Brushes.Red)), Box(list));
        Assert.Equal(0, container.ZIndex);
        Assert.Null(container.Effect);
        Assert.True(container.RenderTransform is null or { Value.IsIdentity: true });
    }

    [AvaloniaFact]
    public void ALiftSetOnTheListPlaysInPlaceOfTheDefaultAndIsRevertedOnRelease()
    {
        var (list, _, _) = Show(virtualized: false);
        Reorder.SetLift(list, Presto.Build().Opacity(to: 0.5).Duration(100).Ease(Eases.None));
        var window = WindowOf(list);
        window.MouseDown(OnRed, MouseButton.Left);
        window.MouseMove(OnRed + new Point(0, 35));
        Frames.Advance(1000);
        var dragged = list.ContainerFromItem("red")!;

        Assert.Equal(0.5, dragged.Opacity);
        Assert.Null(dragged.Effect);
        Assert.Equal(1, dragged.RenderTransform!.Value.M11);
        Assert.Equal(1, dragged.RenderTransform.Value.M22);

        window.MouseUp(OnRed + new Point(0, 35), MouseButton.Left);
        Land();

        Assert.Equal(1, list.ContainerFromItem("red")!.Opacity);
    }

    [AvaloniaFact]
    public void CancelingMovingLeavesTheCollectionUntouched()
    {
        var (list, items, moves) = Show(virtualized: false);
        var window = WindowOf(list);
        var raised = new List<(object, int, int)>();
        Reorder.AddMovingHandler(list, (_, e) =>
        {
            raised.Add((e.Item, e.OldIndex, e.NewIndex));
            e.Cancel = true;
        });

        window.MouseDown(OnRed, MouseButton.Left);
        window.MouseMove(OnRed + new Point(0, 50));
        window.MouseUp(OnRed + new Point(0, 50), MouseButton.Left);
        Land();

        Assert.Equal(("red", 1, 2), raised[0]);
        Assert.Empty(moves);
        Assert.Equal(["a", "red", "green", "d", "e"], items);
        Assert.Equal(SlotOf(Row(list, Brushes.Red)), Box(list));
    }

    [AvaloniaFact]
    public void ACommandReceivesEachStepAndOwnsTheMove()
    {
        var (list, items, moves) = Show(virtualized: false);
        var window = WindowOf(list);
        var steps = new List<(object, int, int)>();
        Reorder.SetCommand(list, new Command(e =>
        {
            steps.Add((e.Item, e.OldIndex, e.NewIndex));
            if (steps.Count == 1) items.Move(e.OldIndex, e.NewIndex);
        }));

        window.MouseDown(OnRed, MouseButton.Left);
        window.MouseMove(OnRed + new Point(0, 50));

        Assert.Equal([("red", 1, 2), ("red", 2, 3)], steps);
        Assert.Equal([(1, 2)], moves);
    }

    [AvaloniaFact]
    public void AContainerWithAHandleIsGrabbedOnlyByIt()
    {
        var (list, _, moves) = Show(virtualized: false, handle: true);
        var window = WindowOf(list);

        window.MouseDown(new Point(30, 30), MouseButton.Left);
        window.MouseMove(new Point(30, 80));
        window.MouseUp(new Point(30, 80), MouseButton.Left);
        Assert.Empty(moves);

        window.MouseDown(new Point(5, 30), MouseButton.Left);
        window.MouseMove(new Point(5, 80));
        Assert.Equal([(1, 2), (2, 3)], moves);
    }

    [AvaloniaFact]
    public void DisablingMidDragRevertsTheDragAndStopsListening()
    {
        var (list, _, moves) = Show(virtualized: false, itemLayout: true);
        var window = WindowOf(list);
        window.MouseDown(OnRed, MouseButton.Left);
        window.MouseMove(OnRed + new Point(0, 35));
        var container = list.ContainerFromItem("red")!;

        Reorder.SetEnabled(list, false);
        window.MouseMove(OnRed + new Point(0, 60));
        Tick();

        Assert.Equal([(1, 2)], moves);
        Assert.Equal(0, container.ZIndex);
        Assert.Null(container.Effect);
        Assert.True(container.RenderTransform is null or { Value.IsIdentity: true });
        Assert.Equal(SlotOf(Row(list, Brushes.Red)), Box(list));
    }

    [AvaloniaFact]
    public void AHorizontalListReordersAlongX()
    {
        ObservableCollection<string> items = ["a", "red", "green", "d", "e"];
        var list = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = Rows(),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation.Horizontal }),
            VerticalAlignment = VerticalAlignment.Top,
        };
        Reorder.SetEnabled(list, true);
        Reorder.SetAxis(list, Orientation.Horizontal);
        var window = new Window { Content = list, Background = Brushes.White, Width = 400, Height = 100 };
        window.Show();
        Tick();

        window.MouseDown(new Point(50, 10), MouseButton.Left);
        window.MouseMove(new Point(50, 40));
        Assert.Equal(["a", "red", "green", "d", "e"], items);

        window.MouseMove(new Point(115, 10));
        Assert.Equal(["a", "green", "red", "d", "e"], items);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void EscapeMidDragMovesTheItemBackStepByStepAndLandsItInItsOriginalSlot(bool virtualized)
    {
        var (list, items, moves) = Show(virtualized);
        var window = WindowOf(list);
        var slot = SlotOf(Row(list, Brushes.Red));
        window.MouseDown(OnRed, MouseButton.Left);
        window.MouseMove(OnRed + new Point(0, 50));

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        window.MouseMove(OnRed + new Point(0, 70));
        window.MouseUp(OnRed + new Point(0, 70), MouseButton.Left);
        Land();

        Assert.Equal([(1, 2), (2, 3), (3, 2), (2, 1)], moves);
        Assert.Equal(["a", "red", "green", "d", "e"], items);
        Assert.Equal(slot, SlotOf(Row(list, Brushes.Red)));
        Assert.Equal(slot, Box(list));
        Assert.Null(list.ContainerFromItem("red")!.Effect);
    }

    [AvaloniaFact]
    public void AltArrowsMoveTheFocusedItemOneStepKeepItFocusedAndStopAtTheEnds()
    {
        var (list, items, moves) = Show(virtualized: true);
        var window = WindowOf(list);
        list.ContainerFromItem("red")!.Focus();

        void Press(Key key, PhysicalKey physical) => window.KeyPress(key, RawInputModifiers.Alt, physical, null);
        Press(Key.Down, PhysicalKey.ArrowDown);
        Assert.Equal(["a", "green", "red", "d", "e"], items);
        Assert.True(list.ContainerFromItem("red")!.IsFocused);

        Press(Key.Up, PhysicalKey.ArrowUp);
        Press(Key.Up, PhysicalKey.ArrowUp);
        Press(Key.Up, PhysicalKey.ArrowUp);

        Assert.Equal([(1, 2), (2, 1), (1, 0)], moves);
        Assert.Equal(["red", "a", "green", "d", "e"], items);
        Assert.True(list.ContainerFromItem("red")!.IsFocused);
    }

    [AvaloniaFact]
    public void AMovedItemStaysSelected()
    {
        var (list, items, _) = Show(virtualized: true);
        var listBox = Assert.IsType<ListBox>(list);
        listBox.SelectedItem = "red";
        list.ContainerFromItem("red")!.Focus();

        WindowOf(list).KeyPress(Key.Down, RawInputModifiers.Alt, PhysicalKey.ArrowDown, null);

        Assert.Equal(["a", "green", "red", "d", "e"], items);
        Assert.Equal("red", listBox.SelectedItem);
    }

    [AvaloniaFact]
    public void HoldingThePointerInTheBottomEdgeBandScrollsTheListAndKeepsMovingTheItemDownInOrder()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 100).Select(i => i == 1 ? "red" : $"row{i}"));
        var moves = new List<(int, int)>();
        items.CollectionChanged += (_, e) => moves.Add((e.OldStartingIndex, e.NewStartingIndex));
        var list = ShowInWindow(VirtualizedList(items));
        Reorder.SetEnabled(list, true);
        var window = WindowOf(list);
        window.MouseDown(OnRed, MouseButton.Left);
        window.MouseMove(new Point(10, 95));
        Tick();
        var top = Top(list);

        for (var i = 0; i < 20; i++) Frames.Advance(16);
        Tick();

        Assert.True(list.Scroll!.Offset.Y > 200);
        Assert.True(moves.Count > 10);
        Assert.Equal(moves.Select((_, i) => (i + 1, i + 2)), moves);
        Assert.Equal(top, Top(list));
    }

    [AvaloniaFact]
    public void AVirtualizedDragFarPastTheViewportKeepsTheSameContainerAndLandsCleanly()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 100).Select(i => i == 0 ? "red" : $"row{i}"));
        var list = ShowInWindow(VirtualizedList(items));
        Reorder.SetEnabled(list, true);
        var window = WindowOf(list);
        var container = list.ContainerFromItem("red")!;
        window.MouseDown(new Point(10, 10), MouseButton.Left);
        window.MouseMove(new Point(10, 99));

        while (list.Scroll!.Offset.Y < 1130)
        {
            Frames.Advance(16);
            Assert.Same(container, list.ContainerFromItem("red"));
        }
        // The item's top follows the pointer 10 below the grab, so this puts it over row 60's slot at 1200.
        var at = new Point(10, 1210 - list.Scroll.Offset.Y);
        window.MouseMove(at);
        window.MouseUp(at, MouseButton.Left);
        Land();

        Assert.Equal(60, items.IndexOf("red"));
        Assert.Same(container, list.ContainerFromItem("red"));
        Assert.Equal(0, container.ZIndex);
        Assert.Null(container.Effect);
        Assert.True(container.RenderTransform is null or { Value.IsIdentity: true });
    }

    // A single frame after a long settle still shows where the drag left the item; the next one draws the landing.
    private void Land()
    {
        Settle(() => !Presto.Engine.IsRunning);
        Tick();
        Tick();
    }

    private static (ItemsControl List, ObservableCollection<string> Items, List<(int, int)> Moves) Show(bool virtualized, bool itemLayout = false, bool handle = false)
    {
        var (list, items) = ReorderList(virtualized, itemLayout, template: handle ? Handled() : null);
        var moves = new List<(int, int)>();
        items.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Move) moves.Add((e.OldStartingIndex, e.NewStartingIndex));
        };
        Reorder.SetEnabled(list, true);
        return (list, items, moves);
    }

    private static FuncDataTemplate<string> Handled()
    {
        var rows = Rows();
        return new FuncDataTemplate<string>((item, scope) =>
        {
            var grip = new Border { Width = 10, Height = RowHeight, Background = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Left };
            Reorder.SetHandle(grip, true);
            return new Panel { Children = { rows.Build(item)!, grip } };
        }, supportsRecycling: false);
    }

    private sealed class Command(Action<ReorderMovingEventArgs> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute((ReorderMovingEventArgs)parameter!);
    }
}
