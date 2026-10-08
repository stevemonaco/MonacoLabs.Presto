using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Monaco.Presto.AvaloniaBridge.Compositor;
using PrestoSample.Pages;

namespace PrestoSample.Tests;

public sealed class CompositorPageTests
{
    [AvaloniaFact]
    public void ThePageRunsAndRevertsEverythingOnDetach()
    {
        var page = new CompositorPage();
        var window = SceneHarness.Show(page, 900, 700);
        var head = page.GetControl<Canvas>("UiOrbit").Children[^1];
        TestFrames.Tick(60);
        Assert.False(head.RenderTransform!.Value.IsIdentity, "the UI-thread comet never moved");
        Assert.Equal(4, page.Motions.Count);
        Assert.All(page.Motions, motion => Assert.True(motion.CurrentTime > 0, "a compositor control never started"));

        SceneHarness.Close(window);

        Assert.True(head.RenderTransform?.Value.IsIdentity ?? true, "the UI-thread comet kept its animated transform");
        Assert.All(page.Motions, motion => Assert.Equal(0, motion.CurrentTime));
    }

    [AvaloniaFact]
    public void EachThreadModeLaneReportsTheThreadItGot()
    {
        var page = new CompositorPage();
        SceneHarness.Show(page, 900, 1100);
        TestFrames.Tick(30);

        Assert.Equal("X + Blur · Blur doesn't fit", page.GetControl<TextBlock>("UnfitLabel").Text);
        Assert.Equal<string?>(["UI thread", "render thread", "render thread", "UI thread", "UI thread"], [.. page.ThreadReadouts.Where((_, i) => i != 4)]);
        Assert.StartsWith("'Blur' has no composition property.", page.ThreadReadouts[4]);
        Border[] dots = [.. page.GetControl<Grid>("ThreadGrid").GetLogicalDescendants().OfType<Border>().Where(b => b.Classes.Contains("dot"))];
        Assert.False(dots[5].RenderTransform!.Value.IsIdentity, "the fallback never moved on the UI thread");
        Assert.True(dots[4].RenderTransform?.Value.IsIdentity ?? true, "the forced compositor lane moved although it threw");
    }

    [AvaloniaFact]
    public async Task AStaleHeartbeatShowsTheUIThreadFrozenAndAFreshOneClearsIt()
    {
        var canvas = new CompositorCanvas { Scene = new OrbitScene(), Width = OrbitScene.Extent, Height = OrbitScene.Extent };
        SceneHarness.Show(canvas);

        canvas.Post(new OrbitScene.Heartbeat(Stopwatch.GetTimestamp() -Stopwatch.Frequency * 7 / 5));
        Assert.True(await SceneHarness.Pump(() => FrozenFor(canvas) >= 1400), "a heartbeat 1.4 s old did not read as frozen");

        canvas.Post(new OrbitScene.Heartbeat(Stopwatch.GetTimestamp()));
        Assert.True(await SceneHarness.Pump(() => FrozenFor(canvas) == 0), "a fresh heartbeat did not clear the freeze");

        // The heartbeat is a wall-clock timestamp, so only real time makes it stale; virtual frames take none.
        await Task.Delay(300);
        Assert.True(await SceneHarness.Pump(() => FrozenFor(canvas) > 0), "the scene never noticed the heartbeat stop");
    }

    // Read on the render thread, the only thread that may touch the scene.
    private static double FrozenFor(CompositorCanvas canvas)
    {
        var frozen = double.NaN;
        canvas.Invoke<OrbitScene>(scene => frozen = scene.FrozenFor);
        TestFrames.Tick();
        return frozen;
    }
}
