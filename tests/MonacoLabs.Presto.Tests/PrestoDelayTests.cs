using System.Runtime.CompilerServices;

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

    [Fact]
    public async Task TheRegistrationIsDisposedWhenTheTimerIsInterrupted()
    {
        using var source = new CancellationTokenSource();
        var scope = Presto.CreateScope();
        var (delay, timer) = StartDelayInScope(scope, source.Token);
        Frames.Advance(16);

        scope.Revert();
        Frames.Advance(16);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delay);

        // The token's registration is the last thing that could hold the timer.
        for (var i = 0; i < 100 && timer.IsAlive; i++)
        {
            await Task.Delay(10, TestToken);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.False(timer.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private (Task Delay, WeakReference Timer) StartDelayInScope(Scope scope, CancellationToken token)
    {
        var delay = scope.Execute(_ => Presto.Delay(100, token));
        return (delay, new WeakReference(Presto.Engine._head));
    }

    [Fact]
    public async Task CompletingReleasesTheTokenRegistration()
    {
        using var source = new CancellationTokenSource();
        var status = new StrongBox<TaskStatus>();
        var timer = StartTrackedDelay(source.Token, status);

        Frames.Advance(300, 16);
        for (var i = 0; i < 100 && (timer.IsAlive || status.Value == default); i++)
        {
            await Task.Delay(10, TestToken);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.Equal(TaskStatus.RanToCompletion, status.Value);
        Assert.False(timer.IsAlive);
    }

    [Fact]
    public async Task ContinuationsNeverRunInsideTheEngineTick()
    {
        WakeEngine();
        var ticking = false;
        bool? ranInTick = null;
        using var context = new PumpingSynchronizationContext();
        var awaiting = AwaitDelay();

        ticking = true;
        Frames.Advance(200, 16);
        ticking = false;
        context.RunUntil(() => ranInTick != null, TimeSpan.FromSeconds(5));

        Assert.False(ranInTick);
        await awaiting;

        async Task AwaitDelay()
        {
            await Presto.Delay(100, TestToken);
            ranInTick = ticking;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference StartTrackedDelay(CancellationToken token, StrongBox<TaskStatus> status)
    {
        WeakReference? timer = null;
        Presto.Engine.TimerCreated = created => timer = new WeakReference(created);
        Presto.Delay(100, token).ContinueWith(task => status.Value = task.Status, TaskScheduler.Default);
        Presto.Engine.TimerCreated = null;
        return timer!;
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
