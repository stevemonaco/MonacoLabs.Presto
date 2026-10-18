using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MonacoLabs.Presto.Controls;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="Drawer"/> (Phase 63c): a 120-wide drawer in a 200x200 window, swiped in by synthetic touch.</summary>
public sealed class DrawerTests : CompositorTestBase
{
    [AvaloniaTheory]
    [InlineData(Edge.Left, 5, 100)]
    [InlineData(Edge.Right, 195, -100)]
    public void AnEdgeSwipeOpensIt(Edge edge, double from, double by)
    {
        var (window, drawer) = Stage(edge);
        Assert.False(drawer.Content!.IsEffectivelyVisible);

        new Touch(window).Drag(Frames, new Point(from, 100), new Vector(by, 0));
        Frames.Advance(3000, 20);

        Assert.True(drawer.IsOpen);
        Assert.Equal(120, drawer.Position.Value, 1);
        var left = drawer.Content.TranslatePoint(default, window)!.Value.X;
        Assert.Equal(edge == Edge.Left ? 0 : 80, left, 1);
    }

    [AvaloniaFact]
    public void ASwipeAwayFromTheEdgeDoesNothing()
    {
        var (window, drawer) = Stage(Edge.Left);

        new Touch(window).Drag(Frames, new Point(60, 100), new Vector(100, 0));
        Frames.Advance(3000, 20);

        Assert.False(drawer.IsOpen);
        Assert.False(drawer.Content!.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void ADrawerDefaultsToTheLeftEdgeAndA20DipStrip()
    {
        var drawer = new Drawer();

        Assert.Equal(Edge.Left, drawer.Edge);
        Assert.Equal(20, drawer.EdgeSwipe);
    }

    [AvaloniaTheory]
    [InlineData(20, true)]
    [InlineData(0, false)]
    public void APlainSheetOpensOnAnEdgeSwipeOnlyWithAStrip(double swipe, bool opens)
    {
        var (window, sheet) = Stage(new Sheet { Edge = Edge.Left, EdgeSwipe = swipe });

        new Touch(window).Drag(Frames, new Point(5, 100), new Vector(100, 0));
        Frames.Advance(3000, 20);

        Assert.Equal(opens, sheet.IsOpen);
    }

    [AvaloniaFact]
    public void UnderReducedMotionAnEdgeSwipeLandsOpenAtOnce()
    {
        var (window, drawer) = Stage(Edge.Left);
        Presto.Defaults.ReducedMotion = true;

        new Touch(window).Drag(Frames, new Point(5, 100), new Vector(100, 0));

        Assert.True(drawer.IsOpen);
        Assert.Equal(120, drawer.Position.Value, 1);
    }

    private static (Window Window, Drawer Drawer) Stage(Edge edge) => Stage(new Drawer { Edge = edge });

    private static (Window Window, T Sheet) Stage<T>(T sheet) where T : Sheet
    {
        sheet.Detents = SheetDetents.Parse("0,120");
        sheet.Content = new Border { Background = Brushes.Red };
        var window = new Window { Content = new Panel { Children = { new Border { Background = Brushes.White }, sheet } }, Width = 200, Height = 200 };
        window.Show();
        Tick();
        return (window, sheet);
    }
}
