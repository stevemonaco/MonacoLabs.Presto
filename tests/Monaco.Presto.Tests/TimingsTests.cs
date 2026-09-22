namespace Monaco.Presto.Tests;

/// <summary>Port of anime.js timings.test.js.</summary>
public sealed class TimingsTests : EngineTestBase
{
    private static AnimationParams ComplexAnimationParams => new()
    {
        ["TranslateX"] = new TweenParams { To = 50, Delay = new FunctionValue((_, _, _) => 15), Duration = new FunctionValue((_, _, _) => 10) },
        ["TranslateY"] = new TweenParams { To = 35, Delay = 10, Duration = 10 },
        ["TranslateZ"] = new TweenParams { To = 20, Delay = 35, Duration = 30 },
        Delay = new FunctionValue((_, _, _) => 10),
        Duration = new FunctionValue((_, _, _) => 10),
        LoopDelay = 10,
        Loop = 1,
        Autoplay = false,
    };

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
    public void SpecifiedTimingsParametersOnAnimation()
    {
        double? completedAt = null;
        Presto.Animate(new StyleTarget(), new()
        {
            ["TranslateX"] = 100,
            Delay = 10,
            Duration = 20,
            Loop = 1,
            LoopDelay = 10,
            OnComplete = a => completedAt = a.CurrentTime,
        });

        RunUntil(() => completedAt != null);

        Assert.Equal(20 + 10 + 20, completedAt);
    }

    [Fact]
    public void AnimationIterationCurrentTimeShouldBeNegativeWhenADelayIsDefined()
    {
        var animation = Presto.Animate(new Dictionary<string, object?>(), ComplexAnimationParams);
        Assert.Equal(-10, animation.CurrentTime);

        animation.Seek(5);
        Assert.Equal(5, animation.CurrentTime);

        animation.Seek(animation.Duration);
        Assert.Equal(animation.Duration, animation.CurrentTime);
    }

    [Fact]
    public void AnimationDurationMustEqualTheHighestTweenEndValueMinusTheDelay()
    {
        var animation = Presto.Animate(new Dictionary<string, object?>(), ComplexAnimationParams);

        Assert.Equal(55 + 10 + 55, animation.Duration);
    }

    [Fact]
    public void AnimationIterationDurationMustEqualTheLongestTweenChangeEndTimeMinusTheDelay()
    {
        var animation = Presto.Animate(new Dictionary<string, object?>(), ComplexAnimationParams);

        Assert.Equal(65 - 10, animation.IterationDuration);
    }

    [Fact]
    public void DelayedAlternateLoopedAnimationsShouldStartCorrectly()
    {
        var target = new StyleTarget();

        var animation = Presto.Animate(target, new() { ["Y"] = -100, Loop = 2, Delay = 1000, Alternate = true, Autoplay = false });

        Assert.Equal(0, target.Y);
        Assert.False(animation.Began);
    }

    [Fact]
    public void NegativeDelayOnAlternateLoopedAnimationsShouldRenderTheValueInAdvance()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            ["Scale"] = TweenValue.FromTo(0, 1),
            Ease = "linear",
            Loop = true,
            Duration = 1000,
            Delay = -5250,
            Alternate = true,
            Autoplay = false,
        });

        animation.Seek(0);

        Assert.Equal(.75, target.Scale);
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
