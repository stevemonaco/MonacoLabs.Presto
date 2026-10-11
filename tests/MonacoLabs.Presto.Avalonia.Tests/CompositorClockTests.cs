using System.Diagnostics;
using Avalonia.Headless.XUnit;
using AvaloniaCompositor = Avalonia.Rendering.Composition.Compositor;

namespace MonacoLabs.Presto.Tests;

/// <summary>Pins the virtual-time seam (docs/ARCHITECTURE.md §3): tests advance the compositor's clock instead of letting real time pass.</summary>
public sealed class CompositorClockTests : CompositorTestBase
{
    [AvaloniaFact]
    public void AdvancingTheClockMovesACompositorAnimationByExactlyThatProgress()
    {
        var bar = ShowBar();
        bar.AnimateOnCompositor(new() { ["X"] = 60, Duration = 1000, Ease = "linear" });
        Tick();
        Tick();
        Assert.Equal(30d, Box(bar).Left);

        CompositorClock.Advance(250);
        Tick();
        Assert.Equal(45d, Box(bar).Left);

        CompositorClock.Advance(250);
        Tick();
        Assert.Equal(60d, Box(bar).Left);

        CompositorClock.Advance(1000);
        Tick();
        Assert.Equal(90d, Box(bar).Left);
    }

    [AvaloniaFact]
    public void AMissingSeamFailsLoudly()
    {
        var error = Assert.Throws<InvalidOperationException>(() => CompositorClock.Property(typeof(AvaloniaCompositor), "Clock"));
        Assert.Contains("Avalonia.Rendering.Composition.Compositor.Clock", error.Message);
        Assert.Throws<InvalidOperationException>(() => CompositorClock.Field(typeof(Stopwatch), "_ticks"));
    }

    /// <summary>Runs outside <see cref="CompositorTestBase"/>, which installs the clock for every test.</summary>
    public sealed class Uninstalled : BridgeTestBase
    {
        [AvaloniaFact]
        public void AdvancingWithoutInstallingFails() =>
            Assert.Throws<InvalidOperationException>(() => CompositorClock.Advance(16));
    }
}
