using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Tests;

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

    [AvaloniaTheory]
    [InlineData("X")]
    [InlineData("Y")]
    [InlineData("Scale")]
    [InlineData("Loop")]
    public void AMotionWithMoreThanTimingAndEaseThrowsWhenSet(string setting)
    {
        var motion = Presto.Build().Duration(200);
        _ = setting switch
        {
            "X" => motion.X(ease: Eases.OutQuad),
            "Y" => motion.Y(ease: Eases.OutQuad),
            "Scale" => motion.Scale(ease: Eases.OutQuad),
            _ => motion.Loop(2),
        };

        Assert.Throws<NotSupportedException>(() => Selection.SetMotion(new ListBox(), motion));
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
        var list = ShowList(Orientation.Horizontal, mode: SelectionIndicatorMode.Fill, startAt: 1);

        Assert.Equal(Slot(list, 1), Indicator(list).Bounds);
    }

    [AvaloniaFact]
    public void WithoutAModeTheIndicatorFillsTheItem()
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

    [AvaloniaFact]
    public void ReplacingTheTemplateSwapsTheIndicatorAndANullTemplateOrBuildLeavesNone()
    {
        var list = ShowList(Orientation.Vertical);
        var old = Indicator(list);
        list.SelectedIndex = 3;
        Tick(list, 100);
        Assert.Equal(1, Running());

        Selection.SetIndicator(list, Template());
        Layout(list);
        var replacement = Indicator(list);
        Assert.NotSame(old, replacement);
        Assert.Equal(0, Running());
        Assert.False(list.Presenter!.IsVisualAncestorOf(old));
        Assert.True(list.Presenter.IsVisualAncestorOf(replacement));
        Assert.Equal(Slot(list, 3), replacement.Bounds);

        Selection.SetIndicator(list, null);
        Assert.Null(Selection.IndicatorOf(list));
        Assert.False(list.Presenter.IsVisualAncestorOf(replacement));

        Selection.SetIndicator(list, new FuncTemplate<Control?>(() => null));
        Layout(list);
        Assert.Null(Selection.IndicatorOf(list));
        Assert.Same(list.Presenter!.Panel, Assert.Single(list.Presenter.GetVisualChildren()));
    }

    [AvaloniaFact]
    public void InDesignModeSettingTheIndicatorDoesNothing()
    {
        var isDesignMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!; // The setter is internal.
        var list = ShowList(Orientation.Vertical);
        var indicator = Indicator(list);
        var other = new ListBox();
        isDesignMode.SetValue(null, true);
        try
        {
            Selection.SetIndicator(list, null);
            Selection.SetIndicator(other, Template());
        }
        finally
        {
            isDesignMode.SetValue(null, false);
        }

        Assert.Same(indicator, Selection.IndicatorOf(list));
        Assert.Null(Selection.IndicatorOf(other));
    }

    [AvaloniaFact]
    public void TheIndicatorLivesInThePresenterBehindThePanelAndIsNotHitTestable()
    {
        var list = ShowList(Orientation.Vertical);
        var presenter = list.Presenter!;
        var indicator = Indicator(list);
        var children = presenter.GetVisualChildren().ToArray();

        Assert.Equal(2, children.Length);
        Assert.True(children[0] == indicator || children[0].IsVisualAncestorOf(indicator));
        Assert.Same(presenter.Panel, children[1]);
        Assert.True(presenter.IsLogicalAncestorOf(indicator));
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var center = indicator.TranslatePoint(new Point(indicator.Bounds.Width / 2, indicator.Bounds.Height / 2), presenter)!.Value;
        Assert.DoesNotContain(indicator, presenter.GetInputElementsAt(center));
    }

    [AvaloniaFact]
    public void ANewPresenterOrPanelTakesTheIndicatorBackOnTheNextLayout()
    {
        var list = ShowList(Orientation.Vertical, startAt: 2);
        var old = list.Presenter;
        var template = list.Template;

        list.Template = null;
        Layout(list);
        list.Template = template;
        Layout(list);
        Assert.NotSame(old, list.Presenter);
        Assert.True(list.Presenter!.IsVisualAncestorOf(Indicator(list)));
        Assert.Equal(Slot(list, 2), Indicator(list).Bounds);

        list.ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel());
        Layout(list);
        Assert.True(list.Presenter!.IsVisualAncestorOf(Indicator(list)));
        Assert.Equal(Slot(list, 2), Indicator(list).Bounds);
    }

    [AvaloniaFact]
    public void WithoutAPresenterTheIndicatorIsNotPlaced()
    {
        var list = new ListBox { Template = null, Items = { new Border { Height = 40 }, new Border { Height = 40 } } };
        Selection.SetIndicator(list, Template());
        Show(list);

        list.SelectedIndex = 1;
        Layout(list);

        Assert.Null(list.Presenter);
        Assert.Null(Indicator(list).GetVisualParent()?.GetVisualParent());
    }

    [AvaloniaFact]
    public void LeavingTheTreeCancelsTheGlideAndTheNextPlacementSnaps()
    {
        var list = ShowList(Orientation.Vertical);
        var window = (Window)TopLevel.GetTopLevel(list)!;
        list.SelectedIndex = 3;
        Tick(list, 100);
        Assert.Equal(1, Running());

        window.Content = null;
        Assert.Equal(0, Running());
        window.Content = list;
        Layout(list);

        Assert.Equal(Slot(list, 3), Indicator(list).Bounds);
    }

    [AvaloniaFact]
    public void HidingCancelsTheRunningGlide()
    {
        var list = ShowList(Orientation.Vertical);
        list.SelectedIndex = 3;
        Tick(list, 100);
        Assert.Equal(1, Running());

        list.ContainerFromIndex(3)!.IsVisible = false;
        Layout(list);

        Assert.False(Indicator(list).IsEffectivelyVisible);
        Assert.Equal(0, Running());
    }

    [AvaloniaFact]
    public void AListThatIsNeitherSelectingNorATreeKeepsTheIndicatorHidden()
    {
        var list = new ItemsControl { Items = { new Border { Width = 100, Height = 40 } } };
        Selection.SetIndicator(list, Template());
        Show(list);
        Layout(list);

        Assert.NotNull(Indicator(list));
        Assert.False(Indicator(list).IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void ASelectionChangeBubblingFromANestedListDoesNotMoveTheIndicator()
    {
        var inner = new ListBox { Items = { new Border { Width = 50, Height = 10 }, new Border { Width = 50, Height = 10 } } };
        var list = ShowList(Orientation.Vertical);
        list.Items.Add(inner);
        Layout(list);
        var row = list.ContainerFromIndex(0)!;
        row.Arrange(new Rect(0, 100, row.Bounds.Width, row.Bounds.Height));

        inner.SelectedIndex = 1;

        Assert.Equal(0, Running());
    }

    [AvaloniaFact]
    public void ASelectionMadeWhileHiddenSnaps()
    {
        var list = ShowList(Orientation.Vertical);
        list.SelectedIndex = -1;
        Layout(list);

        list.SelectedIndex = 3;
        Layout(list);

        Assert.Equal(0, Running());
        Assert.Equal(Slot(list, 3), Indicator(list).Bounds);
    }

    [AvaloniaFact]
    public void ANewGlideTakesOverFromWhereTheRunningOneHasGot()
    {
        var list = ShowList(Orientation.Vertical);
        list.SelectedIndex = 4;
        Tick(list, 100);
        var mid = Indicator(list).Bounds;

        list.SelectedIndex = 1;
        Tick(list, 16);

        Assert.Equal(1, Running());
        Assert.InRange(Indicator(list).Bounds.Y, mid.Y - 10, mid.Y + 10);
        Tick(list, 1000);
        Assert.Equal(Slot(list, 1), Indicator(list).Bounds);
    }

    [AvaloniaTheory]
    [InlineData(true, true, .5)]
    [InlineData(false, true, .5)]
    [InlineData(true, false, .875)]
    public void AMotionSetsTheGlidesDurationDelayAndEase(bool duration, bool ease, double progress)
    {
        Presto.Defaults.Duration = 320;
        var motion = Presto.Build().Delay(96);
        if (duration) motion.Duration(320);
        if (ease) motion.Ease(Eases.None);
        var list = ShowList(Orientation.Vertical);
        Selection.SetMotion(list, motion);

        list.SelectedIndex = 3;
        Tick(list, 80);
        Assert.Equal(Slot(list, 0), Indicator(list).Bounds);
        Tick(list, 176);

        Assert.InRange(Indicator(list).Bounds.Y, Slot(list, 3).Y * progress - 5, Slot(list, 3).Y * progress + 5);
    }

    [AvaloniaFact]
    public void ASpringGlideLastsTheSpringsSettlingDuration()
    {
        var settling = Springs.Gentle.SettlingDuration;
        var list = ShowList(Orientation.Vertical);
        Selection.SetMotion(list, Presto.Build().Duration(100).Ease(Springs.Gentle));

        list.SelectedIndex = 3;
        Tick(list, settling - 50);
        Assert.Equal(1, Running());
        Tick(list, 100);

        Assert.Equal(0, Running());
        Assert.Equal(Slot(list, 3), Indicator(list).Bounds);
    }

    [AvaloniaFact]
    public void StretchHasNoEffectWhenTheCentersCoincideAlongTheAxis()
    {
        var list = new ListBox
        {
            ItemsPanel = new FuncTemplate<Panel?>(() => new Grid()),
            Width = 200,
            Height = 100,
            Styles =
            {
                new Style(x => x.OfType<ListBoxItem>())
                {
                    Setters =
                    {
                        new Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
                        new Setter(Layoutable.MinHeightProperty, 0d),
                        new Setter(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Center),
                        new Setter(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Center),
                    },
                },
            },
            Items = { new Border { Width = 160, Height = 80 }, new Border { Width = 40, Height = 20 } },
        };
        Selection.SetIndicator(list, Template());
        Selection.SetStretch(list, true);
        list.SelectedIndex = 0;
        Show(list);
        Layout(list);
        var start = Indicator(list).Bounds;

        list.SelectedIndex = 1;
        Tick(list, 150);

        var (now, end) = (Indicator(list).Bounds, Slot(list, 1));
        Assert.Equal(Progress(start.Top, now.Top, end.Top), Progress(start.Bottom, now.Bottom, end.Bottom), .05);
        Assert.Equal(Progress(start.Left, now.Left, end.Left), Progress(start.Right, now.Right, end.Right), .05);
    }

    [AvaloniaFact]
    public void WithoutStretchEveryEdgeGlidesOnThePlainEase()
    {
        var list = ShowList(Orientation.Horizontal);
        var start = Indicator(list).Bounds;

        list.SelectedIndex = 3;
        Tick(list, 150);

        var (now, end) = (Indicator(list).Bounds, Slot(list, 3));
        Assert.InRange(Progress(start.Left, now.Left, end.Left), .1, .9);
        Assert.Equal(Progress(start.Left, now.Left, end.Left), Progress(start.Right, now.Right, end.Right), .02);
        Assert.Equal(start.Width, now.Width, 1);
    }

    private static int Running()
    {
        var running = 0;
        for (var timer = Presto.Engine._head; timer != null; timer = timer._next)
        {
            if (!timer.Paused) running++;
        }
        return running;
    }

    private static ListBox ShowList(
        Orientation orientation, Control? indicator = null, SelectionIndicatorMode? mode = null, int startAt = 0)
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
        if (mode is { } set) Selection.SetIndicatorMode(list, set);
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
