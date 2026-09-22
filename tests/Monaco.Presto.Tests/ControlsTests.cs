using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

/// <summary>Port of anime.js controls.test.js (timer subset; animation cases adapted to timers).</summary>
public sealed class ControlsTests : EngineTestBase
{
    private (Timer Timer, double TimeStamp) RunAlternatingTimer(double threshold, double settle, double delay, LoopCount? loop = null, bool alternate = false, bool reversed = false)
    {
        var hasReversed = false;
        double? timeStamp = null;
        var timer = Presto.CreateTimer(new()
        {
            Duration = 100,
            Delay = delay,
            Loop = loop,
            Alternate = alternate,
            Reversed = reversed,
            OnUpdate = self =>
            {
                if (self.CurrentTime >= threshold && !hasReversed)
                {
                    hasReversed = true;
                    timeStamp = self.CurrentTime;
                    self.Alternate();
                }
            },
        });

        RunUntil(() => hasReversed);
        Frames.Advance(settle, 16);

        Assert.NotNull(timeStamp);
        return (timer, timeStamp.Value);
    }

    [Fact]
    public void AlternateTheDirectionOfATimer()
    {
        var (timer, timeStamp) = RunAlternatingTimer(100 * .75, 33, delay: 50);

        Assert.True(timer.CurrentTime < timeStamp);
    }

    [Fact]
    public void AlternateTheDirectionOfAReversedTimer()
    {
        var (timer, timeStamp) = RunAlternatingTimer(0, 33, delay: 50, reversed: true);

        Assert.True(timer.CurrentTime > timeStamp);
    }

    [Fact]
    public void AlternateTheDirectionOfALoopedTimer()
    {
        var (timer, timeStamp) = RunAlternatingTimer(100 * 2.5, 33, delay: 50, loop: 3);

        Assert.True(timer.CurrentTime < timeStamp);
        Assert.Equal(1, timer.CurrentIteration);
    }

    [Fact]
    public void AlternateTheDirectionOfALoopedAlternateTimer()
    {
        var (timer, timeStamp) = RunAlternatingTimer(100 * 2.5, 33, delay: 50, loop: 3, alternate: true);

        Assert.True(timer.CurrentTime < timeStamp);
        Assert.Equal(1, timer.CurrentIteration);
    }

    [Fact]
    public void AlternateTheDirectionOfALoopedAlternateReversedTimerWithOddIterationCount()
    {
        var (timer, timeStamp) = RunAlternatingTimer(100 * 2.5, 33, delay: 50, loop: 3, alternate: true, reversed: true);

        Assert.True(timer.CurrentTime < timeStamp);
        Assert.Equal(1, timer.CurrentIteration);
    }

    [Fact]
    public void AlternateTheDirectionOfALoopedAlternateReversedTimerWithEvenIterationCount()
    {
        var (timer, timeStamp) = RunAlternatingTimer(100 * 1.5, 40, delay: 100, loop: 2, alternate: true, reversed: true);

        Assert.True(timer.CurrentTime > timeStamp);
        Assert.Equal(1, timer.CurrentIteration);
    }

    [Fact]
    public void CompleteATimer()
    {
        var timer1 = Presto.CreateTimer(new() { Duration = 1000 });

        timer1.Complete();

        Assert.True(timer1.Cancelled);
        Assert.True(timer1.Paused);
        Assert.Equal(1000, timer1.CurrentTime);
    }

    [Fact]
    public void CancelATimer()
    {
        var timer1 = Presto.CreateTimer(new() { Duration = 1000 });

        timer1.Seek(500).Cancel();

        Assert.True(timer1.Cancelled);
        Assert.True(timer1.Paused);
    }

    [Fact]
    public void RevertATimer()
    {
        var timer1 = Presto.CreateTimer(new() { Duration = 1000 });
        timer1.Seek(500);

        timer1.Revert();

        Assert.Equal(0, timer1.CurrentTime);
        Assert.True(timer1.Cancelled);
        Assert.True(timer1.Paused);
    }

    [Fact]
    public void ResetAPlayingTimer()
    {
        var timer1 = Presto.CreateTimer(new() { Autoplay = false, Duration = 1000, Loop = 1 });
        Assert.Equal(0, timer1.CurrentTime);
        Assert.True(timer1.Paused);
        Assert.False(timer1.Began);
        Assert.False(timer1.Completed);
        Assert.Equal(0, timer1.CurrentIteration);

        timer1.Seek(1500);
        timer1.Resume();
        Assert.Equal(1500, timer1.CurrentTime);
        Assert.False(timer1.Paused);
        Assert.True(timer1.Began);
        Assert.False(timer1.Completed);
        Assert.Equal(1, timer1.CurrentIteration);

        timer1.Pause();
        timer1.Seek(2000);
        Assert.Equal(2000, timer1.CurrentTime);
        Assert.True(timer1.Paused);
        Assert.True(timer1.Began);
        Assert.True(timer1.Completed);
        Assert.Equal(1, timer1.CurrentIteration);

        timer1.Seek(500);
        timer1.Resume();
        timer1.Reset();
        Assert.Equal(0, timer1.CurrentTime);
        Assert.True(timer1.Paused);
        Assert.False(timer1.Began);
        Assert.False(timer1.Completed);
        Assert.Equal(0, timer1.CurrentIteration);
        timer1.Pause();
    }

    [Fact]
    public void ResetACancelledTimer()
    {
        var timer1 = Presto.CreateTimer(new() { Duration = 1000, Loop = 1 });
        Assert.False(timer1.Cancelled);
        Assert.Equal(0, timer1.CurrentTime);
        Assert.False(timer1.Paused);
        Assert.False(timer1.Began);
        Assert.False(timer1.Completed);
        Assert.Equal(0, timer1.CurrentIteration);

        timer1.Seek(1500);
        timer1.Cancel();
        Assert.True(timer1.Cancelled);
        Assert.Equal(1500, timer1.CurrentTime);
        Assert.True(timer1.Paused);
        Assert.True(timer1.Began);
        Assert.False(timer1.Completed);
        Assert.Equal(1, timer1.CurrentIteration);

        timer1.Reset();
        Assert.False(timer1.Cancelled);
        Assert.Equal(0, timer1.CurrentTime);
        Assert.True(timer1.Paused);
        Assert.False(timer1.Began);
        Assert.False(timer1.Completed);
        Assert.Equal(0, timer1.CurrentIteration);

        timer1.Play();
        Assert.False(timer1.Paused);
        RunUntil(() => timer1.Completed);
        Assert.True(timer1.Completed);
        Assert.Equal(2000, timer1.CurrentTime);
    }

    [Fact]
    public void StretchATimer()
    {
        var timer1 = Presto.CreateTimer(new() { Duration = 1273, Autoplay = false });
        Assert.Equal(1273, timer1.Duration);

        for (var i = 0; i < 9999; i++)
        {
            var newTime = 1 + i;
            timer1.Stretch(newTime);
            Assert.Equal(newTime, timer1.Duration);
            Assert.Equal(newTime, timer1.IterationDuration);
        }

        timer1.Stretch(0);
        Assert.Equal(Constants.MinValue, timer1.Duration);
        Assert.Equal(Constants.MinValue, timer1.IterationDuration);
    }

    [Fact]
    public void StretchALoopedTimer()
    {
        var timer1 = Presto.CreateTimer(new() { Duration = 1273, Autoplay = false, Loop = 3 });
        Assert.Equal(1273 * 4, timer1.Duration);

        for (var i = 0; i < 9999; i++)
        {
            var newTime = 1 + i;
            timer1.Stretch(newTime);
            Assert.Equal(newTime, timer1.Duration);
            Assert.Equal(newTime / timer1.IterationCount, timer1.IterationDuration);
        }

        timer1.Stretch(0);
        Assert.Equal(Constants.MinValue, timer1.Duration);
        Assert.Equal(Constants.MinValue, timer1.IterationDuration);
    }

    [Fact]
    public void SeekATimer()
    {
        var timer1 = Presto.CreateTimer(new() { Duration = 200, Autoplay = false });
        Assert.Equal(0, timer1.CurrentTime);
        Assert.Equal(0, timer1.Progress);
        Assert.False(timer1.Began);
        Assert.False(timer1.Completed);

        timer1.Seek(100);
        Assert.Equal(100, timer1.CurrentTime);
        Assert.Equal(.5, timer1.Progress);
        Assert.True(timer1.Began);
        Assert.False(timer1.Completed);

        timer1.Seek(200);
        Assert.Equal(200, timer1.CurrentTime);
        Assert.Equal(1, timer1.Progress);
        Assert.True(timer1.Began);
        Assert.True(timer1.Completed);

        timer1.Seek(150);
        Assert.Equal(150, timer1.CurrentTime);
        Assert.Equal(.75, timer1.Progress);
        Assert.True(timer1.Began);
        Assert.False(timer1.Completed);

        timer1.Resume();
        double? currentTime = null;
        double? progress = null;
        bool? completed = null;
        Presto.CreateTimer(new()
        {
            Duration = 65,
            OnComplete = _ =>
            {
                timer1.Pause();
                currentTime = timer1.CurrentTime;
                progress = timer1.Progress;
                completed = timer1.Completed;
            },
        });
        RunUntil(() => completed != null);

        Assert.Equal(200, currentTime);
        Assert.Equal(1, progress);
        Assert.True(completed);
    }

    [Fact]
    public void GetAndSetCancelledOnATimer()
    {
        var timer = Presto.CreateTimer(new() { Delay = 10, Duration = 55, LoopDelay = 10, Loop = 1, Autoplay = false });
        Assert.False(timer.Cancelled);

        timer.Cancelled = true;
        Assert.True(timer.Cancelled);
        Assert.True(timer.Paused);

        timer.Cancelled = false;
        Assert.False(timer.Cancelled);
        Assert.False(timer.Paused);
    }
}
