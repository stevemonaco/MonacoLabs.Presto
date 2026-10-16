namespace MonacoLabs.Presto.Tests;

/// <summary>Port of anime.js promises.test.js plus the C# awaitable.</summary>
public sealed class PromisesTests : EngineTestBase
{
    [Fact]
    public async Task ThenOnTimer()
    {
        var callbacks = 0;
        double? currentTimeInCallback = null;
        var task = Presto.CreateTimer(new() { Duration = 30 }).Then(self =>
        {
            callbacks++;
            currentTimeInCallback = self.CurrentTime;
        });
        Assert.False(task.IsCompleted);

        RunUntil(() => task.IsCompleted);
        var timer = await task;

        Assert.Equal(30, timer.CurrentTime);
        Assert.Equal(30, currentTimeInCallback);
        Assert.Equal(1, callbacks);
    }

    [Fact]
    public async Task ThenOnAnimation()
    {
        double? currentTimeInCallback = null;
        var animation = Presto.Animate(new StyleTarget(), new() { ["Y"] = 100, Duration = 30 });
        var task = animation.Then(self => currentTimeInCallback = self.CurrentTime);

        RunUntil(() => currentTimeInCallback != null);

        Assert.Same(animation, await task);
        Assert.Equal(30, currentTimeInCallback);
    }

    [Fact]
    public async Task ThenOnTimeline()
    {
        var target = new StyleTarget();
        double? currentTimeInCallback = null;
        var tl = Presto.CreateTimeline(new())
            .Add(target, new() { ["X"] = 100, Duration = 15 })
            .Add(target, new() { ["Y"] = 100, Duration = 15 });
        var task = tl.Then(self => currentTimeInCallback = self.CurrentTime);

        RunUntil(() => task.IsCompleted);

        Assert.Same(tl, await task);
        Assert.Equal(30, currentTimeInCallback);
    }

    [Fact]
    public async Task AwaitAnAnimation()
    {
        static async Task<Animation> Await(Animation animation) => await animation;

        var animation = Presto.Animate(new StyleTarget(), new() { ["Y"] = 100, Duration = 30 });
        var pending = Await(animation);
        Assert.False(pending.IsCompleted);

        RunUntil(() => animation.Completed);

        Assert.Same(animation, await pending);
        Assert.Equal(30, animation.CurrentTime);
    }

    [Fact]
    public async Task ThenOnAnAlreadyCompletedTimerResolvesImmediately()
    {
        var timer = Presto.CreateTimer(new() { Duration = 30 });
        RunUntil(() => timer.Completed);
        var called = false;

        var task = timer.Then(_ => called = true);

        Assert.True(called);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Same(timer, await task);
    }

    [Fact]
    public async Task AwaitATimer()
    {
        static async Task<Timer> Await(Timer timer) => await timer;

        var timer = Presto.CreateTimer(new() { Duration = 30 });
        var pending = Await(timer);
        Assert.False(pending.IsCompleted);

        RunUntil(() => timer.Completed);

        Assert.Same(timer, await pending);
        Assert.Equal(30, timer.CurrentTime);
    }

    [Fact]
    public async Task ARestartedTimerResolvesANewThen()
    {
        var timer = Presto.CreateTimer(new() { Duration = 30 });
        var first = timer.Then();
        RunUntil(() => first.IsCompleted);

        _ = timer.Restart();
        var second = timer.Then();
        Assert.NotSame(first, second);
        Assert.False(second.IsCompleted);

        RunUntil(() => second.IsCompleted);

        Assert.Same(timer, await second);
        Assert.Equal(30, timer.CurrentTime);
    }

    [Fact]
    public void AThrowingThenCallbackStillCompletesTheTask()
    {
        var timer = Presto.CreateTimer(new() { Duration = 30 });
        var task = timer.Then(_ => throw new InvalidOperationException("callback"));

        Assert.Throws<InvalidOperationException>(() => RunUntil(() => timer.Completed));

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public void CancelRevertAndAMutedCompleteLeaveAPendingThenIncomplete()
    {
        var canceled = Presto.Animate(new StyleTarget(), new() { ["Y"] = 100, Duration = 30 });
        var reverted = Presto.Animate(new StyleTarget(), new() { ["Y"] = 100, Duration = 30 });
        var completed = Presto.Animate(new StyleTarget(), new() { ["Y"] = 100, Duration = 30 });
        var tasks = new[] { canceled.Then(), reverted.Then(), completed.Then() };

        canceled.Cancel();
        reverted.Revert();
        completed.Complete(muteCallbacks: true);
        RunUntil(() => false, max: 100);

        Assert.All(tasks, task => Assert.False(task.IsCompleted));
    }

    [Fact]
    public void AnAnimationReplacedByCompositionLeavesItsThenIncomplete()
    {
        var target = new StyleTarget();
        var first = Presto.Animate(target, new() { ["Y"] = 100, Duration = 30 });
        var task = first.Then();
        Frames.Advance(16);

        var second = Presto.Animate(target, new() { ["Y"] = 0, Duration = 30 });
        RunUntil(() => second.Completed);

        Assert.True(first.Canceled);
        Assert.False(task.IsCompleted);
    }

    /// <summary>Real time rather than the manual clock: the default pump ticks the engine on the test thread through its SynchronizationContext and resolves the awaitable.</summary>
    [Fact]
    public void ThenResolvesOnTheDefaultFramePump()
    {
        using var context = new PumpingSynchronizationContext();
        Presto.Engine.FrameSource = new StopwatchFrameSource();
        var timer = Presto.CreateTimer(new() { Duration = 30 });
        Timer? resolved = null;
        var task = timer.Then(self => resolved = self);

        context.RunUntil(() => task.IsCompleted, TimeSpan.FromSeconds(5));

        Assert.True(task.IsCompletedSuccessfully);
        Assert.Same(timer, resolved);
        Assert.Equal(30, timer.CurrentTime);
        Assert.True(timer.Completed);
    }
}
