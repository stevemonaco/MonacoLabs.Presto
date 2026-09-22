namespace Monaco.Presto.Tests;

/// <summary>Port of anime.js timings.test.js; tween-specific expectations are asserted through IterationProgress on a single timer.</summary>
public sealed class TimingsTests : EngineTestBase
{
    private static TimerParams ComplexTimingsParams => new()
    {
        Delay = 10,
        Duration = 55,
        LoopDelay = 10,
        Loop = 1,
        Autoplay = false,
    };

    [Fact]
    public void SpecifiedTimingsParameters()
    {
        double? completedAt = null;
        Presto.CreateTimer(new()
        {
            Delay = 10,
            Duration = 20,
            Loop = 1,
            LoopDelay = 10,
            OnComplete = t => completedAt = t.CurrentTime,
        });

        RunUntil(() => completedAt != null);

        Assert.Equal(20 + 10 + 20, completedAt);
    }

    [Fact]
    public void IterationCurrentTimeShouldBeNegativeWhenADelayIsDefined()
    {
        var timer = Presto.CreateTimer(ComplexTimingsParams);
        Assert.Equal(-10, timer.CurrentTime);

        timer.Seek(5);
        Assert.Equal(5, timer.CurrentTime);

        timer.Seek(timer.Duration);
        Assert.Equal(timer.Duration, timer.CurrentTime);
    }

    [Fact]
    public void DurationEqualsTheLoopedIterationSpan()
    {
        var timer = Presto.CreateTimer(ComplexTimingsParams);

        Assert.Equal(55 + 10 + 55, timer.Duration);
        Assert.Equal(55, timer.IterationDuration);
    }

    [Fact]
    public void DelayShouldDelayTheStart()
    {
        double? beganAt = null;
        double? progressAt50 = null;
        double? progressAt100 = null;
        Presto.CreateTimer(new()
        {
            Delay = 100,
            Duration = 100,
            OnBegin = self =>
            {
                self.Pause();
                beganAt = Frames.Now;
                self.Seek(50);
                progressAt50 = self.IterationProgress;
                self.Seek(100);
                progressAt100 = self.IterationProgress;
            },
        });

        RunUntil(() => beganAt != null);

        Assert.NotNull(beganAt);
        Assert.InRange(beganAt.Value, 100 - 17, 100 + 17);
        Assert.Equal(.5, progressAt50);
        Assert.Equal(1, progressAt100);
    }

    [Fact]
    public void DelayedAlternateLoopedTimersShouldStartCorrectly()
    {
        var timer = Presto.CreateTimer(new()
        {
            Duration = 1000,
            Loop = 2,
            Delay = 1000,
            Alternate = true,
            Autoplay = false,
        });

        Assert.Equal(0, timer.IterationProgress);
        Assert.False(timer.Began);
    }

    [Fact]
    public void GetAndSetIterationProgressOnNonLoopedTimer()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000, Autoplay = false });

        timer.IterationProgress = 0;
        Assert.Equal(0, timer.IterationProgress);
        timer.IterationProgress = .5;
        Assert.Equal(.5, timer.IterationProgress);
        timer.IterationProgress = 1;
        Assert.Equal(1, timer.IterationProgress);
        Assert.Equal(1000, timer.CurrentTime);
    }

    [Fact]
    public void GetAndSetIterationProgressOnALoopedTimerWithPreDefinedIterations()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000, Autoplay = false, Loop = 3 });

        timer.Seek(2200);
        Assert.Equal(.2, timer.IterationProgress);
        timer.IterationProgress = 0;
        Assert.Equal(2000, timer.CurrentTime);
        Assert.Equal(0, timer.IterationProgress);
        timer.IterationProgress = .5;
        Assert.Equal(.5, timer.IterationProgress);
        timer.IterationProgress = 1;
        Assert.Equal(0, timer.IterationProgress);
        Assert.Equal(3000, timer.CurrentTime);
    }

    [Fact]
    public void GetAndSetCurrentIterationOnALoopedTimerWithPreDefinedIterations()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000, Autoplay = false, Loop = 4 });

        timer.CurrentIteration = 0;
        Assert.Equal(0, timer.CurrentIteration);
        timer.Seek(1500);
        Assert.Equal(1, timer.CurrentIteration);
        timer.CurrentIteration = 2;
        Assert.Equal(2, timer.CurrentIteration);
        Assert.Equal(2000, timer.CurrentTime);
        timer.CurrentIteration = 99;
        Assert.Equal(4, timer.CurrentIteration);
        Assert.Equal(4000, timer.CurrentTime);
    }

    [Fact]
    public void GetAndSetCurrentTimeOnALoopedTimerWithPreDefinedIterations()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000, Autoplay = false, Loop = 4 });

        timer.CurrentTime = 1500;
        Assert.Equal(1500, timer.CurrentTime);
        Assert.Equal(.5, timer.IterationProgress);
        timer.CurrentTime = 4250;
        Assert.Equal(4250, timer.CurrentTime);
        Assert.Equal(.25, timer.IterationProgress);
        timer.CurrentTime = 5500;
        Assert.Equal(5000, timer.CurrentTime);
        Assert.Equal(1, timer.IterationProgress);
    }

    [Fact]
    public void GetAndSetIterationCurrentTimeOnALoopedTimerWithPreDefinedIterations()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000, Autoplay = false, Loop = 4 });

        timer.IterationCurrentTime = 500;
        Assert.Equal(500, timer.CurrentTime);
        Assert.Equal(0, timer.CurrentIteration);
        Assert.Equal(500, timer.IterationCurrentTime);
        Assert.Equal(.5, timer.IterationProgress);
        timer.IterationCurrentTime = 1500;
        Assert.Equal(1500, timer.CurrentTime);
        Assert.Equal(1, timer.CurrentIteration);
        Assert.Equal(500, timer.IterationCurrentTime);
        Assert.Equal(.5, timer.IterationProgress);
        timer.IterationCurrentTime = 250;
        Assert.Equal(1250, timer.CurrentTime);
        Assert.Equal(1, timer.CurrentIteration);
        Assert.Equal(250, timer.IterationCurrentTime);
        Assert.Equal(.25, timer.IterationProgress);
    }
}
