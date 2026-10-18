using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;
using MonacoLabs.Presto.Platform;

namespace MonacoLabs.Presto.Tests;

public sealed class AvaloniaFrameSourceTests : BridgeTestBase
{
    /// <summary>Pumps the render timer, the dispatcher and real time until <paramref name="until"/> holds or <paramref name="rounds"/> run out.</summary>
    private static async Task<bool> Pump(Func<bool> until, int rounds = 120)
    {
        for (var i = 0; i < rounds && !until(); i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            // The fallback is a real DispatcherTimer, which only fires once real time has passed.
            await Task.Delay(16);
        }
        return until();
    }

    [AvaloniaFact]
    public void NowFollowsTheTimeProvider()
    {
        var time = new FakeTimeProvider();
        var source = new AvaloniaFrameSource(time);

        time.Advance(TimeSpan.FromMilliseconds(250));

        Assert.Equal(250, source.Now);
    }

    private static async Task<List<(double Wall, double Now)>> RunJitteredFrames(AvaloniaFrameSource source, FakeTimeProvider time, int frameCount, Action<List<(double Wall, double Now)>>? afterFrame = null, bool stop = true)
    {
        var border = new Border();
        new Window { Content = border, Width = 100, Height = 100 }.Show();
        _ = border.Animate(new() { ["Opacity"] = 0, Duration = 100, Autoplay = false }); // seeds the source with a visual inside the window

        var frames = new List<(double Wall, double Now)>();
        source.Start(() => frames.Add(((time.GetUtcNow() - time.Start).TotalMilliseconds, source.Now)));
        double[] cycle = [4.3, 5.6, 6.8];
        for (var i = 0; i < frameCount; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(cycle[i % cycle.Length]));
            await DeliverFrame(frames);
            afterFrame?.Invoke(frames);
        }
        if (stop) source.Stop();
        return frames;
    }

    private static async Task DeliverFrame<T>(List<T> frames)
    {
        var delivered = frames.Count;
        for (var tries = 0; tries < 10 && frames.Count == delivered; tries++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            // A frame requested from inside a frame callback is only delivered once real time has passed.
            await Task.Delay(1);
        }
    }

    [AvaloniaFact]
    public async Task NowInsideATopLevelFrameIsThePacedFrameTime()
    {
        var time = new FakeTimeProvider();
        var source = new AvaloniaFrameSource(time);
        var frames = await RunJitteredFrames(source, time, 60, delivered =>
        {
            if (delivered.Count > 0)
                Assert.True(source.Now >= delivered[^1].Now, $"Now {source.Now} fell below the last frame time {delivered[^1].Now}");
        });

        var pacer = new FramePacer();
        Assert.All(frames, frame => Assert.Equal(pacer.Frame(frame.Wall), frame.Now));
        Assert.True(pacer.IsSettled, $"only {frames.Count} frames arrived");
        Assert.Contains(frames, frame => frame.Now != frame.Wall);
    }

    [AvaloniaFact]
    public async Task StartingAfterAStopRestartsTheGridAtTheWallClock()
    {
        var time = new FakeTimeProvider();
        var source = new AvaloniaFrameSource(time);
        var last = (await RunJitteredFrames(source, time, 30))[^1];

        // 2.75 intervals after the last frame time: a continued grid would round up and land after the wall clock.
        time.Advance(TimeSpan.FromMilliseconds(2.75 * 5.567 - 4.3 + (last.Now - last.Wall)));
        var (wall, now) = Assert.Single(await RunJitteredFrames(source, time, 1));

        Assert.Equal(wall, now);
    }

    [AvaloniaFact]
    public async Task SettingPacedAgainRestartsTheGridAtTheWallClock()
    {
        var time = new FakeTimeProvider();
        var source = new AvaloniaFrameSource(time);
        var frames = await RunJitteredFrames(source, time, 30, stop: false);
        var (count, last) = (frames.Count, frames[^1]);

        source.Paced = false;
        source.Paced = true;
        // 2.75 intervals after the last frame time: a continued grid would round up and land after the wall clock.
        time.Advance(TimeSpan.FromMilliseconds(2.75 * 5.567 + (last.Now - last.Wall)));
        await DeliverFrame(frames);
        source.Stop();

        Assert.True(frames.Count > count, "no frame arrived after the toggle");
        Assert.Equal(frames[count].Wall, frames[count].Now);
    }

    [AvaloniaFact]
    public async Task UnpacedFramesReportTheWallClock()
    {
        var time = new FakeTimeProvider();
        var frames = await RunJitteredFrames(new AvaloniaFrameSource(time) { Paced = false }, time, 30);

        Assert.NotEmpty(frames);
        Assert.All(frames, frame => Assert.Equal(frame.Wall, frame.Now));
    }

    [AvaloniaFact]
    public async Task TheFallbackTimerReportsTheWallClock()
    {
        AvaloniaFrameSource.Forget(); // so no earlier test's window is found

        var time = new FakeTimeProvider();
        var source = new AvaloniaFrameSource(time);
        var frames = new List<(double Wall, double Now)>();
        source.Start(() => frames.Add(((time.GetUtcNow() - time.Start).TotalMilliseconds, source.Now)));
        Assert.True(source.IsUsingFallbackTimer);

        Assert.True(await Pump(() =>
        {
            time.Advance(TimeSpan.FromMilliseconds(5.6));
            return frames.Count >= 3;
        }), "the fallback timer delivered too few frames");
        source.Stop();

        Assert.All(frames, frame => Assert.Equal(frame.Wall, frame.Now));
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
    public async Task StartingWhileStartedKeepsTheFirstCallback()
    {
        var border = new Border();
        new Window { Content = border, Width = 100, Height = 100 }.Show();
        _ = border.Animate(new() { ["Opacity"] = 0, Duration = 100, Autoplay = false }); // seeds the source with a visual inside the window

        var source = new AvaloniaFrameSource();
        var (first, second) = (0, 0);
        source.Start(() => first++);
        source.Start(() => second++);

        Assert.True(await Pump(() => first >= 2), "no frames arrived");
        source.Stop();
        Assert.Equal(0, second);
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
        AvaloniaFrameSource.Forget(); // so no earlier test's window is found

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
    public async Task ADetachedAnimatedVisualKeepsThePumpOnTheLastTopLevel()
    {
        var border = new Border();
        new Window { Content = border, Width = 100, Height = 100 }.Show();
        _ = border.Animate(new() { ["Opacity"] = 0, Duration = 100, Autoplay = false }); // seeds the source with a visual inside the window

        var source = new AvaloniaFrameSource();
        var frames = 0;
        source.Start(() => frames++);
        Assert.True(await Pump(() => frames > 0), "no frame arrived from the top level");

        _ = new TextBlock().Animate(new() { ["Opacity"] = 0, Duration = 100, Autoplay = false }); // the last animated visual is now detached
        var delivered = frames;

        Assert.True(await Pump(() => frames > delivered + 2), "no frame arrived after the detached visual was animated");
        Assert.False(source.IsUsingFallbackTimer);
        source.Stop();
    }

    [AvaloniaFact]
    public async Task StartFallsBackToADispatcherTimerWhenNoTopLevelExists()
    {
        AvaloniaFrameSource.Forget(); // so no earlier test's window is found

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
        AvaloniaFrameSource.Forget(); // so no earlier test's window is found

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
