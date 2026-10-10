using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace MonacoLabs.Presto.Tests;

/// <summary>Covers PrestoAvalonia's animatable, adapting tests/suites/animatables.test.js to a control target.</summary>
public sealed class AnimatableBridgeTests : BridgeTestBase
{
    [AvaloniaFact]
    public void AnAnimatableDrivesTransformShorthandsAndABrush()
    {
        var border = new Border { Background = Brushes.Red };
        var animatable = border.CreateAnimatable(new()
        {
            ["X"] = 40,
            ["Y"] = new() { Duration = 0 },
            ["Background"] = 40,
            [Visual.OpacityProperty] = new() { Duration = 0 },
            Ease = "linear",
        });

        animatable.Set("X", 100).Set("Y", 150).Set("Background", [0, 0, 255, 1]).Set(Visual.OpacityProperty, .25);

        Assert.Equal(150d, PrestoAvalonia.Get(border, "Y"));
        Assert.Equal(.25, border.Opacity);
        Assert.Equal(.25, animatable.Get(Visual.OpacityProperty));
        Assert.NotEqual(100d, PrestoAvalonia.Get(border, "X"));

        Frames.Advance(60, 16);

        Assert.Equal(100d, PrestoAvalonia.Get(border, "X"));
        Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(border.Background).Color);
    }
}
