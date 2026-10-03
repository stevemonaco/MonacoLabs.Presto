using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace PrestoSample.Tests;

/// <summary>The gallery's sidebar pane, which its splitter resizes and Ctrl+B or its buttons collapse and expand.</summary>
public sealed class NavPaneTests
{
    [AvaloniaFact]
    public void TheSplitterIsClampedAndItsWidthIsRestoredAfterCollapsing() => WithView((window, column, _) =>
    {
        Drag(window, 400);
        Assert.Equal(420, column.ActualWidth);
        PressCtrlB(window);
        TestFrames.Tick(30);
        Assert.Equal(0, column.ActualWidth);
        PressCtrlB(window);
        TestFrames.Tick(30);
        Assert.Equal(420, column.ActualWidth);

        Drag(window, -400);
        Assert.Equal(180, column.ActualWidth);
        PressCtrlB(window);
        TestFrames.Tick(30);
        PressCtrlB(window);
        TestFrames.Tick(30);
        Assert.Equal(180, column.ActualWidth);
    });

    [AvaloniaFact]
    public void DoubleClickingTheSplitterCollapsesThePane() => WithView((window, column, _) =>
    {
        var splitter = window.GetLogicalDescendants().OfType<GridSplitter>().Single();
        var at = SceneHarness.Centre(splitter, window);
        for (var i = 0; i < 2; i++)
        {
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
        }
        TestFrames.Tick(30);
        Assert.Equal(0, column.ActualWidth);
    });

    [AvaloniaFact]
    public void AtItsNarrowestTheNavDoesNotScrollSideways() => WithView((window, _, sidebar) =>
    {
        Drag(window, -400);
        var scroller = sidebar.GetLogicalDescendants().OfType<TreeView>().Single().GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroller.Extent.Width <= scroller.Viewport.Width, $"The nav is {scroller.Extent.Width} wide in a {scroller.Viewport.Width} viewport.");
    });

    [AvaloniaFact]
    public void UnderReducedMotionTheCollapseLandsOnTheFirstFrame() => WithView((window, column, _) =>
    {
        PressCtrlB(window);
        TestFrames.Tick();
        Assert.Equal(0, column.ActualWidth);
    }, reduced: true);

    [AvaloniaFact]
    public void TheSidebarClipsRatherThanReflowsBelowItsMinimumWidth() => WithView((window, column, sidebar) =>
    {
        PressCtrlB(window);
        var clipped = false;
        for (var i = 0; i < 30 && column.ActualWidth > 0; i++)
        {
            TestFrames.Tick();
            if (column.ActualWidth is <= 0 or >= 180) continue;
            clipped = true;
            Assert.True(sidebar.Bounds.Width >= 180, $"The sidebar reflowed to {sidebar.Bounds.Width} in a {column.ActualWidth} column.");
            Assert.Equal(0, sidebar.Bounds.X);
        }
        Assert.True(clipped);
    });

    [AvaloniaFact]
    public void TheExpandButtonShowsOnlyWhileCollapsed() => WithView((window, _, _) =>
    {
        var expand = window.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == "ExpandPane");
        Assert.False(expand.IsEffectivelyVisible);

        PressCtrlB(window);
        TestFrames.Tick(30);
        Assert.True(expand.IsEffectivelyVisible);

        SceneHarness.Click(window, expand);
        TestFrames.Tick(30);
        Assert.False(expand.IsEffectivelyVisible);
    });

    [AvaloniaFact]
    public void CtrlFExpandsACollapsedPaneAndFocusesTheSearchBox() => WithView((window, column, _) =>
    {
        PressCtrlB(window);
        TestFrames.Tick(30);
        window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, null);
        TestFrames.Tick(30);

        Assert.Equal(240, column.ActualWidth);
        Assert.True(window.GetLogicalDescendants().OfType<TextBox>().Single(box => box.Name == "SearchBox").IsFocused);
    });
    private static void PressCtrlB(Window window) => window.KeyPress(Key.B, RawInputModifiers.Control, PhysicalKey.B, null);

    private static void Drag(Window window, double by)
    {
        var splitter = window.GetLogicalDescendants().OfType<GridSplitter>().Single();
        var from = SceneHarness.Centre(splitter, window);
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(from + new Point(by, 0), RawInputModifiers.LeftMouseButton);
        window.MouseUp(from + new Point(by, 0), MouseButton.Left);
        TestFrames.Tick();
    }

    private static void WithView(Action<Window, ColumnDefinition, Grid> test, bool reduced = false) => NavScene.With((window, _, _) =>
    {
        var grids = window.GetLogicalDescendants().OfType<Grid>().ToList();
        test(window, grids.Single(grid => grid.Name == "Shell").ColumnDefinitions[0], grids.Single(grid => grid.Name == "Sidebar"));
    }, reduced);
}
