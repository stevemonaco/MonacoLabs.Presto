using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>The selection indicator: where it sits, when it glides, when it snaps and when it hides.</summary>
public sealed class SelectionIndicatorTests : BridgeTestBase
{
    [AvaloniaFact]
    public void ItGlidesToANewSelectionOverTime()
    {
        var list = ShowList(Orientation.Vertical);
        var from = Indicator(list).Bounds;

        list.SelectedIndex = 3;
        Tick(list, 100);
        var mid = Indicator(list).Bounds;
        Tick(list, 1000);

        var to = Slot(list, 3);
        Assert.Equal(Slot(list, 0), from);
        Assert.InRange(mid.Y, from.Y + 1, to.Y - 1);
        Assert.Equal(to, Indicator(list).Bounds);
    }

    [AvaloniaFact]
    public void ItSnapsWhenTheSelectedItemMovesOnAResize()
    {
        var list = ShowList(Orientation.Vertical);
        list.SelectedIndex = 2;
        Tick(list, 1000);

        ((Border)list.Items[0]!).Height = 90;
        Layout(list);

        Assert.Equal(Slot(list, 2), Indicator(list).Bounds);
    }

    [AvaloniaFact]
    public void ItSnapsOntoASelectionScrolledIntoView()
    {
        var stage = VirtualListStage.Show(_ => 20);
        Selection.SetIndicator(stage.List, Template());
        stage.List.SelectedIndex = 0;
        Layout(stage.List);

        stage.List.SelectedIndex = 400;
        Layout(stage.List);
        stage.List.ScrollIntoView(400);
        Layout(stage.List);

        Assert.True(Indicator(stage.List).IsEffectivelyVisible);
        Assert.Equal(Slot(stage.List, 400), Indicator(stage.List).Bounds);
    }

    [AvaloniaFact]
    public void ItHidesWhenNothingIsSelected()
    {
        var list = ShowList(Orientation.Vertical);

        list.SelectedIndex = -1;
        Layout(list);

        Assert.False(Indicator(list).IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void ItHidesWhenTheSelectedContainerIsVirtualizedAway()
    {
        var stage = VirtualListStage.Show(_ => 20);
        Selection.SetIndicator(stage.List, Template());
        stage.List.SelectedIndex = 0;
        Layout(stage.List);
        Assert.True(Indicator(stage.List).IsEffectivelyVisible);

        // The panel keeps the list's tab-once element realized, so that has to move off the selection first.
        KeyboardNavigation.SetTabOnceActiveElement(stage.List, null);
        stage.Scroller.Offset = new Vector(0, 2000);
        Layout(stage.List);

        Assert.Null(stage.List.ContainerFromIndex(0));
        Assert.False(Indicator(stage.List).IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void FillCoversTheItem()
    {
        var list = ShowList(Orientation.Horizontal, startAt: 1);

        Assert.Equal(Slot(list, 1), Indicator(list).Bounds);
    }

    [AvaloniaFact]
    public void AHorizontalListsUnderlineIsABarAlongTheItemsBottomEdge()
    {
        var list = ShowList(Orientation.Horizontal, new Border { Height = 3, Background = Brushes.Red }, SelectionIndicatorMode.Underline, startAt: 1);

        var slot = Slot(list, 1);
        Assert.Equal(new Rect(slot.X, slot.Bottom - 3, slot.Width, 3), Indicator(list).Bounds);
    }

    [AvaloniaFact]
    public void AVerticalListsUnderlineRunsDownTheItemsLeftEdgeInsetByItsMargin()
    {
        var list = ShowList(Orientation.Vertical, new Border { Width = 3, Margin = new Thickness(0, 4), Background = Brushes.Red }, SelectionIndicatorMode.Underline, startAt: 1);

        var slot = Slot(list, 1);
        Assert.Equal(new Rect(slot.X, slot.Y + 4, 3, slot.Height - 8), Indicator(list).Bounds);
    }

    [AvaloniaTheory]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    public void AStretchingIndicatorsLeadingEdgeRunsAheadOfItsTrailingEdge(int from, int to)
    {
        var list = ShowList(Orientation.Horizontal);
        Selection.SetStretch(list, true);
        list.SelectedIndex = from;
        Tick(list, 1000);
        var start = Indicator(list).Bounds;

        list.SelectedIndex = to;
        Tick(list, 150);

        var now = Indicator(list).Bounds;
        var end = Slot(list, to);
        var (leading, trailing) = to > from
            ? (Progress(start.Right, now.Right, end.Right), Progress(start.Left, now.Left, end.Left))
            : (Progress(start.Left, now.Left, end.Left), Progress(start.Right, now.Right, end.Right));
        Assert.True(leading > trailing + .1, $"leading {leading}, trailing {trailing}");
        Assert.True(now.Width > start.Width);

        Tick(list, 1000);
        Assert.Equal(end, Indicator(list).Bounds);
    }

    [AvaloniaFact]
    public void CollapsingATreeViewSectionHidesItAndExpandingPlacesItWithoutEasing()
    {
        var page = new TreeViewItem { Header = "Page" };
        var section = new TreeViewItem { Header = "Section", IsExpanded = true, ItemsSource = new[] { new TreeViewItem { Header = "First" }, page } };
        var tree = new TreeView { ItemsSource = new[] { section }, Width = 200, VerticalAlignment = VerticalAlignment.Top };
        Selection.SetIndicator(tree, Template());
        Show(tree);
        tree.SelectedItem = page;
        Layout(tree);
        var row = Row(page);
        Assert.Equal(Slot(tree, row), Indicator(tree).Bounds);

        section.IsExpanded = false;
        Layout(tree);
        Assert.False(Indicator(tree).IsEffectivelyVisible);

        section.IsExpanded = true;
        Layout(tree);
        Assert.True(Indicator(tree).IsEffectivelyVisible);
        Assert.Equal(Slot(tree, row), Indicator(tree).Bounds);
    }

    [AvaloniaFact]
    public void UnderReducedMotionANewSelectionIsPlacedAtOnce()
    {
        Presto.Defaults.ReducedMotion = true;
        var list = ShowList(Orientation.Vertical);

        list.SelectedIndex = 3;
        Layout(list);

        Assert.Equal(Slot(list, 3), Indicator(list).Bounds);
    }

    private static ListBox ShowList(
        Orientation orientation, Control? indicator = null, SelectionIndicatorMode mode = SelectionIndicatorMode.Fill, int startAt = 0)
    {
        var horizontal = orientation == Orientation.Horizontal;
        var list = new ListBox
        {
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = orientation }),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Styles =
            {
                new Style(x => x.OfType<ListBoxItem>())
                {
                    Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(0)), new Setter(Layoutable.MinHeightProperty, 0d) },
                },
            },
        };
        for (var i = 0; i < 5; i++) list.Items.Add(new Border { Width = horizontal ? 60 : 100, Height = horizontal ? 30 : 40 });
        Selection.SetIndicator(list, Template(indicator));
        Selection.SetIndicatorMode(list, mode);
        list.SelectedIndex = startAt;
        Show(list);
        Layout(list);
        return list;
    }

    private static FuncTemplate<Control?> Template(Control? indicator = null) =>
        new(() => indicator ?? new Border { Background = Brushes.Red });

    private static Control Indicator(ItemsControl list) => Selection.IndicatorOf(list)!;

    private static Rect Slot(ListBox list, int index) => Slot(list, list.ContainerFromIndex(index)!);

    private static Rect Slot(ItemsControl list, Control row) => new(row.TranslatePoint(default, list.Presenter!)!.Value, row.Bounds.Size);

    private static Control Row(TreeViewItem item) =>
        item.GetTemplateDescendants().OfType<Control>().First(part => part.Name == "PART_LayoutRoot");

    private static double Progress(double start, double now, double end) => (now - start) / (end - start);

    // A placement made while handling LayoutUpdated lands in the pass after it.
    private static void Layout(Control control)
    {
        Dispatcher.UIThread.RunJobs();
        control.UpdateLayout();
        control.UpdateLayout();
    }

    private void Tick(Control control, double ms)
    {
        for (var elapsed = 0d; elapsed < ms; elapsed += 16)
        {
            Frames.Advance(16);
            Layout(control);
        }
    }
}
