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

    [Fact]
    public async Task ThenOrCancelCompletesWhenTheTimerCompletes()
    {
        var timer = Presto.CreateTimer(new() { Duration = 30 });
        var animation = Presto.Animate(new StyleTarget(), new() { ["Y"] = 100, Duration = 30 });
        var timerTask = timer.ThenOrCancel();
        var animationTask = animation.ThenOrCancel();

        timer.Pause();
        RunUntil(() => animation.Completed);
        Assert.False(timerTask.IsCompleted);
        timer.Resume();
        RunUntil(() => timer.Completed);

        Assert.Same(timer, await timerTask);
        Assert.Same(animation, await animationTask);
    }

    [Fact]
    public async Task ThenOrCancelIsCanceledByCancelRevertAndAMutedComplete()
    {
        var canceled = Presto.Animate(new StyleTarget(), new() { ["Y"] = 100, Duration = 30 });
        var reverted = Presto.Animate(new StyleTarget(), new() { ["Y"] = 100, Duration = 30 });
        var completed = Presto.Animate(new StyleTarget(), new() { ["Y"] = 100, Duration = 30 });
        var tasks = new[] { canceled.ThenOrCancel(), reverted.ThenOrCancel(), completed.ThenOrCancel() };

        canceled.Cancel();
        reverted.Revert();
        completed.Complete(muteCallbacks: true);

        foreach (var task in tasks) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task ThenOrCancelIsCanceledWhenCompositionReplacesTheAnimation()
    {
        var target = new StyleTarget();
        var first = Presto.Animate(target, new() { ["Y"] = 100, Duration = 30 });
        var task = first.ThenOrCancel();
        Frames.Advance(16);

        Presto.Animate(target, new() { ["Y"] = 0, Duration = 30 });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task ThenOrCancelIsCanceledWhenTheParentTimelineIsReverted()
    {
        var tl = Presto.CreateTimeline(new()).Add(new StyleTarget(), new() { ["X"] = 100, Duration = 30 });
        Timer? child = null;
        foreach (var c in tl.Children) child = c;
        var task = child!.ThenOrCancel();
        Frames.Advance(16);

        tl.Revert();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task ThenOrCancelOnACompletedOrCanceledTimerReturnsAtOnce()
    {
        var completed = Presto.CreateTimer(new() { Duration = 30 });
        RunUntil(() => completed.Completed);
        var canceled = Presto.CreateTimer(new() { Duration = 30 }).Cancel();

        var completedTask = completed.ThenOrCancel();
        var canceledTask = canceled.ThenOrCancel();

        Assert.True(completedTask.IsCompletedSuccessfully);
        Assert.Same(completed, await completedTask);
        Assert.True(canceledTask.IsCanceled);
    }

    [Fact]
    public async Task TheTokenCancelsTheWaitButNotTheTimer()
    {
        var timer = Presto.CreateTimer(new() { Duration = 100 });
        using var source = new CancellationTokenSource();
        var task = timer.ThenOrCancel(source.Token);
        var untokened = timer.ThenOrCancel();
        Frames.Advance(16);

        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.False(timer.Paused);
        RunUntil(() => timer.Completed);
        Assert.Same(timer, await untokened);
    }

    [Fact]
    public async Task ARevivedTimerKeepsItsCanceledTaskAndGivesANewOne()
    {
        var timer = Presto.CreateTimer(new() { Duration = 30 });
        var first = timer.ThenOrCancel();
        timer.Cancel();

        timer.Restart();
        var second = timer.ThenOrCancel();
        RunUntil(() => timer.Completed);

        Assert.True(first.IsCanceled);
        Assert.Same(timer, await second);
    }

    [Fact]
    public async Task ThenOrCancelResumesOutsideTheTick()
    {
        var tickThread = Environment.CurrentManagedThreadId;
        var ticking = false;
        var completing = Presto.CreateTimer(new() { Duration = 30 });
        var canceling = Presto.CreateTimer(new() { Duration = 100, OnUpdate = self => self.Cancel() });
        var resumedInTick = new bool[2];
        var continuations = new[]
        {
            completing.ThenOrCancel().ContinueWith(_ => resumedInTick[0] = ticking && Environment.CurrentManagedThreadId == tickThread, TaskContinuationOptions.ExecuteSynchronously),
            canceling.ThenOrCancel().ContinueWith(_ => resumedInTick[1] = ticking && Environment.CurrentManagedThreadId == tickThread, TaskContinuationOptions.ExecuteSynchronously),
        };

        while (!completing.Completed)
        {
            ticking = true;
            Frames.Advance(16);
            ticking = false;
        }
        await Task.WhenAll(continuations);

        Assert.True(canceling.Canceled);
        Assert.Equal([false, false], resumedInTick);
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

    [Fact]
    public void ThenCallbacksRunAfterOnComplete()
    {
        var log = new List<string>();
        var timer = Presto.CreateTimer(new() { Duration = 30, OnComplete = _ => log.Add("complete") });
        timer.Then(_ => log.Add("then"));

        RunUntil(() => timer.Completed);

        Assert.Equal(["complete", "then"], log);
    }

    [Fact]
    public void SeveralThenCallsShareOneTaskAndRunEveryCallbackInOrder()
    {
        var log = new List<string>();
        var timer = Presto.CreateTimer(new() { Duration = 30 });

        var first = timer.Then(_ => log.Add("first"));
        var second = timer.Then(_ => log.Add("second"));
        RunUntil(() => timer.Completed);

        Assert.Same(first, second);
        Assert.True(first.IsCompleted);
        Assert.Equal(["first", "second"], log);
    }

    [Fact]
    public void ThenContinuationsNeverRunInsideTheTick()
    {
        var timer = Presto.CreateTimer(new() { Duration = 30 });
        var testThread = Environment.CurrentManagedThreadId;
        int? continuationThread = null;
        using var ran = new ManualResetEventSlim();
        _ = timer.Then().ContinueWith(_ =>
        {
            continuationThread = Environment.CurrentManagedThreadId;
            ran.Set();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        RunUntil(() => timer.Completed);

        Assert.True(ran.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.NotEqual(testThread, continuationThread);
    }
}
