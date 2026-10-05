using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Events;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// The Phase 59 spike (GAPS-2-PLAN §5.3), kept as pinning tests: how Avalonia 12.1.2 treats a <see cref="ScrollViewer.OffsetProperty"/>
/// written with <c>SetCurrentValue</c>, how each kind of offset change is notified, and how a <see cref="VirtualizingStackPanel"/>
/// estimates its extent while realizing far items.
/// </summary>
public sealed class ScrollSpikeTests : BridgeTestBase
{
    private static readonly Point Inside = new(100, 100);

    [AvaloniaFact]
    public void ASetCurrentValueOffsetReadsBackExactlyThroughLayoutAnExtentThatStillHoldsItAndAViewportResize()
    {
        var (window, scroller, _, trail) = ScrollStage.Show(trail: 600);
        scroller.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, 123.37));
        var changes = Record(scroller);

        window.UpdateLayout();
        trail.Height = 500;
        window.UpdateLayout();
        scroller.Height = 150;
        window.UpdateLayout();

        Assert.Equal(123.37, scroller.Offset.Y);
        Assert.Equal(150, scroller.Viewport.Height);
        Assert.Empty(changes);
    }

    [AvaloniaFact]
    public void AnOffsetOutsideTheRangeIsCoercedInsideTheWriteAndNotifiedWithTheClampedValue()
    {
        var (_, scroller, _, _) = ScrollStage.Show(trail: 600);
        var changes = Record(scroller);

        scroller.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, 5000));
        Assert.Equal(800, scroller.Offset.Y);
        scroller.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, -5));
        Assert.Equal(0, scroller.Offset.Y);

        Assert.Equal([800d, 0d], changes.Select(change => change.Y));
    }

    [AvaloniaTheory]
    [InlineData("wheel", 150.5)]
    [InlineData("thumb", 142.16666666666669)]
    [InlineData("page down", 300.5)]
    [InlineData("focus", 230)]
    public void UserInputStillMovesASetCurrentValueOffsetAndIsNotifiedOutsideLayoutAtUnsetPriority(string input, double expected)
    {
        var (window, scroller, target, trail) = ScrollStage.Show();
        var button = new Button { Height = 30, VerticalAlignment = VerticalAlignment.Top };
        trail.Child = button;
        target.Focusable = true;
        target.Focus();
        scroller.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, 100.5));
        Dispatcher.UIThread.RunJobs();
        var changes = Record(scroller);

        switch (input)
        {
            case "wheel": window.MouseWheel(Inside, new Vector(0, -1)); break;
            case "thumb": ScrollStage.DragThumb(window, scroller, 10); break;
            case "page down": window.KeyPress(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null); break;
            default: button.Focus(); break;
        }
        Dispatcher.UIThread.RunJobs();

        var change = Assert.Single(changes);
        Assert.Equal(scroller.Offset.Y, change.Y);
        Assert.Equal(expected, change.Y);
        Assert.Equal(BindingPriority.Unset, change.Priority);
        Assert.False(change.InArrange);
        Assert.Equal((true, false), change.ArrangeValid);
    }

    [AvaloniaFact]
    public void TheScrollViewerHandlesNeitherEndNorHomeButAListBoxEndAndHomeScrollToItsEnds()
    {
        var (window, scroller, target, _) = ScrollStage.Show();
        target.Focusable = true;
        target.Focus();
        scroller.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, 100));
        window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null);
        window.KeyPress(Key.End, RawInputModifiers.Control, PhysicalKey.End, null);
        Assert.Equal(100, scroller.Offset.Y);

        var stage = VirtualListStage.Show(Varied);
        stage.List.ContainerFromIndex(0)!.Focus();
        var changes = Record(stage.Scroller);
        stage.Window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(stage.Scroller.ScrollBarMaximum.Y, stage.Scroller.Offset.Y);
        Assert.Equal(VirtualListStage.ViewportSize - Varied(4999), stage.Top(4999));
        Assert.False(Assert.Single(changes).InArrange);

        stage.Window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, stage.Scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void PriorityNeverTellsWritersApart()
    {
        var (window, scroller, _, _) = ScrollStage.Show(trail: 600);
        var changes = Record(scroller);

        scroller.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, 100));
        window.MouseWheel(Inside, new Vector(0, -1));
        scroller.Offset = new Vector(0, 300);
        scroller.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, 400));
        window.MouseWheel(Inside, new Vector(0, -1));

        Assert.Equal(
            [BindingPriority.Unset, BindingPriority.Unset, BindingPriority.LocalValue, BindingPriority.LocalValue, BindingPriority.LocalValue],
            changes.Select(change => change.Priority));
    }

    [AvaloniaFact]
    public void AWritersOwnChangeArrivesInsideItsCallAndAWriteFromTheHandlerNestsInsideIt()
    {
        var (_, scroller, _, _) = ScrollStage.Show(trail: 600);
        var writing = false;
        var seen = new List<(double Y, bool InCall)>();
        scroller.PropertyChanged += (_, e) =>
        {
            if (e.Property != ScrollViewer.OffsetProperty) return;
            seen.Add((((Vector)e.NewValue!).Y, writing));
            if (seen.Count == 1) scroller.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, 50));
        };

        writing = true;
        scroller.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, 200));
        writing = false;

        Assert.Equal([(200d, true), (50d, true)], seen);
        Assert.Equal(50, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void AnExtentShrinkCoercesTheOffsetInsideArrangeAfterTheMaximumMovesAndRegrowthDoesNotRestoreIt()
    {
        var (window, scroller, _, trail) = ScrollStage.Show(trail: 600);
        scroller.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, 690));
        var seen = new List<string>();
        scroller.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrollViewer.OffsetProperty) seen.Add($"offset {scroller.Offset.Y} max {scroller.ScrollBarMaximum.Y} in arrange {InArrange()} arrange valid {scroller.IsArrangeValid}/{scroller.Presenter!.IsArrangeValid}");
            else if (e.Property == ScrollViewer.ExtentProperty) seen.Add($"extent {scroller.Extent.Height}");
        };

        trail.Height = 100;
        window.UpdateLayout();
        trail.Height = 600;
        window.UpdateLayout();

        Assert.Equal(["offset 300 max 300 in arrange True arrange valid True/False", "extent 500", "extent 1000"], seen);
        Assert.Equal(300, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void TheLastWrittenValueAndTheMovedMaximumSeparateOwnWritesClampsAndInput()
    {
        var (window, scroller, _, trail) = ScrollStage.Show(trail: 600);
        var written = double.NaN;
        var kinds = new List<string>();
        scroller.PropertyChanged += (_, e) =>
        {
            if (e.Property != ScrollViewer.OffsetProperty) return;
            var y = ((Vector)e.NewValue!).Y;
            kinds.Add(y == written ? "own" : y < written && y == scroller.ScrollBarMaximum.Y ? "clamp" : "foreign");
        };
        void Write(double y)
        {
            written = Math.Clamp(y, 0, scroller.ScrollBarMaximum.Y);
            scroller.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, written));
        }

        Write(690);
        trail.Height = 100;
        window.UpdateLayout();
        Write(5000);
        window.MouseWheel(Inside, new Vector(0, 1));
        Write(50.25);
        ScrollStage.DragThumb(window, scroller, 10);
        scroller.Offset = new Vector(0, 10);

        Assert.Equal(["own", "clamp", "foreign", "own", "foreign", "foreign"], kinds);
    }

    [AvaloniaFact]
    public void TheFirstLayoutsExtentUsesAStaleEstimateThatTheNextMeasureCorrectsToTheRealizedAverage()
    {
        var stage = VirtualListStage.Show(Varied);
        var realized = stage.List.GetRealizedContainers().Select(container => container.Bounds.Height).ToList();
        Assert.Equal(100_100, stage.Scroller.Extent.Height);
        Assert.Equal(40, realized.Average());

        Remeasure(stage);

        Assert.Equal(VirtualListStage.Count * 40, stage.Scroller.Extent.Height);
        var stale = VirtualListStage.Show(Varied).Scroller;
        stale.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, 3000 * 40));
        Assert.Equal(99_900, stale.Offset.Y);
    }

    [AvaloniaFact]
    public void AJumpByOffsetRealizesTheItemAtThatOffsetInOnePassAndThePanelNeverWritesTheOffset()
    {
        var stage = VirtualListStage.Show(Skewed);
        var estimate = stage.Scroller.Extent.Height / VirtualListStage.Count;
        var changes = Record(stage.Scroller);

        stage.Scroller.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, 3000 * estimate));
        Assert.Null(stage.List.ContainerFromIndex(3000));
        Dispatcher.UIThread.RunJobs();
        var extent = stage.Scroller.Extent.Height;
        stage.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(20, estimate);
        Assert.Equal(0, stage.Top(3000));
        Assert.Equal(100_180, extent);
        Assert.Equal(extent, stage.Scroller.Extent.Height);
        Assert.Equal([60_000d], changes.Select(change => change.Y));
        Assert.Equal(237_000, Enumerable.Range(0, 3000).Sum(Skewed));
    }

    [AvaloniaFact]
    public void ScrollingAmongRealizedItemsKeepsTheirPositionsWhileTheExtentEstimateSwings()
    {
        var stage = VirtualListStage.Show(Varied);
        Remeasure(stage);
        stage.List.ScrollIntoView(3000);
        var scroller = stage.Scroller;
        var top = stage.Top(3000)!.Value + scroller.Offset.Y;
        var extents = new List<double>();
        var changes = Record(scroller);

        foreach (var step in Enumerable.Repeat(-30d, 20).Concat(Enumerable.Repeat(30d, 20)))
        {
            scroller.SetCurrentValue(ScrollViewer.OffsetProperty, scroller.Offset + new Vector(0, step));
            Dispatcher.UIThread.RunJobs();
            extents.Add(scroller.Extent.Height);
        }

        Assert.Equal(top, stage.Top(3000)!.Value + scroller.Offset.Y);
        Assert.Equal(40, changes.Count);
        Assert.InRange(extents.Min(), 185_000, 195_000);
        Assert.InRange(extents.Max(), 205_000, 215_000);
    }

    [AvaloniaFact]
    public void ScrollIntoViewRealizesTheItemInsideTheCallAndAlignsItToTheNearestEdge()
    {
        var stage = VirtualListStage.Show(Skewed);
        var changes = Record(stage.Scroller);

        stage.List.ScrollIntoView(4500);
        Assert.Equal(VirtualListStage.ViewportSize - 80, stage.Top(4500));
        stage.List.ScrollIntoView(100);
        Assert.Equal(0, stage.Top(100));

        Assert.Equal(2, changes.Count);
        Assert.All(changes, change => Assert.False(change.InArrange));
    }

    [AvaloniaFact]
    public void SmoothTreatsItsOwnWriteClampedByAShrunkExtentAsForeignAndStops()
    {
        var (window, scroller, _, trail) = ScrollStage.Show(trail: 600);
        var lead = (Border)((StackPanel)scroller.Content!).Children[0];
        Scroll.SetSmooth(scroller, true);
        window.MouseWheel(Inside, new Vector(0, -3));
        Frames.Advance(100, 16);
        Assert.InRange(scroller.Offset.Y, 50, 99);

        lead.Height = 100;
        trail.Height = 100;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(32, 16);
        lead.Height = ScrollStage.Lead;
        trail.Height = 600;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);

        Assert.Equal(100, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void ASnapTreatsAClampOfItsOwnWriteAsForeignAndSnapsAgainWithinTheNewRange()
    {
        var (_, scroller, target, trail) = ScrollStage.Show();
        PrestoAvalonia.OnScroll(new ScrollObserverParams { Target = target, Snap = .25 });
        Frames.Advance(16);
        ScrollStage.ScrollTo(scroller, 230);
        Frames.Advance(320, 16);
        Assert.InRange(scroller.Offset.Y, 231, 249);

        trail.Height = 0;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(200, scroller.Offset.Y);
        Frames.Advance(1000, 16);

        Assert.Equal(175, scroller.Offset.Y);
    }

    private static double Varied(int index) => 20 + (index % 5 * 10);

    private static double Skewed(int index) => index < 50 ? 20 : 80;

    private static void Remeasure(VirtualListStage stage)
    {
        stage.List.GetVisualDescendants().OfType<VirtualizingStackPanel>().Single().InvalidateMeasure();
        stage.Window.UpdateLayout();
    }

    private static List<(double Y, BindingPriority Priority, bool InArrange, (bool, bool) ArrangeValid)> Record(ScrollViewer scroller)
    {
        var changes = new List<(double, BindingPriority, bool, (bool, bool))>();
        scroller.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrollViewer.OffsetProperty) changes.Add((((Vector)e.NewValue!).Y, e.Priority, InArrange(), (scroller.IsArrangeValid, scroller.Presenter!.IsArrangeValid)));
        };
        return changes;
    }

    private static bool InArrange() => Environment.StackTrace.Contains(".ArrangeOverride(", StringComparison.Ordinal);
}
