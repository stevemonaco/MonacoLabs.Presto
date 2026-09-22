namespace Monaco.Presto.Tests;

/// <summary>Port of anime.js parameters.test.js (frameRate and playbackRate cases) adapted to timers, plus the engine-wide Speed and Fps equivalents.</summary>
public sealed class ParametersTests : EngineTestBase
{
    [Fact]
    public void PlaybackRateOnTimer()
    {
        var timer = Presto.CreateTimer(new() { PlaybackRate = .5, Duration = 100 });
        double? probed = null;
        Presto.CreateTimer(new() { Duration = 100, OnComplete = _ => probed = timer.CurrentTime });

        RunUntil(() => probed != null);

        Assert.NotNull(probed);
        Assert.InRange(probed.Value, 40, 60);
    }

    [Fact]
    public void FrameRateThrottlesUpdates()
    {
        var updates = 0;
        Presto.CreateTimer(new() { FrameRate = 1, Duration = 10000, OnUpdate = _ => updates++ });

        Frames.Advance(1500, 16);

        Assert.InRange(updates, 1, 2);
    }

    [Fact]
    public void EngineSpeedDoublesTimerPlayback()
    {
        Presto.Engine.Speed = 2;
        double? completedAt = null;
        Presto.CreateTimer(new() { Duration = 100, OnComplete = _ => completedAt = Frames.Now });

        RunUntil(() => completedAt != null);

        Assert.NotNull(completedAt);
        Assert.InRange(completedAt.Value, 50 - 17, 50 + 17);
    }

    [Fact]
    public void ChangingEngineSpeedContinuesRunningTimersFromTheirCurrentTime()
    {
        var timer = Presto.CreateTimer(new() { Duration = 100 });
        Frames.Advance(40);
        Assert.Equal(52, timer.CurrentTime);

        Presto.Engine.Speed = 2;
        Assert.Equal(2, Presto.Engine._speed);
        Frames.Advance(16);

        Assert.Equal(52 + 16 * 2, timer.CurrentTime);
        Frames.Advance(16);
        Assert.True(timer.Completed);
    }

    [Fact]
    public void EngineFpsThrottlesUpdates()
    {
        var updates = 0;
        Presto.CreateTimer(new() { Duration = 10000, OnUpdate = _ => updates++ });
        Frames.Advance(100, 16);
        var updatesAt240Fps = updates;

        Presto.Engine.Fps = 30;
        updates = 0;
        Frames.Advance(100, 16);

        Assert.True(updatesAt240Fps >= 6, $"Expected every 16 ms frame to tick at 240 fps, got {updatesAt240Fps} updates.");
        Assert.InRange(updates, 1, updatesAt240Fps / 2);
    }
}
