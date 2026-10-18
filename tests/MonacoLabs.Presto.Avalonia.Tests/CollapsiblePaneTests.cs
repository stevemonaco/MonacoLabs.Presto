using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Pane"/> (Phase 64e): a 120-wide column with a minimum of 80 beside a star column, its content in a clipping
/// cell, in a 300x100 window.
/// </summary>
public sealed class CollapsiblePaneTests : CompositorTestBase
{
    private const double MinWidth = 80;

    [AvaloniaFact]
    public void CollapsingEasesToZeroAndExpandingRestoresTheDraggedWidth()
    {
        var (column, content) = Stage();
        column.Width = new GridLength(150);
        Tick();

        Pane.Collapse(column, content);
        Run(3);
        Assert.InRange(column.ActualWidth, 1, 149);
        Run(40);
        Assert.Equal(0, column.ActualWidth);
        Assert.True(Pane.IsCollapsed(column));

        Pane.Expand(column, content);
        Run(3);
        Assert.InRange(column.ActualWidth, 1, 149);
        Run(60);
        Assert.Equal(150, column.ActualWidth);
        Assert.False(Pane.IsCollapsed(column));

        // The column ends on a local width, so a splitter's write takes effect.
        column.Width = new GridLength(130);
        Tick();
        Assert.Equal(130, column.ActualWidth);
    }

    [AvaloniaFact]
    public void MinWidthIsLiftedWhileTheWidthMovesAndRestoredOnceExpanded()
    {
        var (column, content) = Stage();

        Pane.Toggle(column, content);
        Assert.Equal(0, column.MinWidth);
        Run(40);
        Assert.Equal(0, column.MinWidth);

        Pane.Toggle(column, content);
        Run(3);
        Assert.Equal(0, column.MinWidth);
        Run(60);
        Assert.Equal(MinWidth, column.MinWidth);
        Assert.Equal(120, column.ActualWidth);
    }

    [AvaloniaFact]
    public void BelowItsMinimumTheContentKeepsItsWidthAndIsClipped()
    {
        var (column, content) = Stage();

        Pane.Collapse(column, content);
        var clipped = false;
        for (var i = 0; i < 40 && column.ActualWidth > 0; i++)
        {
            Run(1);
            if (column.ActualWidth is <= 0 or >= MinWidth) continue;
            clipped = true;
            Assert.True(content.Bounds.Width >= MinWidth, $"The content reflowed to {content.Bounds.Width} in a {column.ActualWidth} column.");
            Assert.Equal(0, content.Bounds.X);
        }
        Assert.True(clipped);
    }

    [AvaloniaFact]
    public void AColumnWithoutAMinWidthExpandsBackToItsWidth()
    {
        var (column, content) = Stage(minWidth: 0);

        Pane.Collapse(column, content);
        Run(40);
        Pane.Expand(column, content);
        Run(60);

        Assert.Equal(120, column.ActualWidth);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheWidthJumps()
    {
        Presto.Defaults.ReducedMotion = true;
        var (column, content) = Stage();

        Pane.Collapse(column, content);
        Run(1);
        Assert.Equal(0, column.ActualWidth);

        Pane.Expand(column, content);
        Run(1);
        Assert.Equal(120, column.ActualWidth);
        Assert.Equal(MinWidth, column.MinWidth);
    }

    [AvaloniaTheory]
    [InlineData(GridUnitType.Star)]
    [InlineData(GridUnitType.Auto)]
    public void AStarOrAutoColumnThrows(GridUnitType unit)
    {
        var (column, content) = Stage();
        column.Width = new GridLength(1, unit);
        Tick();

        Assert.Throws<ArgumentException>(() => Pane.Collapse(column, content));
    }

    private static (ColumnDefinition Column, Border Content) Stage(double minWidth = MinWidth)
    {
        var content = new Border { Background = Brushes.Red };
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(120)) { MinWidth = minWidth }, new ColumnDefinition(GridLength.Star) },
            Children = { new Border { ClipToBounds = true, Child = content } },
        };
        new Window { Content = grid, Width = 300, Height = 100 }.Show();
        Tick();
        return (grid.ColumnDefinitions[0], content);
    }
}
