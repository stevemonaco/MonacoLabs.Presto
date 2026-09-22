using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

/// <summary>Port of anime.js seconds.test.js. The fixture restores milliseconds.</summary>
public sealed class SecondsTests : EngineTestBase
{
    [Fact]
    public void CallsAddedToAZeroDurationTimelineWithADelayShouldNotFireBeforeTheEndOfTheDelayDuration()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        int timer1Log = 0, timer2Log = 0, timer3Log = 0;
        var completed = false;

        var tl = Presto.CreateTimeline(new()
        {
            Delay = 1,
            Loop = 1,
            Autoplay = false,
            OnComplete = _ => completed = true,
        })
        .Call(_ => timer1Log++, 0)
        .Call(_ => timer2Log++, 0)
        .Call(_ => timer3Log++, 0)
        .Init();

        tl.Seek(-.1);

        Assert.Equal(0, timer1Log);
        Assert.Equal(0, timer2Log);
        Assert.Equal(0, timer3Log);

        tl.Play();
        RunUntil(() => completed);

        Assert.True(completed);
        Assert.Equal(1, timer1Log);
        Assert.Equal(1, timer2Log);
        Assert.Equal(1, timer3Log);
    }

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
    public void StretchAnAnimation()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var animation1 = Presto.Animate(new Dictionary<string, object?>(), new()
        {
            ["Width"] = new TweenParams[] { new() { To = 100, Duration = .1 }, new() { To = 100, Duration = .2 } },
            Duration = .1,
            Autoplay = false,
        });
        Assert.Equal(.3, animation1.Duration);
        Assert.Equal(.1, TestHelpers.GetTweenAtIndex(animation1, 0)!._updateDuration);
        Assert.Equal(.2, TestHelpers.GetTweenAtIndex(animation1, 1)!._updateDuration);

        animation1.Stretch(.6);
        Assert.Equal(.6, animation1.Duration);
        Assert.Equal(.2, TestHelpers.GetTweenAtIndex(animation1, 0)!._updateDuration);
        Assert.Equal(.4, TestHelpers.GetTweenAtIndex(animation1, 1)!._updateDuration);

        animation1.Stretch(.03);
        Assert.Equal(.03, animation1.Duration);
        Assert.Equal(.01, TestHelpers.GetTweenAtIndex(animation1, 0)!._updateDuration);
        Assert.Equal(.02, TestHelpers.GetTweenAtIndex(animation1, 1)!._updateDuration);

        animation1.Stretch(0);
        Assert.Equal(Constants.MinValue, animation1.Duration);
        Assert.Equal(Constants.MinValue, TestHelpers.GetTweenAtIndex(animation1, 0)!._updateDuration);
        Assert.Equal(Constants.MinValue, TestHelpers.GetTweenAtIndex(animation1, 1)!._updateDuration);

        animation1.Stretch(.03);
        Assert.Equal(.03, animation1.Duration);
        Assert.Equal(.03, TestHelpers.GetTweenAtIndex(animation1, 0)!._updateDuration);
        Assert.Equal(.03, TestHelpers.GetTweenAtIndex(animation1, 1)!._updateDuration);
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
