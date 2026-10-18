using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Events;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Covers <see cref="Scroll.CueForProperty"/>, <see cref="Scroll.CueEdgeProperty"/>, <see cref="Scroll.CueRampProperty"/> and
/// <see cref="Scroll.FadeEdgesProperty"/>.
/// </summary>
/// <remarks>The <see cref="ScrollStage"/> scroller holds 700 units in a 200 unit viewport, so it scrolls to 500.</remarks>
public sealed class ScrollEdgeCueTests : BridgeTestBase
{
    private const double End = 500;

    private static (ScrollViewer Scroller, Border Cue) Show(ScrollEdge edge, Orientation orientation = Orientation.Vertical)
    {
        var stage = ScrollStage.Show(orientation);
        var cue = Cue(stage.Scroller, edge);
        stage.Panel.Children.Add(cue);
        Dispatcher.UIThread.RunJobs();
        return (stage.Scroller, cue);
    }

    private static Border Cue(Control target, ScrollEdge edge)
    {
        var cue = new Border();
        Scroll.SetCueEdge(cue, edge);
        Scroll.SetCueFor(cue, target);
        return cue;
    }

    private static void AssertOpacities(ScrollViewer scroller, Border cue, Orientation orientation, params (double Offset, double Opacity)[] expected)
    {
        foreach (var (offset, opacity) in expected)
        {
            scroller.Offset = orientation == Orientation.Horizontal ? new Vector(offset, 0) : new Vector(0, offset);
            Assert.Equal(opacity, cue.Opacity, 6);
        }
    }

    private static LinearGradientBrush Mask(Control control) => Assert.IsType<LinearGradientBrush>(control.OpacityMask);

    private static void AssertEnds(Control control, double leading, double trailing)
    {
        var stops = Mask(control).GradientStops;
        Assert.Equal(leading, stops[0].Color.A / 255d, 2);
        Assert.Equal(trailing, stops[3].Color.A / 255d, 2);
    }

    private static void AssertRamp(Control control, double ramp)
    {
        var stops = Mask(control).GradientStops;
        Assert.Equal(ramp, stops[1].Offset, 6);
        Assert.Equal(1 - ramp, stops[2].Offset, 6);
    }

    private static byte Green(Window window, int x, int y)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var locked = frame.Lock();
        return Marshal.ReadByte(locked.Address, (y * locked.RowBytes) + (x * 4) + 1);
    }

    [AvaloniaFact]
    public void ABottomCueIsFullAtTheTopAndRampsOutOverTheLast24Units()
    {
        var (scroller, cue) = Show(ScrollEdge.Bottom);

        Assert.Equal(1, cue.Opacity);
        AssertOpacities(scroller, cue, Orientation.Vertical, (300, 1), (End - 24, 1), (End - 12, .5), (End - 6, .25), (End, 0));
    }

    [AvaloniaFact]
    public void ATopCueMirrorsIt()
    {
        var (scroller, cue) = Show(ScrollEdge.Top);

        Assert.Equal(0, cue.Opacity);
        AssertOpacities(scroller, cue, Orientation.Vertical, (6, .25), (12, .5), (24, 1), (End, 1), (0, 0));
    }

    [AvaloniaFact]
    public void LeftAndRightCuesFollowAHorizontalScroller()
    {
        var (scroller, left) = Show(ScrollEdge.Left, Orientation.Horizontal);
        var right = Cue(scroller, ScrollEdge.Right);
        ((StackPanel)scroller.Content!).Children.Add(right);

        AssertOpacities(scroller, left, Orientation.Horizontal, (6, .25), (End, 1));
        AssertOpacities(scroller, right, Orientation.Horizontal, (6, 1), (End - 6, .25), (End, 0));
    }

    [AvaloniaFact]
    public void ContentNoLongerThanTheViewportCuesNothing()
    {
        var scroller = new ScrollViewer { Height = 200, Content = new Border { Height = 100 } };
        var top = Cue(scroller, ScrollEdge.Top);
        var bottom = Cue(scroller, ScrollEdge.Bottom);
        Show(new Panel { Children = { scroller, top, bottom } });

        Assert.Equal(0, top.Opacity);
        Assert.Equal(0, bottom.Opacity);
    }

    [AvaloniaFact]
    public void AListBoxCueFollowsItsTemplatedScrollerAndIgnoresOneInsideAnItem()
    {
        var list = new ListBox
        {
            Height = 200,
            ItemsSource = Enumerable.Range(0, 20).ToArray(),
            ItemTemplate = new FuncDataTemplate<int>((i, _) => i == 0
                ? new ScrollViewer { Height = 50, Content = new Border { Height = 400 } }
                : new Border { Height = 50 }),
        };
        var cue = Cue(list, ScrollEdge.Top);
        Show(new StackPanel { Children = { cue, list } });
        var scrollers = list.GetVisualDescendants().OfType<ScrollViewer>().ToList();
        var own = scrollers.Single(scroller => scroller.TemplatedParent == list);
        var inner = scrollers.Single(scroller => scroller != own);

        inner.Offset = new Vector(0, 100);
        Assert.Equal(0, cue.Opacity);

        own.Offset = new Vector(0, 12);
        Assert.Equal(.5, cue.Opacity, 6);

        Scroll.SetCueRamp(list, 48);
        Assert.Equal(.25, cue.Opacity, 6);
    }

    [AvaloniaFact]
    public void ClearingCueForRestoresTheLocalOpacityAndReleasesTheContainer()
    {
        var stage = ScrollStage.Show();
        var scroller = stage.Scroller;
        var cue = new Border { Opacity = .4 };
        Scroll.SetCueFor(cue, scroller);
        stage.Panel.Children.Add(cue);
        Assert.Equal(0, cue.Opacity);
        scroller.Offset = new Vector(0, 300);
        Assert.Equal(1, cue.Opacity);

        cue.ClearValue(Scroll.CueForProperty);

        Assert.Equal(.4, cue.Opacity);
        Assert.False(ScrollContainer.Containers.TryGetValue(scroller, out _));
    }

    [AvaloniaFact]
    public void DetachingClearsTheOpacityAndReleasesTheContainer()
    {
        var (scroller, cue) = Show(ScrollEdge.Bottom);
        Assert.Equal(1, cue.Opacity);
        scroller.Offset = new Vector(0, End);
        Assert.Equal(0, cue.Opacity);

        ((StackPanel)scroller.Content!).Children.Remove(cue);

        Assert.Equal(1, cue.Opacity);
        Assert.False(ScrollContainer.Containers.TryGetValue(scroller, out _));
    }

    [AvaloniaFact]
    public void ChangingTheEdgeAppliesAtOnce()
    {
        var (_, cue) = Show(ScrollEdge.Top);

        Scroll.SetCueEdge(cue, ScrollEdge.Bottom);

        Assert.Equal(1, cue.Opacity);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheCueStillFollows()
    {
        Presto.Defaults.ReducedMotion = true;
        var (scroller, cue) = Show(ScrollEdge.Bottom);

        AssertOpacities(scroller, cue, Orientation.Vertical, (End - 12, .5), (End, 0));
    }

    [AvaloniaFact]
    public void TheCueCanBeSetInXamlAndStyles()
    {
        var view = new ScrollEdgeCueXamlView();
        Show(view);
        var scroller = view.FindControl<ScrollViewer>("Scroller")!;
        var shadow = view.FindControl<Border>("Shadow")!;

        scroller.Offset = new Vector(0, End - 12);
        Assert.Equal(.25, shadow.Opacity, 6);
        AssertEnds(scroller, 0, .75);

        Scroll.SetCueFor(shadow, null);

        Assert.Equal(.3, shadow.Opacity, 6);
    }

    [AvaloniaFact]
    public void TheMaskRunsDownAVerticalScrollerAndAcrossOneThatOnlyScrollsSideways()
    {
        var vertical = ScrollStage.Show().Scroller;
        var horizontal = ScrollStage.Show(Orientation.Horizontal).Scroller;

        Scroll.SetFadeEdges(vertical, 24);
        Scroll.SetFadeEdges(horizontal, 24);

        Assert.Equal(new RelativePoint(0, 0, RelativeUnit.Relative), Mask(vertical).StartPoint);
        Assert.Equal(new RelativePoint(0, 1, RelativeUnit.Relative), Mask(vertical).EndPoint);
        Assert.Equal(new RelativePoint(0, 0, RelativeUnit.Relative), Mask(horizontal).StartPoint);
        Assert.Equal(new RelativePoint(1, 0, RelativeUnit.Relative), Mask(horizontal).EndPoint);
        horizontal.Offset = new Vector(12, 0);
        AssertEnds(horizontal, .5, 0);
    }

    [AvaloniaFact]
    public void EachEndOfTheMaskFadesByTheDistancePastIt()
    {
        var scroller = ScrollStage.Show().Scroller;
        Assert.Null(scroller.OpacityMask);

        Scroll.SetFadeEdges(scroller, 24);

        AssertRamp(scroller, 24 / ScrollStage.ViewportSize);
        AssertEnds(scroller, 1, 0);
        scroller.Offset = new Vector(0, 12);
        AssertEnds(scroller, .5, 0);
        scroller.Offset = new Vector(0, 250);
        AssertEnds(scroller, 0, 0);
        scroller.Offset = new Vector(0, End - 6);
        AssertEnds(scroller, 0, .75);
        scroller.Offset = new Vector(0, End);
        AssertEnds(scroller, 0, 1);
    }

    [AvaloniaFact]
    public void AListBoxMaskFollowsItsTemplatedScroller()
    {
        var list = new ListBox { Height = 200, ItemsSource = Enumerable.Range(0, 20).ToArray(), ItemTemplate = new FuncDataTemplate<int>((_, _) => new Border { Height = 50 }) };
        Scroll.SetFadeEdges(list, 24);
        Show(list);
        var scroller = list.GetVisualDescendants().OfType<ScrollViewer>().Single(scroller => scroller.TemplatedParent == list);

        AssertEnds(list, 1, 0);
        scroller.Offset = new Vector(0, 12);
        AssertEnds(list, .5, 0);
        scroller.Offset = new Vector(0, scroller.Extent.Height - scroller.Viewport.Height - 6);
        AssertEnds(list, 0, .75);
    }

    [AvaloniaFact]
    public void TheFadeIsCappedAtHalfTheLengthAndFollowsItsChanges()
    {
        var scroller = ScrollStage.Show().Scroller;
        Scroll.SetFadeEdges(scroller, 24);
        var mask = Mask(scroller);

        Scroll.SetFadeEdges(scroller, 150);
        AssertRamp(scroller, .5);

        Scroll.SetFadeEdges(scroller, 50);
        AssertRamp(scroller, .25);

        scroller.Height = 80;
        Dispatcher.UIThread.RunJobs();
        AssertRamp(scroller, .5);
        Assert.Same(mask, scroller.OpacityMask);
    }

    [AvaloniaFact]
    public void TheMaskReplacesTheControlsOwnAndReturningToZeroClearsItAndReleasesTheContainer()
    {
        var scroller = ScrollStage.Show().Scroller;
        var own = new SolidColorBrush(Colors.Black);
        scroller.OpacityMask = own;

        Scroll.SetFadeEdges(scroller, 24);
        Assert.NotSame(own, scroller.OpacityMask);

        Scroll.SetFadeEdges(scroller, 0);

        Assert.Null(scroller.OpacityMask);
        Assert.False(ScrollContainer.Containers.TryGetValue(scroller, out _));
    }

    [AvaloniaFact]
    public void DetachingClearsTheMaskAndReleasesTheContainer()
    {
        var stage = ScrollStage.Show();
        Scroll.SetFadeEdges(stage.Scroller, 24);

        stage.Window.Content = null;

        Assert.Null(stage.Scroller.OpacityMask);
        Assert.False(ScrollContainer.Containers.TryGetValue(stage.Scroller, out _));
    }

    [AvaloniaFact]
    public void AMaskMutatedInPlaceRendersWithoutAScroll()
    {
        var stage = ScrollStage.Show();
        stage.Window.Background = Brushes.White;
        stage.Panel.Background = Brushes.Red;
        Scroll.SetFadeEdges(stage.Scroller, 24);
        var mask = Mask(stage.Scroller);
        Assert.Equal(0, Green(stage.Window, 50, 2));
        Assert.InRange(Green(stage.Window, 50, 197), 200, 255);

        // 500 past the bottom over a ramp of 1000 leaves the bottom end half faded.
        Scroll.SetCueRamp(stage.Scroller, 1000);

        Assert.Same(mask, stage.Scroller.OpacityMask);
        Assert.InRange(Green(stage.Window, 50, 197), 80, 150);
    }
}
