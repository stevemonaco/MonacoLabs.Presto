using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge.Compositor;
using PrestoSample.Showcase.Pages;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Tests;

public sealed class LoadersPageTests
{
    [AvaloniaFact]
    public void ThePageRunsAndRevertsItsScopeOnDetach()
    {
        var page = new LoadersPage();
        var reverted = false;
        page.Scope.Add(_ => () => reverted = true);
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(60);
        Assert.Equal(9, page.Loaders.Count);
        Assert.All(page.Loaders, loader => Assert.True(loader.CurrentTime > 0));

        SceneHarness.Close(window);

        Assert.True(reverted);
    }

    [AvaloniaFact]
    public void TheRenderThreadToggleSwapsVariantsAndLeavesNothingRunning()
    {
        var page = new LoadersPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var renderThread = page.GetControl<ToggleSwitch>("RenderThread");
        var retired = new List<object>(page.Loaders);

        renderThread.IsChecked = true;
        TestFrames.Tick(90);
        Assert.Equal(3, page.Loaders.Count);
        Assert.Equal(6, page.CompositorLoaders.Count);
        Assert.All(page.CompositorLoaders, loader => Assert.True(loader.CurrentTime > 0));
        retired.AddRange(page.CompositorLoaders);

        renderThread.IsChecked = false;
        TestFrames.Tick(30);
        Assert.Equal(9, page.Loaders.Count);
        Assert.Empty(page.CompositorLoaders);
        retired.AddRange(page.Loaders);

        renderThread.IsChecked = true;
        TestFrames.Tick(90);
        Assert.All(page.CompositorLoaders, loader => Assert.True(loader.CurrentTime > 0));
        retired.AddRange(page.Loaders);
        retired.AddRange(page.CompositorLoaders);

        SceneHarness.Close(window);

        Assert.All(retired, loader => Assert.True(loader switch
        {
            Timer timer => timer.Cancelled,
            // Reverting rewinds the playback model to before its longest delay.
            CompositorAnimation compositor => compositor.CurrentTime <= 0,
            _ => false,
        }));
    }

    [AvaloniaFact]
    public void TheSpeedSliderRetunesEveryLoaderAndLeavesTheEngineAlone()
    {
        var page = new LoadersPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var engineSpeed = Presto.Engine.Speed;

        page.GetControl<Slider>("Speed").Value = 2.5;

        Assert.All(page.Loaders, loader => Assert.Equal(2.5, loader.Speed));
        Assert.Equal(engineSpeed, Presto.Engine.Speed);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void PauseAllPausesEveryLoaderAndResumesThem()
    {
        var page = new LoadersPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(10);
        var pauseAll = page.GetControl<ToggleSwitch>("PauseAll");

        pauseAll.IsChecked = true;
        var times = page.Loaders.Select(loader => loader.CurrentTime).ToList();
        TestFrames.Tick(20);

        Assert.All(page.Loaders, loader => Assert.True(loader.Paused));
        Assert.Equal(times, page.Loaders.Select(loader => loader.CurrentTime));

        pauseAll.IsChecked = false;
        TestFrames.Tick(5);
        Assert.All(page.Loaders, loader => Assert.False(loader.Paused));
        SceneHarness.Close(window);
    }
}
