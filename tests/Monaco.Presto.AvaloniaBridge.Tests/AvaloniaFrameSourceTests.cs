using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge.Tests;

public sealed class AvaloniaFrameSourceTests : BridgeTestBase
{
    /// <summary>Pumps the render timer, the dispatcher and real time until <paramref name="until"/> holds or <paramref name="rounds"/> run out.</summary>
    private static async Task<bool> Pump(Func<bool> until, int rounds = 120)
    {
        for (var i = 0; i < rounds && !until(); i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(16);
        }
        return until();
    }

    [AvaloniaFact]
    public async Task FramesComeFromTheTopLevelOfAnAnimatedControl()
    {
        var border = new Border();
        new Window { Content = border, Width = 100, Height = 100 }.Show();
        _ = border.Animate(new() { ["Opacity"] = 0, Duration = 100, Autoplay = false }); // seeds the source with a visual inside the window

        var source = new AvaloniaFrameSource();
        var frames = 0;
        source.Start(() => frames++);

        Assert.False(source.IsUsingFallbackTimer);
        Assert.True(await Pump(() => frames > 0), "no frame arrived from the top level");

        var delivered = frames;
        source.Stop();
        await Pump(() => false, 5);

        Assert.Equal(delivered, frames);
    }

    [AvaloniaFact]
    public void RestartingReusesTheFrameAlreadyRequested()
    {
        var border = new Border();
        new Window { Content = border, Width = 100, Height = 100 }.Show();
        _ = border.Animate(new() { ["Opacity"] = 0, Duration = 100, Autoplay = false }); // seeds the source with a visual inside the window

        var source = new AvaloniaFrameSource();
        var frames = 0;
        source.Start(() => frames++);
        source.Stop();
        source.Start(() => frames++);

        for (var i = 0; i < 4; i++)
        {
            var before = frames;
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            // The request left outstanding by Stop() would pump a second chain, delivering the frame twice.
            Assert.InRange(frames - before, 0, 1);
        }

        Assert.True(frames > 0, "no frame arrived after the restart");
    }

    /// <summary>The Android path: the lifetime hands out no view, so the top level can only come from an animated visual.</summary>
    [AvaloniaFact]
    public async Task TheFallbackTimerHandsThePumpOverOnceAnAnimatedVisualHasATopLevel()
    {
        AvaloniaFrameSource.Seed(new Border()); // an unparented visual, so no earlier test's window is found

        var source = new AvaloniaFrameSource();
        var frames = 0;
        source.Start(() => frames++);
        Assert.True(source.IsUsingFallbackTimer);

        var border = new Border();
        new Window { Content = border, Width = 100, Height = 100 }.Show();
        _ = border.Animate(new() { ["Opacity"] = 0, Duration = 100, Autoplay = false }); // seeds the source with a visual inside the window

        Assert.True(await Pump(() => !source.IsUsingFallbackTimer), "the pump stayed on the fallback timer");

        var delivered = frames;
        Assert.True(await Pump(() => frames > delivered), "no frame arrived after the handover");
        source.Stop();
    }

    [AvaloniaFact]
    public async Task StartFallsBackToADispatcherTimerWhenNoTopLevelExists()
    {
        AvaloniaFrameSource.Seed(new Border()); // an unparented visual, so no earlier test's window is found

        var source = new AvaloniaFrameSource();
        var frames = 0;
        source.Start(() => frames++);

        Assert.True(source.IsUsingFallbackTimer);
        Assert.True(await Pump(() => frames > 0), "the fallback timer delivered no frame");

        var delivered = frames;
        source.Stop();
        await Pump(() => false, 5);

        Assert.False(source.IsUsingFallbackTimer);
        Assert.Equal(delivered, frames);
    }

    [AvaloniaFact]
    public async Task AThrowingFrameCallbackDoesNotRetireTheTopLevelPump()
    {
        var border = new Border();
        new Window { Content = border, Width = 100, Height = 100 }.Show();
        _ = border.Animate(new() { ["Opacity"] = 0, Duration = 100, Autoplay = false }); // seeds the source with a visual inside the window

        var source = new AvaloniaFrameSource();
        var frames = 0;
        source.Start(() =>
        {
            if (++frames == 1) throw new InvalidOperationException("the first frame faults");
        });

        Assert.False(source.IsUsingFallbackTimer);
        Assert.True(await PumpThroughFaults(() => frames > 1), "the pump never recovered from the faulted frame");
        source.Stop();
    }

    [AvaloniaFact]
    public async Task AThrowingFrameCallbackDoesNotRetireTheFallbackTimer()
    {
        AvaloniaFrameSource.Seed(new Border()); // an unparented visual, so no earlier test's window is found

        var source = new AvaloniaFrameSource();
        var frames = 0;
        source.Start(() =>
        {
            if (++frames == 1) throw new InvalidOperationException("the first frame faults");
        });

        Assert.True(source.IsUsingFallbackTimer);
        Assert.True(await PumpThroughFaults(() => frames > 1), "the pump never recovered from the faulted frame");
        source.Stop();
    }

    /// <summary>Pumps as <see cref="Pump"/> does, swallowing the fault a frame callback deliberately throws through the tick.</summary>
    private static async Task<bool> PumpThroughFaults(Func<bool> until, int rounds = 120)
    {
        for (var i = 0; i < rounds && !until(); i++)
        {
            try
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
            catch (InvalidOperationException)
            {
            }
            await Task.Delay(16);
        }
        return until();
    }
}
