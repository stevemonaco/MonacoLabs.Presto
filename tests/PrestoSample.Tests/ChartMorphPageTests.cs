using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase.Pages;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoSample.Tests;

public sealed class ChartMorphPageTests
{
    [AvaloniaFact]
    public void EveryPointSettlesInsideTheDonut()
    {
        var page = new ChartMorphPage();
        var window = SceneHarness.Show(page, 712, 440);
        var plot = page.GetControl<Canvas>("Plot");

        page.GetControl<TabStrip>("Views").SelectedIndex = 1;
        TestFrames.Tick(90);

        var radius = ChartMorphPage.DonutRadius;
        var donut = new Rect((plot.Width / 2) - radius, (plot.Height / 2) - radius, 2 * radius, 2 * radius).Inflate(.5);
        var points = plot.Children.OfType<Path>().Where(path => path.Name == null).ToList();
        Assert.Equal(8, points.Count);
        foreach (var point in points) Assert.True(donut.Contains(point.Data!.Bounds), $"{point.Data.Bounds} outside {donut}");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void SwitchingMidMorphEndsWithTheLineDrawn()
    {
        var page = new ChartMorphPage();
        var window = SceneHarness.Show(page, 712, 440);
        var views = page.GetControl<TabStrip>("Views");

        views.SelectedIndex = 2;
        TestFrames.Tick(4);
        views.SelectedIndex = 3;
        TestFrames.Tick(150);

        var draw = (double[])PrestoAvalonia.Get(page.GetControl<Path>("LineStroke"), "Draw")!;
        Assert.Equal(1, draw[1], 6);
        SceneHarness.Close(window);
    }
}
