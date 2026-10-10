using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using MonacoLabs.Presto;

namespace PrestoSample.Tests;

/// <summary>The guards of TEST-SPEED-PLAN §2.4: the harness steps virtual time and stops once nothing moves.</summary>
public sealed class SceneHarnessTests
{
    /// <summary>Pumping in real time, 120 frames would take about 1.9 s.</summary>
    [AvaloniaFact]
    public async Task PumpingNeverWaitsOnTheWallClock()
    {
        var window = SceneHarness.Show(new Border(), 100, 100);
        var started = Stopwatch.GetTimestamp();

        Assert.False(await SceneHarness.Pump(static () => false, rounds: 120));

        Assert.InRange(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 0, 500);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task SettleReturnsOnceTheLastAnimationEnds()
    {
        var bar = new Border { Background = Brushes.Red, Width = 40, Height = 20 };
        Canvas.SetLeft(bar, 30);
        var window = SceneHarness.Show(new Canvas { Background = Brushes.White, Children = { bar } }, 200, 100);
        var probe = new Probe();
        _ = Presto.Animate(probe, new() { ["Value"] = 1, Duration = 100, Ease = "linear" });
        // A bare composition animation, which, unlike AnimateOnCompositor, leaves nothing on the engine.
        var visual = ElementComposition.GetElementVisual(bar)!;
        var fade = visual.Compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1f, 0f);
        fade.Duration = TimeSpan.FromMilliseconds(300);
        visual.StartAnimation("Opacity", fade);
        var started = Presto.Engine.Now;

        await SceneHarness.Settle();

        Assert.Equal(1, probe.Value);
        Assert.Equal(0, SceneHarness.CountPixels(window, new Rect(30, 0, 40, 20), Colors.White));
        Assert.InRange(Presto.Engine.Now - started, 300, 30 * 16);
        SceneHarness.Close(window);
    }

    private sealed class Probe
    {
        public double Value { get; set; }
    }
}
