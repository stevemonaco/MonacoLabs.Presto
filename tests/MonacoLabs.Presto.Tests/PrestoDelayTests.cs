namespace MonacoLabs.Presto.Tests;

public sealed class PrestoDelayTests : EngineTestBase
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CompletesOnceTheDurationOfEngineTimeHasPassed()
    {
        WakeEngine();
        var delay = Presto.Delay(100, TestToken);

        // A playing timer starts 12 ms ahead (CORE-TIMER-037), and so does the delay's.
        Frames.Advance(87);
        Assert.False(delay.IsCompleted);

        Frames.Advance(1);
        Assert.True(delay.IsCompletedSuccessfully);
        await delay;
    }

    [Fact]
    public void TakesTwiceTheFramesAtHalfSpeed()
    {
        WakeEngine();
        Assert.Equal(6, FramesUntilComplete(Presto.Delay(100, TestToken)));

        Presto.Engine.Speed = 0.5;
        Assert.Equal(12,FramesUntilComplete(Presto.Delay(100, TestToken)));
    }

    [Fact]
    public async Task CancelingMidWaitCancelsTheTaskAndReleasesTheTimer()
    {
        using var source = new CancellationTokenSource();
        var delay = Presto.Delay(100, source.Token);
        Frames.Advance(16);
        Assert.NotNull(Presto.Engine._head);

        source.Cancel();

        Assert.True(delay.IsCanceled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delay);
        Frames.Advance(16);
        Assert.Null(Presto.Engine._head);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void ANonPositiveDurationIsCompleteOnReturn(double duration)
    {
        Assert.True(Presto.Delay(duration, TestToken).IsCompletedSuccessfully);
        Assert.Null(Presto.Engine._head);
    }

    [Fact]
    public void AnAlreadyCanceledTokenReturnsACanceledTaskWithoutATimer()
    {
        var delay = Presto.Delay(100, new CancellationToken(canceled: true));

        Assert.True(delay.IsCanceled);
        Assert.Null(Presto.Engine._head);
    }

    // Starts the delay mid-flight, past the engine's first-frame catch-up.
    private void WakeEngine()
    {
        Presto.CreateTimer(new() { Duration = 10000 });
        Frames.Advance(16);
    }

    private int FramesUntilComplete(Task delay)
    {
        var frames = 0;
        while (!delay.IsCompleted && frames < 1000)
        {
            Frames.Advance(16);
            frames++;
        }
        return frames;
    }
}
