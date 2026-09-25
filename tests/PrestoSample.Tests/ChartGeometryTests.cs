using Avalonia;
using Avalonia.Headless.XUnit;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class ChartGeometryTests
{
    public static TheoryData<string> Shapes => ["bar", "wedge", "big wedge", "circle"];

    [AvaloniaTheory]
    [MemberData(nameof(Shapes))]
    public void EachBuilderReturnsAClosedContour(string shape)
    {
        var geometry = shape switch
        {
            "bar" => ChartGeometry.Bar(50, 20, 200, 34, 6),
            "wedge" => ChartGeometry.Wedge(new Point(100, 100), 40, 80, 30, 60),
            "big wedge" => ChartGeometry.Wedge(new Point(100, 100), 40, 80, 10, 250),
            _ => ChartGeometry.Circle(new Point(100, 100), 12),
        };

        Assert.True(geometry.ContourLength > 0);
        geometry.TryGetPointAtDistance(0, out var first);
        geometry.TryGetPointAtDistance(geometry.ContourLength, out var last);
        Assert.Equal(first.X, last.X, 3);
        Assert.Equal(first.Y, last.Y, 3);
    }

    [Fact]
    public void WedgeSweepsSumTo360()
    {
        var sweeps = ChartGeometry.Sweeps([.. Datasets.ChartPoints[0].Select(point => point.Value)]);

        Assert.Equal(360, sweeps.Sum(s => s.Sweep), 9);
        for (var i = 1; i < sweeps.Length; i++) Assert.Equal(sweeps[i - 1].Start + sweeps[i - 1].Sweep, sweeps[i].Start, 9);
    }
}
