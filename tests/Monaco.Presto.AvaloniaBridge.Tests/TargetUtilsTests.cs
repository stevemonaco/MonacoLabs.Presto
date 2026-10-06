using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Headless.XUnit;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Covers the PrestoAvalonia get/set/remove overloads, adapting tests/suites/utils.test.js to AvaloniaObject targets.</summary>
public sealed class TargetUtilsTests : BridgeTestBase
{
    [AvaloniaFact]
    public void GetReadsBackAComposedTransformShorthand()
    {
        var border = new Border();
        border.Animate(new() { ["X"] = 100, ["Rotate"] = 90, Duration = 100, Autoplay = false }).Seek(100);

        Assert.Equal(100d, PrestoAvalonia.Get(border, "X"));
        Assert.Equal("0.25turn", PrestoAvalonia.Get(border, "Rotate", "turn"));
    }

    [AvaloniaFact]
    public void GetResolvesATypedProperty()
    {
        var border = new Border { Opacity = .25 };

        Assert.Equal(.25d, PrestoAvalonia.Get(border, Visual.OpacityProperty));
    }

    [AvaloniaFact]
    public void SetWritesAtAnimationPriorityAndRevertUncoversTheLocalValue()
    {
        var border = new Border { Opacity = .25 };

        var set = PrestoAvalonia.Set(border, new() { ["Opacity"] = 1 });

        Assert.Equal(1, border.Opacity);
        Assert.Equal(BindingPriority.Animation, border.GetDiagnostic(Visual.OpacityProperty).Priority);

        set.Revert();

        Assert.Equal(.25, border.Opacity);
    }

    [AvaloniaFact]
    public void RemoveStopsAnAnimationFromWritingATarget()
    {
        var border = new Border { Opacity = 0 };
        var animation = border.Animate(new() { [Visual.OpacityProperty] = 1, Duration = 100, Ease = "linear" });
        Frames.Advance(50, 16);
        var stopped = border.Opacity;

        var removed = PrestoAvalonia.Remove(border, Visual.OpacityProperty);
        Frames.Advance(100, 16);

        Assert.Equal([border], removed);
        Assert.True(animation.Canceled);
        Assert.Equal(stopped, border.Opacity);
    }
}
