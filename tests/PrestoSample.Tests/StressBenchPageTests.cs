using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge.Compositor;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class StressBenchPageTests
{
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EachModeRunsAndRevertsItsScopeOnDetach(int mode)
    {
        var page = new StressBenchPage();
        page.GetControl<ComboBox>("ModeBox").SelectedIndex = mode;
        page.GetControl<ComboBox>("SetBox").SelectedIndex = 2;
        page.GetControl<Slider>("CountSlider").Value = 300;
        var reverted = false;
        page.Scope.Add(_ => () => reverted = true);
        var window = SceneHarness.Show(page, 1100, 800);
        TestFrames.Tick(60);
        var workload = page.Workload;
        Assert.NotEmpty(workload);

        SceneHarness.Close(window);
        TestFrames.Tick();

        Assert.True(reverted);
        AssertStopped(workload);
        Assert.Empty(page.GetControl<Canvas>("Targets").Children);
    }

    [AvaloniaFact]
    public void SwitchingCountAndModeLeavesNothingOfThePreviousSetRunning()
    {
        var page = new StressBenchPage();
        page.GetControl<Slider>("CountSlider").Value = 300;
        var window = SceneHarness.Show(page, 1100, 800);
        var targets = page.GetControl<Canvas>("Targets");
        var mode = page.GetControl<ComboBox>("ModeBox");
        var setBox = page.GetControl<ComboBox>("SetBox");
        TestFrames.Tick(10);
        var uiThread = page.Workload;
        var uiTargets = targets.Children.ToList();

        page.GetControl<Slider>("CountSlider").Value = 500;
        TestFrames.Tick(10);
        AssertStopped(uiThread);
        Assert.Equal(500, targets.Children.Count);
        Assert.DoesNotContain(targets.Children, uiTargets.Contains);
        Assert.All(uiTargets, target => Assert.True(target.RenderTransform?.Value.IsIdentity ?? true));

        setBox.SelectedIndex = 2;
        mode.SelectedIndex = 1;
        TestFrames.Tick(10);
        var perVisual = page.Workload;
        Assert.Equal(2, perVisual.Count);

        mode.SelectedIndex = 2;
        TestFrames.Tick(10);
        var scene = Assert.IsType<BenchScene>(Assert.Single(page.Workload));
        AssertStopped(perVisual);
        Assert.Empty(targets.Children);

        mode.SelectedIndex = 0;
        TestFrames.Tick(10);
        AssertStopped([scene]);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task TheWorkerEngineRunsOnItsOwnThreadUntilSwitchedOff()
    {
        var page = new StressBenchPage();
        page.GetControl<Slider>("CountSlider").Value = 100;
        var window = SceneHarness.Show(page, 1100, 800);
        var toggle = page.GetControl<ToggleSwitch>("WorkerToggle");

        toggle.IsChecked = true;
        var worker = page.Workload.OfType<Engine>().Single();
        var readout = page.GetControl<TextBlock>("WorkerReadout");
        // The worker's engine runs on a real clock, which virtual frames do not move.
        for (var waited = Stopwatch.StartNew(); !readout.Text!.Contains("fps") && waited.Elapsed.TotalSeconds < 5;)
        {
            await Task.Delay(16);
            TestFrames.Tick();
        }
        Assert.True(readout.Text!.Contains("fps"), "the worker never reported");

        toggle.IsChecked = false;
        Assert.True(await SceneHarness.Pump(() => !worker.IsRunning), "the worker kept running once switched off");
        Assert.Empty(page.Workload.OfType<Engine>());
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task TheGraphReportsTheMeanAndNearestRankP95OfThePushedFrameTimes()
    {
        var canvas = new CompositorCanvas { Scene = new FrameGraphScene(), Width = 720, Height = 120 };
        SceneHarness.Show(canvas);

        // 1..100 ms: mean 50.5, and the 95th of 100 sorted samples is 95.
        foreach (var ms in Enumerable.Range(1, 100).Reverse()) canvas.Post(new FrameGraphScene.FrameTime(ms));
        Assert.Equal((50.5, 95d), await Stats(canvas));

        // Past capacity the oldest samples drop out: 240 frames of 10 ms leave nothing else.
        for (var i = 0; i < FrameGraphScene.Capacity; i++) canvas.Post(new FrameGraphScene.FrameTime(10));
        Assert.Equal((10d, 10d), await Stats(canvas));
    }

    private static void AssertStopped(IEnumerable<object> workload)
    {
        foreach (var item in workload)
        {
            switch (item)
            {
                case Animation animation:
                    Assert.True(animation.Canceled, "a UI-thread animation of the previous set is still running");
                    break;
                case CompositorAnimation compositor:
                    // Reverting rewinds the playback model to before its longest delay.
                    Assert.True(compositor.CurrentTime <= 0, "a compositor animation of the previous set is still running");
                    break;
                case BenchScene scene:
                    Assert.False(scene.Engine.IsRunning, "the previous bench scene is still ticking");
                    break;
            }
        }
    }

    // Read on the render thread, the only thread that may touch the scene.
    private static async Task<(double Mean, double P95)> Stats(CompositorCanvas canvas)
    {
        var stats = (double.NaN, double.NaN);
        canvas.Invoke<FrameGraphScene>(scene => stats = (scene.Mean, scene.P95));
        await SceneHarness.Pump(() => !double.IsNaN(stats.Item1));
        return stats;
    }
}
