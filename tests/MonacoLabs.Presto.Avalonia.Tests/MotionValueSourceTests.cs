using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MonacoLabs.Presto.Events;

namespace MonacoLabs.Presto.Tests;

/// <summary>Covers the bridge's <see cref="MotionValue"/> sources: scroll, scroll observer, draggable, pointer and property values.</summary>
public sealed class MotionValueSourceTests : BridgeTestBase
{
    // A 200 by 200 scroller over 1000 by 600 content, so it scrolls 800 across and 400 down.
    private static (Window Window, ScrollViewer Scroller, Border Target) ShowScroller()
    {
        var target = new Border { Width = 100, Height = 100, Background = Brushes.Red };
        var scroller = new ScrollViewer
        {
            Width = 200,
            Height = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Canvas { Width = 1000, Height = 600, Children = { target } },
        };
        Canvas.SetTop(target, 300);
        var window = new Window { Content = scroller, Width = 300, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, scroller, target);
    }

    private void ScrollTo(ScrollViewer scroller, double x, double y)
    {
        scroller.Offset = new Vector(x, y);
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(16);
    }

    [AvaloniaFact]
    public void ScrollValuesFollowTheOffsetOnBothAxes()
    {
        var (_, scroller, _) = ShowScroller();
        var x = PrestoAvalonia.ScrollValue(scroller, ScrollAxis.Horizontal);
        var y = PrestoAvalonia.ScrollValue(scroller);
        var progressX = PrestoAvalonia.ScrollValue(scroller, ScrollAxis.Horizontal, progress: true);
        var progressY = PrestoAvalonia.ScrollValue(scroller, progress: true);

        ScrollTo(scroller, 200, 100);

        Assert.Equal(200d, x.Value);
        Assert.Equal(100d, y.Value);
        Assert.Equal(.25, progressX.Value);
        Assert.Equal(.25, progressY.Value);

        ScrollTo(scroller, 800, 400);

        Assert.Equal(800d, x.Value);
        Assert.Equal(1d, progressX.Value);
        Assert.Equal(1d, progressY.Value);
    }

    [AvaloniaFact]
    public void ScrollProgressIsRefreshedWhenTheExtentChanges()
    {
        var (_, scroller, _) = ShowScroller();
        var progress = PrestoAvalonia.ScrollValue(scroller, progress: true);
        ScrollTo(scroller, 0, 200);
        Assert.Equal(.5, progress.Value);

        ((Canvas)scroller.Content!).Height = 1000;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(.25, progress.Value);
    }

    [AvaloniaFact]
    public void ScrollRemainingValuesHoldTheDistanceLeftOnBothAxes()
    {
        var vertical = ScrollStage.Show();
        var horizontal = ScrollStage.Show(Orientation.Horizontal);
        var down = PrestoAvalonia.ScrollRemainingValue(vertical.Scroller);
        var across = PrestoAvalonia.ScrollRemainingValue(horizontal.Scroller, ScrollAxis.Horizontal);

        Assert.Equal(500d, down.Value);
        Assert.Equal(500d, across.Value);

        vertical.Scroller.Offset = new Vector(0, 120.4);
        horizontal.Scroller.Offset = new Vector(500, 0);

        Assert.Equal(380d, down.Value);
        Assert.Equal(0d, across.Value);
    }

    [AvaloniaFact]
    public void ScrollRemainingIsRefreshedWhenTheExtentChanges()
    {
        var stage = ScrollStage.Show();
        var remaining = PrestoAvalonia.ScrollRemainingValue(stage.Scroller);
        ScrollStage.ScrollTo(stage.Scroller, 100);

        stage.Trail.Height = 500;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(600d, remaining.Value);
    }

    [AvaloniaFact]
    public void ScrollRemainingIsZeroWhenTheContentFits()
    {
        var stage = ScrollStage.Show(trail: 0);
        stage.Panel.Children.RemoveAt(0);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0d, PrestoAvalonia.ScrollRemainingValue(stage.Scroller).Value);
    }

    [AvaloniaFact]
    public void ProgressValueMatchesTheObserverAcrossItsThresholds()
    {
        var (_, scroller, target) = ShowScroller();
        var observer = PrestoAvalonia.OnScroll(new ScrollObserverParams { Target = target });
        Frames.Advance(16);
        var progress = observer.ProgressValue;

        // The target enters at 100 and leaves at 400.
        foreach (var offset in new double[] { 50, 100, 175, 250, 400, 300, 0 })
        {
            ScrollTo(scroller, 0, offset);
            Assert.Equal(observer.Progress, progress.Value);
        }
    }

    [AvaloniaFact]
    public void DraggableValuesFollowADragAndTheReleaseSpring()
    {
        var target = new Border { Width = 50, Height = 50, Background = Brushes.Red };
        var window = new Window { Content = new Canvas { Children = { target } }, Width = 300, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var draggable = PrestoAvalonia.CreateDraggable(target);
        var x = draggable.XValue;
        var y = draggable.YValue;

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(55, 45));
        Frames.Advance(16);

        Assert.Equal(30d, x.Value);
        Assert.Equal(20d, y.Value);

        window.MouseMove(new Point(95, 45));
        Frames.Advance(16);
        window.MouseUp(new Point(95, 45), MouseButton.Left);
        for (var i = 0; i < 100; i++)
        {
            Frames.Advance(16);
            Assert.Equal(draggable.X, x.Value);
            Assert.Equal(draggable.Y, y.Value);
        }
        Assert.True(x.Value > 70);
    }

    [AvaloniaFact]
    public void PointerValueFollowsPointerMoved()
    {
        var box = Box();
        var window = Show(box);
        var x = PrestoAvalonia.PointerValue(box, Orientation.Horizontal);
        var y = PrestoAvalonia.PointerValue(box, Orientation.Vertical, relativeToBounds: true);

        window.MouseMove(new Point(10, 40));

        Assert.Equal(10d, x.Value);
        Assert.Equal(.8, y.Value);

        window.MouseMove(new Point(30, 5));

        Assert.Equal(30d, x.Value);
        Assert.Equal(.1, y.Value);
    }

    [AvaloniaFact]
    public void ValueOfFollowsSetValue()
    {
        var box = Box();
        Show(box);
        var width = PrestoAvalonia.ValueOf(box, Layoutable.WidthProperty);

        Assert.Equal(50d, width.Value);

        box.SetValue(Layoutable.WidthProperty, 80d);

        Assert.Equal(80d, width.Value);
    }

    [AvaloniaFact]
    public void ValueOfAPropertyThatDoesNotConvertToADoubleThrows()
    {
        var box = Box();
        Show(box);
        box.Tag = "wide";
        Assert.Throws<FormatException>(() => PrestoAvalonia.ValueOf(box, Control.TagProperty));

        box.Tag = 3;
        var tag = PrestoAvalonia.ValueOf(box, Control.TagProperty);
        Assert.Equal(3d, tag.Value);
        Assert.Throws<InvalidCastException>(() => box.Tag = new object());
    }

    [AvaloniaFact]
    public void ScrollAndPropertyValuesStopWhenTheirControlDetaches()
    {
        var (window, scroller, _) = ShowScroller();
        var scroll = PrestoAvalonia.ScrollValue(scroller);
        var height = PrestoAvalonia.ValueOf(scroller, Layoutable.HeightProperty);
        ScrollTo(scroller, 0, 100);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        scroller.Offset = new Vector(0, 300);
        scroller.Height = 150;
        Frames.Advance(16);

        Assert.Equal(100d, scroll.Value);
        Assert.Equal(200d, height.Value);
        Assert.False(ScrollContainer.Containers.TryGetValue(scroller, out _));
    }

    [AvaloniaFact]
    public void PointerValueStopsWhenItsScopeReverts()
    {
        var box = Box();
        var window = Show(box);
        var scope = Presto.CreateScope();
        var x = scope.Execute(_ => PrestoAvalonia.PointerValue(box, Orientation.Horizontal));
        window.MouseMove(new Point(10, 10));

        scope.Revert();
        window.MouseMove(new Point(30, 10));

        Assert.Equal(10d, x.Value);
    }

    [AvaloniaFact]
    public void ObserverAndDraggableValuesFreezeOnRevert()
    {
        var (window, scroller, target) = ShowScroller();
        var observer = PrestoAvalonia.OnScroll(new ScrollObserverParams { Target = target });
        Frames.Advance(16);
        var progress = observer.ProgressValue;
        ScrollTo(scroller, 0, 250);
        var frozen = progress.Value;

        observer.Revert();
        ScrollTo(scroller, 0, 350);

        Assert.Equal(.5, frozen);
        Assert.Equal(frozen, progress.Value);

        var box = Box();
        window.Content = new Canvas { Children = { box } };
        Dispatcher.UIThread.RunJobs();
        var draggable = PrestoAvalonia.CreateDraggable(box);
        var x = draggable.XValue;
        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(55, 25));
        Frames.Advance(16);
        Assert.Equal(30d, x.Value);

        draggable.Revert();
        frozen = x.Value;
        window.MouseMove(new Point(85, 25));
        Frames.Advance(16);

        Assert.Equal(frozen, x.Value);
    }

    [AvaloniaFact]
    public void ASinkSeesOneChangePerFrameWhileScrollingSeveralNotches()
    {
        var (_, scroller, _) = ShowScroller();
        var doubled = PrestoAvalonia.ScrollValue(scroller).Map(v => v * 2);
        var seen = new List<double>();
        doubled.Changed += value => seen.Add(value.Value);

        for (var notch = 1; notch <= 3; notch++)
        {
            scroller.Offset = new Vector(0, notch * 50);
            Dispatcher.UIThread.RunJobs();
        }
        Frames.Advance(16);

        Assert.Equal([300d], seen);
    }
}
