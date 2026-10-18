namespace MonacoLabs.Presto.Tests;

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
    public void IterationSettersStepOverTheLoopDelay()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000, Autoplay = false, Loop = 3, LoopDelay = 500 });

        timer.CurrentIteration = 2;
        Assert.Equal(3000, timer.CurrentTime);
        Assert.Equal(2, timer.CurrentIteration);

        timer.IterationCurrentTime = 250;
        Assert.Equal(3250, timer.CurrentTime);
        timer.IterationProgress = .5;
        Assert.Equal(3500, timer.CurrentTime);
        Assert.Equal(2, timer.CurrentIteration);
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

    [Fact]
    public void RaisingLoopMidPlayExtendsDurationAndPlaysTheExtraIterations()
    {
        var loops = 0;
        var timer = Presto.CreateTimer(new() { Duration = 100, LoopDelay = 10, OnLoop = _ => loops++ });
        Frames.Advance(48);

        timer.Loop = 2;
        Assert.Equal(2, timer.Loop.Value);
        Assert.Equal(3, timer.IterationCount);
        Assert.Equal(320, timer.Duration);
        RunUntil(() => timer.Completed);

        Assert.Equal(2, loops);
        Assert.Equal(320, timer.CurrentTime);
    }

    [Fact]
    public void LoweringLoopBelowTheCurrentIterationCompletesOnTheNextTick()
    {
        var completed = 0;
        var timer = Presto.CreateTimer(new() { Duration = 100, Loop = 4, OnComplete = _ => completed++ });
        RunUntil(() => timer.CurrentIteration == 2);

        timer.Loop = 1;
        Assert.Equal(200, timer.Duration);
        Assert.Equal(200, timer.CurrentTime);
        Assert.False(timer.Completed);
        Frames.Advance(16);

        Assert.True(timer.Completed);
        Assert.Equal(1, completed);
        Assert.Equal(1, timer.Progress);
    }

    [Fact]
    public void LoweringLoopOnAPausedTimerRendersTheEndStateOnceItPlays()
    {
        var target = new StyleTarget();
        var updates = 0;
        var animation = Presto.Animate(target, new() { ["X"] = 100, Duration = 100, Loop = 4, Ease = "linear", OnUpdate = _ => updates++ });
        RunUntil(() => animation.CurrentIteration == 2 && animation.IterationProgress > .3);
        animation.Pause();

        animation.Loop = 0;
        updates = 0;
        animation.Play();
        Frames.Advance(16);

        Assert.True(animation.Completed);
        Assert.Equal(100, target.X);
        Assert.True(updates > 0);
    }

    [Fact]
    public void SettingLoopToInfiniteNeverCompletes()
    {
        var timer = Presto.CreateTimer(new() { Duration = 100 });
        Frames.Advance(16);

        timer.Loop = LoopCount.Infinite;
        Frames.Advance(500, 16);

        Assert.True(timer.Loop.IsInfinite);
        Assert.False(timer.Completed);
    }

    [Theory]
    [InlineData(null, false, 50, 50)]
    [InlineData("inQuad", false, 50, 25)]
    [InlineData(null, true, 25, 75)]
    [InlineData("inQuad", true, 25, 56.25)]
    public void IterationTimeReflectsPlaybackEaseAndReversed(string? playbackEase, bool reversed, double seek, double expected)
    {
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["X"] = 100,
            Duration = 100,
            Ease = "linear",
            PlaybackEase = playbackEase is null ? null : (EaseValue?)playbackEase,
            Reversed = reversed,
            Autoplay = false,
        });

        animation.Seek(seek);

        Assert.Equal(seek, animation.CurrentTime);
        Assert.Equal(expected, animation.IterationTime);
    }

    [Fact]
    public void NullParametersComeFromTheEngineDefaultsOnceAtConstruction()
    {
        var defaults = Presto.Defaults;
        defaults.Delay = 50;
        defaults.LoopDelay = 10;
        defaults.Loop = 2;
        defaults.Alternate = true;
        defaults.Reversed = true;
        defaults.PlaybackRate = 2;
        defaults.FrameRate = 30;

        var timer = Presto.CreateTimer(new() { Duration = 100, Autoplay = false });
        var overridden = Presto.CreateTimer(new() { Duration = 100, Delay = 5, Loop = 0, Autoplay = false });
        defaults.Delay = 0;
        defaults.LoopDelay = 0;
        defaults.Loop = 0;
        defaults.Alternate = false;
        defaults.Reversed = false;
        defaults.PlaybackRate = 1;

        Assert.Equal(50, timer.Delay);
        Assert.Equal(10, timer.LoopDelay);
        Assert.Equal(2, timer.Loop.Value);
        Assert.True(timer.IsAlternating);
        Assert.True(timer.Reversed);
        Assert.Equal(2, timer.Speed);
        Assert.Equal(30, timer.Fps);
        Assert.Equal(5, overridden.Delay);
        Assert.Equal(0, overridden.Loop.Value);
    }

    [Fact]
    public void ATimelineChildTakesNullParametersFromTheTimelinesChildDefaults()
    {
        Presto.Defaults.Delay = 50;
        Presto.Defaults.Loop = 3;
        var tl = Presto.CreateTimeline(new() { Autoplay = false, Defaults = new() { Delay = 20, LoopDelay = 5 } })
            .Add(new TimerParams { Duration = 100 });

        var child = TestHelpers.GetChildAtIndex(tl, 0)!;

        Assert.Equal(20, child.Delay);
        Assert.Equal(5, child.LoopDelay);
        Assert.Equal(3, child.Loop.Value);
    }

    [Fact]
    public void ANullDurationIsInfiniteIgnoresTheDefaultAndNeverCompletes()
    {
        Presto.Defaults.Duration = 500;
        var completed = 0;
        var timer = Presto.CreateTimer(new() { OnComplete = _ => completed++ });

        Assert.Equal(double.PositiveInfinity, timer.IterationDuration);
        Assert.Equal(1e12, timer.Duration);

        Frames.Advance(10000, 16);
        Assert.True(timer.CurrentTime > 9000);
        Assert.False(timer.Completed);

        timer.Seek(1e12 - 1);
        Assert.False(timer.Completed);
        Assert.Equal(0, completed);
    }

    [Fact]
    public void AFunctionBasedDefaultDelayGivesAPlainTimerNoDelay()
    {
        Presto.Defaults.Delay = new FunctionValue((_, _, _) => 200);

        var timer = Presto.CreateTimer(new() { Duration = 100 });

        Assert.Equal(0, timer.Delay);
    }

    [Fact]
    public void TimersWithoutAnIdGetConsecutiveCounterIds()
    {
        var first = Presto.CreateTimer(new() { Duration = 100, Autoplay = false });
        var named = Presto.CreateTimer(new() { Duration = 100, Autoplay = false, Id = "named" });
        var third = Presto.CreateTimer(new() { Duration = 100, Autoplay = false });

        Assert.Equal("named", named.Id);
        Assert.Equal(int.Parse(first.Id) + 2, int.Parse(third.Id));
    }

    [Fact]
    public void SettingLoopOnATimelineChildGrowsTheTimelineButNeverShrinksIt()
    {
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(new TimerParams { Duration = 100 })
            .Add(new TimerParams { Duration = 50 }, 0);
        var shorter = TestHelpers.GetChildAtIndex(tl, 1)!;
        Assert.Equal(100, tl.Duration);

        shorter.Loop = 3;
        Assert.Equal(200, tl.Duration);

        shorter.Loop = 0;
        Assert.Equal(200, tl.Duration);
    }

    [Fact]
    public void SeekWithMutedCallbacksFiresNothingForTheIterationsItSkips()
    {
        var fired = 0;
        var timer = Presto.CreateTimer(new()
        {
            Duration = 100,
            Loop = 3,
            Autoplay = false,
            OnBegin = _ => fired++,
            OnBeforeUpdate = _ => fired++,
            OnUpdate = _ => fired++,
            OnLoop = _ => fired++,
            OnComplete = _ => fired++,
        });

        timer.Seek(250, muteCallbacks: true);
        Assert.Equal(2, timer.CurrentIteration);
        timer.Seek(400, muteCallbacks: true);

        Assert.True(timer.Completed);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void SeekWithInternalRenderMovesThePlayheadWithoutWritingTargets()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new() { ["X"] = 100, Duration = 100, Ease = "linear", Autoplay = false });

        animation.Seek(50, internalRender: true);
        Assert.Equal(50, animation.CurrentTime);
        Assert.Equal(0, target.X);

        animation.Seek(60);
        Assert.Equal(60, target.X);
    }
}
