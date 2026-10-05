using System.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace Monaco.Presto.Tests;

public sealed class FrameSourceTests
{
    [Fact]
    public void ManualAdvanceBumpsNowWithoutRunningBeforeStart()
    {
        var source = new ManualFrameSource();

        source.Advance(16);

        Assert.Equal(16, source.Now);
        Assert.False(source.IsRunning);
    }

    [Fact]
    public void ManualAdvanceInvokesCallbackOncePerAdvanceAfterStart()
    {
        var source = new ManualFrameSource();
        var frames = 0;
        source.Start(() => frames++);
        Assert.True(source.IsRunning);

        source.Advance(16);
        source.Advance(16);

        Assert.Equal(2, frames);
        Assert.Equal(32, source.Now);
    }

    [Fact]
    public void ManualSteppedAdvanceInvokesPerFrameAndLandsOnTarget()
    {
        var source = new ManualFrameSource();
        var frames = 0;
        source.Start(() => frames++);

        source.Advance(100, 16);

        Assert.Equal(7, frames);
        Assert.Equal(100, source.Now);
    }

    [Fact]
    public void ManualStopFromInsideCallbackPreventsFurtherInvocations()
    {
        var source = new ManualFrameSource();
        var frames = 0;
        source.Start(() =>
        {
            frames++;
            source.Stop();
        });

        source.Advance(100, 16);

        Assert.Equal(1, frames);
        Assert.False(source.IsRunning);
        Assert.Equal(100, source.Now);
    }

    [Fact]
    public void ManualStartTwiceKeepsFirstCallback()
    {
        var source = new ManualFrameSource();
        var first = 0;
        var second = 0;
        source.Start(() => first++);
        source.Start(() => second++);

        source.Advance(16);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
    }

    [Fact]
    public void StopwatchNowFollowsTheTimeProvider()
    {
        var time = new FakeTimeProvider();
        var source = new StopwatchFrameSource(time);

        time.Advance(TimeSpan.FromMilliseconds(250));

        Assert.Equal(250, source.Now);
    }

    [Fact]
    public void StopwatchWithoutASynchronizationContextPumpsOnOneBackgroundThreadUntilStopped()
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            var source = new StopwatchFrameSource();
            var frames = 0;
            var threadIds = new HashSet<int>();
            source.Start(() =>
            {
                threadIds.Add(Environment.CurrentManagedThreadId);
                Interlocked.Increment(ref frames);
            });

            var elapsed = Stopwatch.StartNew();
            while (Volatile.Read(ref frames) < 3 && elapsed.ElapsedMilliseconds < 250) Thread.Sleep(1);
            source.Stop();

            var framesAtStop = Volatile.Read(ref frames);
            Assert.True(framesAtStop >= 3, $"Expected at least 3 frames within 250 ms, got {framesAtStop}.");
            var pumpThreadId = Assert.Single(threadIds);
            Assert.NotEqual(Environment.CurrentManagedThreadId, pumpThreadId);

            Thread.Sleep(50);
            Assert.Equal(framesAtStop, Volatile.Read(ref frames));

            source.Stop();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public void StopwatchPostsFramesThroughTheStartingThreadsSynchronizationContext()
    {
        using var context = new PumpingSynchronizationContext();
        var source = new StopwatchFrameSource();
        var frames = 0;
        var threadIds = new HashSet<int>();
        source.Start(() =>
        {
            threadIds.Add(Environment.CurrentManagedThreadId);
            frames++;
        });

        context.RunUntil(() => frames >= 3, TimeSpan.FromMilliseconds(250));
        source.Stop();

        Assert.True(frames >= 3, $"Expected at least 3 frames within 250 ms, got {frames}.");
        Assert.Equal(Environment.CurrentManagedThreadId, Assert.Single(threadIds));

        var framesAtStop = frames;
        context.RunUntil(() => false, TimeSpan.FromMilliseconds(50));
        Assert.Equal(framesAtStop, frames);
    }
}
