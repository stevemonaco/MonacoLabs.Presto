using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Monaco.Presto.Tests;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Port of the 'Computes auto grid from DOM element positions' case of tests/suites/stagger.test.js.</summary>
public sealed class StaggerLayoutTests : BridgeTestBase
{
    private static Border[] LayOutGrid(int columns, int rows, double size)
    {
        var grid = new Grid();
        for (var column = 0; column < columns; column++) grid.ColumnDefinitions.Add(new ColumnDefinition(size, GridUnitType.Pixel));
        for (var row = 0; row < rows; row++) grid.RowDefinitions.Add(new RowDefinition(size, GridUnitType.Pixel));

        var cells = new Border[columns * rows];
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i] = new Border();
            Grid.SetColumn(cells[i], i % columns);
            Grid.SetRow(cells[i], i / columns);
            grid.Children.Add(cells[i]);
        }

        new Window { Content = grid, Width = columns * size, Height = rows * size }.Show();
        return cells;
    }

    private static double Delay(Timer animation, int index) => TestHelpers.GetTweenAtIndex(animation, index)!._delay;

    [AvaloniaFact]
    public void ComputesAutoGridFromLaidOutControlPositions()
    {
        var cells = LayOutGrid(5, 3, 20);

        var animation = Presto.Animate(cells, new AvaloniaAnimationParams
        {
            ["Opacity"] = 0,
            Delay = PrestoAvalonia.Stagger(10, new() { From = StaggerFrom.Center }),
            Autoplay = false,
        });

        Assert.Equal(0, Delay(animation, 7));

        Assert.Equal(Delay(animation, 6), Delay(animation, 8));
        Assert.Equal(Delay(animation, 5), Delay(animation, 9));
        Assert.Equal(Delay(animation, 2), Delay(animation, 12));

        var corner = Delay(animation, 0);
        Assert.Equal(corner, Delay(animation, 4));
        Assert.Equal(corner, Delay(animation, 10));
        Assert.Equal(corner, Delay(animation, 14));

        Assert.True(corner > Delay(animation, 6));
        Assert.True(corner > Delay(animation, 2));
    }

    [AvaloniaFact]
    public void ControlsWithoutALayoutShareTheOrigin()
    {
        var cells = new[] { new Border(), new Border(), new Border() };

        var animation = Presto.Animate(cells, new AvaloniaAnimationParams
        {
            ["Opacity"] = 0,
            Delay = PrestoAvalonia.Stagger(10, new() { From = StaggerFrom.Center }),
            Autoplay = false,
        });

        for (var i = 0; i < cells.Length; i++) Assert.Equal(0, Delay(animation, i));
    }
}
