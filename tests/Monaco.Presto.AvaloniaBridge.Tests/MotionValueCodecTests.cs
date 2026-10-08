using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.Tests;

public sealed class MotionValueCodecTests : BridgeTestBase
{
    private static object MapAt(double input, double[] range, object[] outputs) =>
        Presto.CreateMotionValue(input).Map(range, outputs).Value;

    [AvaloniaFact]
    public void ColorsInterpolateAcrossStops()
    {
        object[] stops = [Color.FromRgb(0, 0, 0), Color.FromRgb(200, 100, 50), Color.FromRgb(0, 200, 100)];

        Assert.Equal(Color.FromRgb(100, 50, 25), MapAt(50, [0, 100, 200], stops));
        Assert.Equal(Color.FromRgb(100, 150, 75), MapAt(150, [0, 100, 200], stops));
    }

    [AvaloniaFact]
    public void ThicknessInterpolatesEveryChannel() =>
        Assert.Equal(new Thickness(5, 10, 15, 20), MapAt(50, [0, 100], [new Thickness(0), new Thickness(10, 20, 30, 40)]));

    [AvaloniaFact]
    public void SolidBrushesInterpolateTheirColor()
    {
        var brush = Assert.IsAssignableFrom<ISolidColorBrush>(
            MapAt(50, [0, 100], [new SolidColorBrush(Color.FromRgb(0, 0, 0)), new SolidColorBrush(Color.FromRgb(200, 100, 50))]));

        Assert.Equal(Color.FromRgb(100, 50, 25), brush.Color);
    }

    [AvaloniaFact]
    public void TwoStopGradientsInterpolateTheirStops()
    {
        static LinearGradientBrush Gradient(Color start, Color end) => new()
        {
            GradientStops = { new GradientStop(start, 0), new GradientStop(end, 1) },
        };

        var brush = Assert.IsType<LinearGradientBrush>(MapAt(50, [0, 100],
            [Gradient(Color.FromRgb(0, 0, 0), Color.FromRgb(200, 200, 200)), Gradient(Color.FromRgb(200, 100, 50), Color.FromRgb(0, 0, 0))]));

        Assert.Equal(2, brush.GradientStops.Count);
        Assert.Equal(Color.FromRgb(100, 50, 25), brush.GradientStops[0].Color);
        Assert.Equal(Color.FromRgb(100, 100, 100), brush.GradientStops[1].Color);
    }

    [AvaloniaFact]
    public void OutputsOfDifferentKindsThrow()
    {
        var source = Presto.CreateMotionValue();

        Assert.Throws<ArgumentException>(() => source.Map([0, 1], [Colors.Red, new Thickness(1)]));
        Assert.Throws<ArgumentException>(() => source.Map([0, 1], [Colors.Red, "10px"]));
    }
}
