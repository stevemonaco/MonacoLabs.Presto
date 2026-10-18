using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Scroll.StickToEndProperty"/> on a 200 high scroller over 40 high rows: a plain <see cref="ItemsControl"/> of
/// 10 rows, or <see cref="VirtualListStage"/>'s virtualized <see cref="ListBox"/>.
/// </summary>
public sealed class ScrollStickToEndTests : BridgeTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AppendsAtTheEndEaseToTheNewEndAndLandExactly(bool virtualized)
    {
        var (scroller, items) = Stage(virtualized);
        ToEnd(scroller);
        var from = scroller.Offset.Y;

        Append(items, 3);
        Run(16);
        Assert.InRange(scroller.Offset.Y, from + 1, from + 119);
        Run(1000);

        Assert.Equal(scroller.ScrollBarMaximum.Y, scroller.Offset.Y);
        Assert.Equal(from + 120, scroller.Offset.Y, 1e-6);
        Assert.False(Presto.Engine.IsRunning);
    }

    [AvaloniaFact]
    public void ABurstMidEaseRetargetsWithoutJumping()
    {
        var (scroller, items) = Stage(virtualized: false);
        ToEnd(scroller);
        Append(items, 3);
        Run(96);
        var ease = Assert.Single(Presto.GetAnimations(scroller, "ScrollY"));
        var before = scroller.Offset.Y;

        Append(items, 5);
        Run(16);
        Assert.InRange(scroller.Offset.Y, before, before + 60);
        Assert.Equal([ease], Presto.GetAnimations(scroller, "ScrollY"));
        Run(1000);

        Assert.Equal(520, scroller.Offset.Y);
        Assert.Equal(scroller.ScrollBarMaximum.Y, scroller.Offset.Y);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AWheelUpReleasesAndLaterAppendsLeaveTheOffsetAlone(bool midEase)
    {
        var (scroller, items) = Stage(virtualized: false);
        ToEnd(scroller);
        if (midEase)
        {
            Append(items, 3);
            Run(96);
        }

        Wheel(scroller, 1);
        var left = scroller.Offset.Y;
        Append(items, 3);
        Run(1000);

        Assert.True(left < scroller.ScrollBarMaximum.Y - 1);
        Assert.Equal(left, scroller.Offset.Y);
    }

    [AvaloniaTheory]
    [InlineData("wheel")]
    [InlineData("offset")]
    public void ScrollingBackToTheEndReArms(string by)
    {
        var (scroller, items) = Stage(virtualized: false);
        ToEnd(scroller);
        Wheel(scroller, 1);
        Append(items, 1);
        Run(500);

        if (by == "wheel") Wheel(scroller, -10);
        else ToEnd(scroller);
        Append(items, 3);
        Run(1000);

        Assert.Equal(360, scroller.Offset.Y);
        Assert.Equal(scroller.ScrollBarMaximum.Y, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void ContentShorterThanTheViewportThatGrowsPastItFollows()
    {
        var (scroller, items) = Stage(virtualized: false, count: 2);

        Append(items, 6);
        Run(1000);

        Assert.Equal(120, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void WithTheScrollAnchorAppendsAtTheEndFollowWithoutAnAnchorAndMoveForwardOnly()
    {
        var (scroller, items) = Stage(virtualized: true);
        var list = (ListBox)scroller.TemplatedParent!;
        Presence.SetScrollAnchor(list, true);
        ToEnd(scroller);
        var offsets = Offsets(scroller);
        var from = scroller.Offset.Y;

        Append(items, 3);
        Assert.False(ItemChanges.For(list).Anchoring);
        Run(1000);

        Assert.Equal(from + 120, scroller.Offset.Y, 1e-6);
        Assert.Equal(offsets.Order(), offsets);
    }

    [AvaloniaFact]
    public void WithTheScrollAnchorAnInsertAtTheTopWhileScrolledUpKeepsTheRowStillAndMovesTheOffsetOnce()
    {
        var stage = VirtualListStage.Show(_ => 40);
        Presence.SetScrollAnchor(stage.List, true);
        Scroll.SetStickToEnd(stage.Scroller, true);
        stage.Scroller.Offset = new Vector(0, 1000);
        Dispatcher.UIThread.RunJobs();
        var y = stage.Top(25);
        var offsets = Offsets(stage.Scroller);

        stage.Items.Insert(0, -1);
        Assert.True(ItemChanges.For(stage.List).Anchoring);
        Run(1000);

        Assert.Equal(y, stage.Top(26));
        Assert.Equal([1040], offsets);
        Assert.Empty(Presto.GetAnimations(stage.Scroller, "ScrollY"));
    }

    [AvaloniaFact]
    public void WithTheScrollAnchorAnInsertAtTheTopWhileAtTheEndIsLeftToStickToEnd()
    {
        var (scroller, items) = Stage(virtualized: true);
        var list = (ListBox)scroller.TemplatedParent!;
        Presence.SetScrollAnchor(list, true);
        ToEnd(scroller);

        items.Insert(0, -1);
        Assert.False(ItemChanges.For(list).Anchoring);
        Run(1000);

        Assert.Equal(scroller.ScrollBarMaximum.Y, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void TurningItOffMidEaseStopsWhereItIsAndFollowsNoMore()
    {
        var (scroller, items) = Stage(virtualized: false);
        ToEnd(scroller);
        Append(items, 3);
        Run(96);
        var left = scroller.Offset.Y;

        Scroll.SetStickToEnd(scroller, false);
        Append(items, 3);
        Run(1000);

        Assert.Equal(left, scroller.Offset.Y);
        Assert.Empty(Presto.GetAnimations(scroller, "ScrollY"));
        Assert.False(Presto.Engine.IsRunning);
    }

    [AvaloniaFact]
    public void ReducedMotionJumpsToTheNewEndAndTheEngineIdles()
    {
        Presto.Defaults.ReducedMotion = true;
        var (scroller, items) = Stage(virtualized: false);
        ToEnd(scroller);

        Append(items, 3);
        Run(16);
        Assert.Equal(320, scroller.Offset.Y);
        Run(100);

        Assert.False(Presto.Engine.IsRunning);
    }

    private static (ScrollViewer Scroller, ObservableCollection<int> Items) Stage(bool virtualized, int count = 10)
    {
        if (virtualized)
        {
            var stage = VirtualListStage.Show(_ => 40);
            Scroll.SetStickToEnd(stage.Scroller, true);
            return (stage.Scroller, stage.Items);
        }
        var items = new ObservableCollection<int>(Enumerable.Range(0, count));
        var scroller = new ScrollViewer
        {
            Height = 200,
            VerticalAlignment = VerticalAlignment.Top,
            Content = new ItemsControl { ItemsSource = items, ItemTemplate = new FuncDataTemplate<int>((_, _) => new Border { Height = 40 }) },
        };
        Scroll.SetStickToEnd(scroller, true);
        Show(scroller);
        return (scroller, items);
    }

    private static void Append(ObservableCollection<int> items, int count)
    {
        for (var i = 0; i < count; i++) items.Add(items.Count);
        Dispatcher.UIThread.RunJobs();
    }

    private static void ToEnd(ScrollViewer scroller)
    {
        // A virtualized extent settles over a few passes once the last rows realize.
        for (var i = 0; i < 3; i++)
        {
            scroller.Offset = new Vector(0, scroller.ScrollBarMaximum.Y);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Wheel(ScrollViewer scroller, double notches)
    {
        ((Window)TopLevel.GetTopLevel(scroller)!).MouseWheel(new Point(50, 100), new Vector(0, notches));
        Dispatcher.UIThread.RunJobs();
    }

    private static List<double> Offsets(ScrollViewer scroller)
    {
        var offsets = new List<double>();
        scroller.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrollViewer.OffsetProperty) offsets.Add(scroller.Offset.Y);
        };
        return offsets;
    }

    private void Run(double ms)
    {
        for (var elapsed = 0d; elapsed < ms; elapsed += 16)
        {
            Frames.Advance(16);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void WithoutStickToEndGrowthAtTheEndLeavesTheOffsetAlone()
    {
        var (_, scroller, _, trail) = ScrollStage.Show();
        ToEnd(scroller);
        var end = scroller.Offset.Y;

        trail.Height += 200;
        Dispatcher.UIThread.RunJobs();
        Run(1000);

        Assert.False(Scroll.GetStickToEnd(scroller));
        Assert.Equal(end + 200, scroller.ScrollBarMaximum.Y);
        Assert.Equal(end, scroller.Offset.Y);
    }
}
