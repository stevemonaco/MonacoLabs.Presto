using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Covers <see cref="PrestoAvalonia.ScrollIntoView(Control, ScrollAlignment, Thickness, AnimationBuilder?)"/>, mostly on
/// <see cref="ScrollStage"/>'s 200 unit viewport with the target at 300 to 400 and the offset running 0 to 800.
/// </summary>
public sealed class ScrollIntoViewTests : BridgeTestBase
{
    [AvaloniaTheory]
    [InlineData(Orientation.Vertical, ScrollAlignment.Start, 300)]
    [InlineData(Orientation.Vertical, ScrollAlignment.Center, 250)]
    [InlineData(Orientation.Vertical, ScrollAlignment.End, 200)]
    [InlineData(Orientation.Vertical, ScrollAlignment.Nearest, 200)]
    [InlineData(Orientation.Horizontal, ScrollAlignment.Start, 300)]
    [InlineData(Orientation.Horizontal, ScrollAlignment.Center, 250)]
    [InlineData(Orientation.Horizontal, ScrollAlignment.End, 200)]
    [InlineData(Orientation.Horizontal, ScrollAlignment.Nearest, 200)]
    public void EachAlignmentLandsTheTargetWhereItSays(Orientation orientation, ScrollAlignment alignment, double offset)
    {
        var (_, scroller, target, _) = ScrollStage.Show(orientation, trail: 600);

        var animation = PrestoAvalonia.ScrollIntoView(target, alignment);
        Frames.Advance(1000, 16);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(offset, Along(scroller.Offset, orientation));
        Assert.Equal(ScrollStage.Lead - offset, Along(target.TranslatePoint(default, scroller)!.Value, orientation));
        Assert.True(animation.Completed);
    }

    [AvaloniaTheory]
    [InlineData(ScrollAlignment.Start, 280)]
    [InlineData(ScrollAlignment.End, 220)]
    public void TheMarginKeepsTheTargetClearOfTheEdge(ScrollAlignment alignment, double offset)
    {
        var (_, scroller, target, _) = ScrollStage.Show(trail: 600);

        PrestoAvalonia.ScrollIntoView(target, alignment, new Thickness(20));
        Frames.Advance(1000, 16);

        Assert.Equal(offset, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void NearestWithTheTargetInViewDoesNothingAndCompletesAtOnce()
    {
        var (_, scroller, target, _) = ScrollStage.Show(trail: 600);
        ScrollStage.ScrollTo(scroller, 250);

        var animation = PrestoAvalonia.ScrollIntoView(target);

        Assert.True(animation.Completed);
        Frames.Advance(100, 16);
        Assert.Equal(250, scroller.Offset.Y);
        Assert.Empty(Presto.GetAnimations(scroller, "ScrollY"));
    }

    [AvaloniaTheory]
    [InlineData(0, 200)]
    [InlineData(150, 200)]
    [InlineData(500, 300)]
    [InlineData(800, 300)]
    public void NearestScrollsTheLeastDistanceFromAboveOrBelow(double from, double offset)
    {
        var (_, scroller, target, _) = ScrollStage.Show(trail: 600);
        ScrollStage.ScrollTo(scroller, from);

        PrestoAvalonia.ScrollIntoView(target);
        Frames.Advance(1000, 16);

        Assert.Equal(offset, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void ATargetNearTheEndClampsToTheScrollableRange()
    {
        var (_, scroller, target, _) = ScrollStage.Show(trail: 50);

        PrestoAvalonia.ScrollIntoView(target, ScrollAlignment.Start);
        Frames.Advance(1000, 16);

        Assert.Equal(250, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void NestedScrollersMoveTogetherAndShowTheTargetInBoth()
    {
        var (_, outer, inner, target) = ShowNested();

        var animation = PrestoAvalonia.ScrollIntoView(target);
        Frames.Advance(16);
        Frames.Advance(16);
        Assert.InRange(inner.Offset.X, 1, 249);
        Assert.InRange(outer.Offset.Y, 1, 199);
        Frames.Advance(1000, 16);

        Assert.Equal(250, inner.Offset.X);
        Assert.Equal(200, outer.Offset.Y);
        Assert.True(animation.Completed);
        Dispatcher.UIThread.RunJobs();
        var inInner = target.TranslatePoint(default, inner)!.Value;
        var inOuter = target.TranslatePoint(default, outer)!.Value;
        Assert.InRange(inInner.X, 0, inner.Viewport.Width - 100);
        Assert.InRange(inOuter.Y, 0, outer.Viewport.Height - 100);
    }

    [AvaloniaFact]
    public void AWheelOnTheOuterScrollerStopsOnlyTheOuter()
    {
        var (window, outer, inner, target) = ShowNested();
        PrestoAvalonia.ScrollIntoView(target);
        Frames.Advance(160, 16);

        window.MouseWheel(new Point(50, 20), new Vector(0, -1));
        Dispatcher.UIThread.RunJobs();
        var left = outer.Offset.Y;
        Frames.Advance(1000, 16);

        Assert.NotEqual(200, left);
        Assert.Equal(left, outer.Offset.Y);
        Assert.Equal(250, inner.Offset.X);
    }

    [AvaloniaFact]
    public void ReducedMotionJumpsOnTheFirstFrameAndTheEngineIdles()
    {
        var (_, scroller, target, _) = ScrollStage.Show(trail: 600);
        Presto.Defaults.ReducedMotion = true;

        PrestoAvalonia.ScrollIntoView(target, ScrollAlignment.Start);
        Frames.Advance(16);

        Assert.Equal(300, scroller.Offset.Y);
        Frames.Advance(16);
        Assert.False(Presto.Engine.IsRunning);
    }

    private static double Along(Vector vector, Orientation orientation) => orientation == Orientation.Horizontal ? vector.X : vector.Y;

    /// <summary>
    /// A 200 by 200 vertical scroller over a 300 unit lead, a 150 wide horizontal scroller 100 tall and a 300 unit trail;
    /// the inner one holds a 100 wide target at 300, so it scrolls 0 to 550 and the outer 0 to 500.
    /// </summary>
    private static (Window Window, ScrollViewer Outer, ScrollViewer Inner, Border Target) ShowNested()
    {
        var target = new Border { Width = 100 };
        var inner = new ScrollViewer
        {
            Width = 150,
            Height = 100,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { new Border { Width = 300 }, target, new Border { Width = 300 } },
            },
        };
        var outer = new ScrollViewer
        {
            Width = 200,
            Height = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Content = new StackPanel { Children = { new Border { Height = 300 }, inner, new Border { Height = 300 } } },
        };
        var window = new Window { Content = outer, Width = 300, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, outer, inner, target);
    }
}
