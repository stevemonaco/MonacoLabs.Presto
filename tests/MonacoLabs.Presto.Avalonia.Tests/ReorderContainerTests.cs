using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Pins how Avalonia 12.1.2 and the list plumbing treat <see cref="ObservableCollection{T}.Move"/> and a held container,
/// which GAPS-PLAN §6.2 builds <c>Reorder</c> on. Theories run a non-virtualizing <see cref="ItemsControl"/> and a
/// virtualizing <see cref="ListBox"/>; the item "red" is the one moved.
/// </summary>
public sealed class ReorderContainerTests : CompositorTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMoveClearsAndPreparesTheMovedItemsContainerAndOnlyAVirtualizingListKeepsItsInstance(bool virtualized)
    {
        var (list, items) = ReorderList(virtualized);
        var before = items.ToDictionary(item => item, item => list.ContainerFromItem(item));
        var events = new List<string>();
        list.ContainerClearing += (_, e) => events.Add(ReferenceEquals(e.Container, before["red"]) ? "clearing red" : "clearing");
        list.ContainerPrepared += (_, e) => events.Add($"prepared {e.Index}");

        items.Move(1, 3);
        Tick();

        Assert.Equal(["clearing red", "prepared 3"], events);
        Assert.Equal(virtualized, ReferenceEquals(before["red"], list.ContainerFromItem("red")));
        Assert.All(items.Where(item => item != "red"), item => Assert.Same(before[item], list.ContainerFromItem(item)));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMoveEasesTheNeighborsAndLandsTheMovedContainerInOneFrame(bool virtualized)
    {
        var (list, items) = ReorderList(virtualized, itemLayout: true);
        var green = Extent(list, Colors.Lime);

        items.Move(1, 3);
        Tick();

        Assert.Equal(SlotOf(Row(list, Brushes.Red)), Box(list));
        Assert.Equal(green, Extent(list, Colors.Lime));
        Assert.NotEqual(green!.Value.Y, SlotOf(Row(list, Brushes.Lime)).Y);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMovePlaysNeitherTheEnterNorTheExitOfTheMovedRow(bool virtualized)
    {
        var (list, items) = ReorderList(virtualized, decorate: FadeInAndOut);

        items.Move(1, 3);
        Tick();

        Assert.Empty(Ghosts(list));
        Assert.Equal(1, Row(list, Brushes.Red).Opacity);
        Assert.Equal(SlotOf(Row(list, Brushes.Red)), Box(list));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMoveTakesThePointerCaptureFromTheMovedContainerButNotFromTheList(bool virtualized)
    {
        var (list, items) = ReorderList(virtualized);
        var window = WindowOf(list);
        var container = list.ContainerFromItem("red")!;
        foreach (var (holder, keeps) in new (Control, bool)[] { (container, false), (list, true) })
        {
            IPointer? pointer = null;
            void Capture(object? sender, PointerPressedEventArgs e) => (pointer = e.Pointer).Capture(holder);
            list.AddHandler(InputElement.PointerPressedEvent, Capture, handledEventsToo: true);
            window.MouseDown(OnRed, MouseButton.Left);
            Assert.Same(holder, pointer!.Captured);

            items.Move(items.IndexOf("red"), items.IndexOf("red") == 1 ? 3 : 1);
            Tick();

            Assert.Equal(keeps, ReferenceEquals(holder, pointer.Captured));
            window.MouseUp(OnRed, MouseButton.Left);
            list.RemoveHandler(InputElement.PointerPressedEvent, Capture);
        }
    }

    [AvaloniaFact]
    public void AMoveTakesFocusFromTheMovedContainer()
    {
        var (list, items) = ReorderList(virtualized: true);
        var container = list.ContainerFromItem("red")!;
        container.Focus();

        items.Move(1, 3);
        Tick();

        Assert.Same(container, list.ContainerFromItem("red"));
        Assert.False(container.IsFocused);
    }

    [AvaloniaFact]
    public void AFocusedContainerStaysRealizedWhileItsListScrollsItFarAway()
    {
        var list = ShowLong();
        var container = list.ContainerFromItem("red")!;
        container.Focus();

        list.Scroll!.Offset = new Vector(0, 600);
        Tick();

        Assert.Same(container, list.ContainerFromItem("red"));
        Assert.Equal("red", container.DataContext);
    }

    [AvaloniaFact]
    public void AContainerHoldingThePointerCaptureStaysRealizedWhileItsListScrollsItFarAway()
    {
        var list = ShowLong(focusable: false);
        var container = list.ContainerFromItem("red")!;
        IPointer? pointer = null;
        container.AddHandler(InputElement.PointerPressedEvent, (_, e) => (pointer = e.Pointer).Capture(container), handledEventsToo: true);
        WindowOf(list).MouseDown(OnRed, MouseButton.Left);

        list.Scroll!.Offset = new Vector(0, 600);
        Tick();

        Assert.False(container.IsFocused);
        Assert.Same(container, pointer!.Captured);
        Assert.Same(container, list.ContainerFromItem("red"));
    }

    [AvaloniaFact]
    public void AContainerHoldingNeitherIsRecycledWhenItsListScrollsItFarAway()
    {
        var list = ShowLong();
        var container = list.ContainerFromItem("red")!;

        list.Scroll!.Offset = new Vector(0, 600);
        Tick();

        Assert.Null(list.ContainerFromItem("red"));
        Assert.NotEqual("red", container.DataContext);
    }

    [AvaloniaFact]
    public void ADraggableOnAListBoxItemNeverGrabsBecauseTheItemHandlesThePress()
    {
        var (list, _) = ReorderList(virtualized: true);
        var window = WindowOf(list);
        var draggable = PrestoAvalonia.CreateDraggable(list.ContainerFromItem("red")!);

        window.MouseDown(OnRed, MouseButton.Left);
        window.MouseMove(OnRed + new Point(0, 30));
        Frames.Advance(16);

        Assert.Equal(0d, draggable.Y);
    }

    [AvaloniaFact]
    public void ADraggablesTranslationAddsToTheFlipThatIsEasingTheSameContainer()
    {
        var (list, items) = ReorderList(virtualized: false, itemLayout: true);
        var container = list.ContainerFromItem("red")!;
        var draggable = PrestoAvalonia.CreateDraggable(container);
        var held = Box(list);
        items.Move(2, 0);
        Tick();
        Assert.Equal(held, Box(list));

        draggable.X = 10;
        draggable.Y = 15;
        Frames.Advance(16);
        Tick();

        Assert.Equal(held.Translate(new Vector(10, 15)), Box(list));
        Assert.Equal(new Point(10, 2 * RowHeight + 15), SlotOf(container).Position);
    }

    private static ListBox ShowLong(bool focusable = true)
    {
        var list = VirtualizedList(new ObservableCollection<string>(Enumerable.Range(0, 50).Select(i => i == 1 ? "red" : $"row{i}")));
        if (!focusable) list.Styles.Add(new Style(x => x.OfType<ListBoxItem>()) { Setters = { new Setter(InputElement.FocusableProperty, false) } });
        return ShowInWindow(list);
    }

    private static void FadeInAndOut(Border row)
    {
        Presence.SetEnter(row, Presto.Build().Opacity(from: 0).Duration(Held).Ease(Eases.None));
        Presence.SetExit(row, Presto.Build().Opacity(to: 0).Duration(Held).Ease(Eases.None));
    }
}
