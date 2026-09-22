using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

/// <summary>Port of anime.js seconds.test.js (timer subset); the 0 duration timeline case is Phase 4. The fixture restores milliseconds.</summary>
public sealed class SecondsTests : EngineTestBase
{
    [Fact]
    public void TimersOffsetTimeShouldBeProperlyScaledWhenNotControlledByATimeline()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        Frames.Advance(500);

        var timer1 = Presto.CreateTimer(new() { Duration = .05, Autoplay = false });
        var timer2 = Presto.CreateTimer(new() { Duration = .05, Delay = .01, Autoplay = false });

        Assert.True(timer1._offset < 1);
        Assert.True(timer2._offset < 1);
    }

    [Fact]
    public void StretchALoopedTimerWithZeroLoops()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var timer1 = Presto.CreateTimer(new() { Duration = .1, Autoplay = false, Loop = 0 });
        Assert.Equal(.1, timer1.Duration);

        for (var i = 1; i < 9999; i++)
        {
            var newTime = Math.Round(i * .1, 1);
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
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var timer1 = Presto.CreateTimer(new() { Duration = .1, Autoplay = false, Loop = 3 });
        Assert.Equal(.1 * 4, timer1.Duration);

        for (var i = 1; i < 9999; i++)
        {
            var newTime = Math.Round(i * .1, 1);
            timer1.Stretch(newTime);
            Assert.Equal(newTime, timer1.Duration);
            Assert.Equal(newTime / timer1.IterationCount, timer1.IterationDuration);
        }

        timer1.Stretch(0);
        Assert.Equal(Constants.MinValue, timer1.Duration);
        Assert.Equal(Constants.MinValue, timer1.IterationDuration);
    }

    [Fact]
    public void ATimerPlaysInSeconds()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var timer = Presto.CreateTimer(new() { Duration = .05 });

        Frames.Advance(100, 16);

        Assert.True(timer.Completed);
        Assert.Equal(.05, timer.CurrentTime);
    }
}
