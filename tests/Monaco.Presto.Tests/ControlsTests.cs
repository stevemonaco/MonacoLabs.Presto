using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

/// <summary>
/// Port of anime.js controls.test.js; the timer variants of the reset and seek cases keep the tween-free path covered.
/// The DOM inline style revert cases are bridge work.
/// </summary>
public sealed class ControlsTests : EngineTestBase
{
    private (Animation Animation, StyleTarget Target, double TimeStamp) RunAlternatingAnimation(
        double threshold, double settle, double delay, LoopCount? loop = null, bool alternate = false, bool reversed = false)
    {
        var hasReversed = false;
        double? timeStamp = null;
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            ["X"] = 100,
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
        return (animation, target, timeStamp.Value);
    }

    [Fact]
    public void AlternateTheDirectionOfAnAnimation()
    {
        var (animation, target, timeStamp) = RunAlternatingAnimation(100 * .75, 33, delay: 50);

        Assert.True(animation.CurrentTime < timeStamp);
        Assert.True(target.X < 100);
    }

    [Fact]
    public void AlternateTheDirectionOfAReversedAnimation()
    {
        var (animation, target, timeStamp) = RunAlternatingAnimation(0, 33, delay: 50, reversed: true);

        Assert.True(animation.CurrentTime > timeStamp);
        Assert.True(target.X > 0);
    }

    [Fact]
    public void AlternateTheDirectionOfALoopedAnimation()
    {
        var (animation, _, timeStamp) = RunAlternatingAnimation(100 * 2.5, 33, delay: 50, loop: 3);

        Assert.True(animation.CurrentTime < timeStamp);
        Assert.Equal(1, animation.CurrentIteration);
    }

    [Fact]
    public void AlternateTheDirectionOfALoopedAlternateAnimation()
    {
        var (animation, _, timeStamp) = RunAlternatingAnimation(100 * 2.5, 33, delay: 50, loop: 3, alternate: true);

        Assert.True(animation.CurrentTime < timeStamp);
        Assert.Equal(1, animation.CurrentIteration);
    }

    [Fact]
    public void AlternateTheDirectionOfALoopedAlternateReversedAnimationWithOddIterationCount()
    {
        var (animation, _, timeStamp) = RunAlternatingAnimation(100 * 2.5, 33, delay: 50, loop: 3, alternate: true, reversed: true);

        Assert.True(animation.CurrentTime < timeStamp);
        Assert.Equal(1, animation.CurrentIteration);
    }

    [Fact]
    public void AlternateTheDirectionOfALoopedAlternateReversedAnimationWithEvenIterationCount()
    {
        var (animation, _, timeStamp) = RunAlternatingAnimation(100 * 1.5, 40, delay: 100, loop: 2, alternate: true, reversed: true);

        Assert.True(animation.CurrentTime > timeStamp);
        Assert.Equal(1, animation.CurrentIteration);
    }

    [Fact]
    public void CompleteATimer()
    {
        var timer1 = Presto.CreateTimer(new() { Duration = 1000 });

        timer1.Complete();

        Assert.True(timer1.Canceled);
        Assert.True(timer1.Paused);
        Assert.Equal(1000, timer1.CurrentTime);
    }

    [Fact]
    public void CompleteAnAnimation()
    {
        var target = new Dictionary<string, object?> { ["Height"] = "100px" };
        var animation1 = Presto.Animate(target, new() { ["Width"] = 32, ["Height"] = 200, Ease = "linear" });

        animation1.Complete();

        Assert.True(animation1.Canceled);
        Assert.True(animation1.Paused);
        Assert.Equal(1000, animation1.CurrentTime);
        Assert.Equal("200px", target["Height"]);
        Assert.Equal(32d, target["Width"]);
    }

    [Fact]
    public void CancelATimer()
    {
        var timer1 = Presto.CreateTimer(new() { Duration = 1000 });

        timer1.Seek(500).Cancel();

        Assert.True(timer1.Canceled);
        Assert.True(timer1.Paused);
    }

    [Fact]
    public void CancelAnAnimation()
    {
        var target = new Dictionary<string, object?> { ["Height"] = "100px" };
        var animation1 = Presto.Animate(target, new() { ["Width"] = 32, ["Height"] = 200, Ease = "linear" });

        animation1.Seek(500).Cancel();

        Assert.True(animation1.Canceled);
        Assert.True(animation1.Paused);
        Assert.Equal("150px", target["Height"]);
        Assert.Equal(16d, target["Width"]);
    }

    [Fact]
    public void RevertATimer()
    {
        var timer1 = Presto.CreateTimer(new() { Duration = 1000 });
        timer1.Seek(500);

        timer1.Revert();

        Assert.Equal(0, timer1.CurrentTime);
        Assert.True(timer1.Canceled);
        Assert.True(timer1.Paused);
    }

    /// <summary>Adapted: the DOM case asserts that the inline style attribute is gone, which a plain object cannot express.</summary>
    [Fact]
    public void RevertAnAnimation()
    {
        var target = new StyleTarget();
        var animation1 = Presto.Animate(target, new() { ["X"] = 200, ["TranslateX"] = "200px" });
        animation1.Seek(500);

        animation1.Revert();

        Assert.True(animation1.Canceled);
        Assert.True(animation1.Paused);
        Assert.Equal(0, target.X);
        Assert.Equal("0px", target.TranslateX);
    }

    /// <summary>Adapted: the DOM case asserts that the inline style attribute is gone, which a plain object cannot express.</summary>
    [Fact]
    public void RevertATimeline()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Ease = "linear" } })
            .Add(target, new() { ["X"] = 200 })
            .Add(target, new() { ["X"] = 400, ["TranslateX"] = "200px" });
        tl.Seek(1500);

        tl.Revert();

        Assert.True(tl.Canceled);
        Assert.True(TestHelpers.GetChildAtIndex(tl, 0)!.Canceled);
        Assert.True(TestHelpers.GetChildAtIndex(tl, 1)!.Canceled);
        Assert.True(tl.Paused);
        Assert.Equal(0, target.X);
        Assert.Equal("0px", target.TranslateX);
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
    public void ResetAPlayingAnimation()
    {
        var animation1 = Presto.Animate(new StyleTarget(), new() { ["X"] = 200, ["Y"] = 200, Autoplay = false, Duration = 1000, Loop = 1 });
        Assert.Equal(0, animation1.CurrentTime);
        Assert.True(animation1.Paused);
        Assert.False(animation1.Began);
        Assert.False(animation1.Completed);
        Assert.Equal(0, animation1.CurrentIteration);

        animation1.Seek(1500);
        animation1.Resume();
        Assert.Equal(1500, animation1.CurrentTime);
        Assert.False(animation1.Paused);
        Assert.True(animation1.Began);
        Assert.False(animation1.Completed);
        Assert.Equal(1, animation1.CurrentIteration);

        animation1.Pause();
        animation1.Seek(2000);
        Assert.Equal(2000, animation1.CurrentTime);
        Assert.True(animation1.Paused);
        Assert.True(animation1.Began);
        Assert.True(animation1.Completed);
        Assert.Equal(1, animation1.CurrentIteration);

        animation1.Seek(500);
        animation1.Resume();
        animation1.Reset();
        Assert.Equal(0, animation1.CurrentTime);
        Assert.True(animation1.Paused);
        Assert.False(animation1.Began);
        Assert.False(animation1.Completed);
        Assert.Equal(0, animation1.CurrentIteration);
    }

    [Fact]
    public void ResettingAnAnimationShouldSetItsValuesBackToTheirInitialStates()
    {
        var target = new StyleTarget();
        var originalY = target.Y;

        Presto.Animate(target, new() { ["Y"] = -100, Autoplay = false, Duration = 1000, Loop = 1 }).Reset();

        Assert.Equal(originalY, target.Y);
    }

    [Fact]
    public void ResetACanceledTimer()
    {
        var timer1 = Presto.CreateTimer(new() { Duration = 1000, Loop = 1 });
        Assert.False(timer1.Canceled);
        Assert.Equal(0, timer1.CurrentTime);
        Assert.False(timer1.Paused);
        Assert.False(timer1.Began);
        Assert.False(timer1.Completed);
        Assert.Equal(0, timer1.CurrentIteration);

        timer1.Seek(1500);
        timer1.Cancel();
        Assert.True(timer1.Canceled);
        Assert.Equal(1500, timer1.CurrentTime);
        Assert.True(timer1.Paused);
        Assert.True(timer1.Began);
        Assert.False(timer1.Completed);
        Assert.Equal(1, timer1.CurrentIteration);

        timer1.Reset();
        Assert.False(timer1.Canceled);
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
    public void ResetACanceledAnimation()
    {
        var animation1 = Presto.Animate(new StyleTarget(), new() { ["X"] = 200, ["Y"] = 200, Duration = 1000, Loop = 1 });
        Assert.False(animation1.Canceled);
        Assert.Equal(0, animation1.CurrentTime);
        Assert.False(animation1.Paused);
        Assert.False(animation1.Began);
        Assert.False(animation1.Completed);
        Assert.Equal(0, animation1.CurrentIteration);

        animation1.Seek(1500);
        animation1.Cancel();
        Assert.True(animation1.Canceled);
        Assert.Equal(1500, animation1.CurrentTime);
        Assert.True(animation1.Paused);
        Assert.True(animation1.Began);
        Assert.False(animation1.Completed);
        Assert.Equal(1, animation1.CurrentIteration);

        animation1.Reset();
        Assert.False(animation1.Canceled);
        Assert.Equal(0, animation1.CurrentTime);
        Assert.True(animation1.Paused);
        Assert.False(animation1.Began);
        Assert.False(animation1.Completed);
        Assert.Equal(0, animation1.CurrentIteration);
    }

    [Fact]
    public void RefreshAnAnimation()
    {
        var width = "128px";
        var target = new Dictionary<string, object?> { ["Width"] = "0px" };
        var animation1 = Presto.Animate(target, new()
        {
            ["Width"] = new FunctionValue((_, _, _) => width),
            Duration = 100,
            Autoplay = false,
        });

        animation1.Seek(100);
        Assert.Equal("128px", target["Width"]);

        width = "256px";
        animation1.Refresh().Restart().Seek(100);
        Assert.Equal("256px", target["Width"]);
    }

    [Fact]
    public void RefreshAnAnimationWithRelativeValues()
    {
        var target = new StyleTarget();
        var animation1 = Presto.Animate(target, new()
        {
            ["X"] = "+=50",
            ["Rotate"] = new FunctionValue((_, _, _) => "+=10"),
            Duration = 100,
            Autoplay = false,
        });

        animation1.Seek(100);
        Assert.Equal(50, target.X);
        Assert.Equal("10deg", target.Rotate);

        animation1.Refresh().Restart().Seek(100);
        Assert.Equal(50, target.X);
        Assert.Equal("20deg", target.Rotate);
    }

    [Fact]
    public void RefreshAnAnimationFromValue()
    {
        var width = "200px";
        var target = new Dictionary<string, object?> { ["Width"] = "0px" };
        var animation1 = Presto.Animate(target, new()
        {
            ["Width"] = TweenValue.FromTo(new FunctionValue((_, _, _) => width), "400px"),
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        animation1.Seek(0);
        Assert.Equal("200px", target["Width"]);
        animation1.Seek(50);
        Assert.Equal("300px", target["Width"]);
        animation1.Seek(100);
        Assert.Equal("400px", target["Width"]);

        width = "100px";
        animation1.Refresh().Restart().Seek(0);
        Assert.Equal("100px", target["Width"]);
        animation1.Seek(50);
        Assert.Equal("250px", target["Width"]);
        animation1.Seek(100);
        Assert.Equal("400px", target["Width"]);
    }

    [Fact]
    public void RefreshAnAnimationFromValueInObjectForm()
    {
        var width = "200px";
        var target = new Dictionary<string, object?> { ["Width"] = "0px" };
        var animation1 = Presto.Animate(target, new()
        {
            ["Width"] = new TweenParams { From = new FunctionValue((_, _, _) => width), To = "400px" },
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        animation1.Seek(0);
        Assert.Equal("200px", target["Width"]);
        animation1.Seek(100);
        Assert.Equal("400px", target["Width"]);

        width = "100px";
        animation1.Refresh().Restart().Seek(0);
        Assert.Equal("100px", target["Width"]);
        animation1.Seek(50);
        Assert.Equal("250px", target["Width"]);
        animation1.Seek(100);
        Assert.Equal("400px", target["Width"]);
    }

    [Fact]
    public void RefreshAnAnimationWithFromToValues()
    {
        var from = "100px";
        var to = "200px";
        var target = new Dictionary<string, object?> { ["Width"] = "0px" };
        var animation1 = Presto.Animate(target, new()
        {
            ["Width"] = TweenValue.FromTo(new FunctionValue((_, _, _) => from), new FunctionValue((_, _, _) => to)),
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        animation1.Seek(0);
        Assert.Equal("100px", target["Width"]);
        animation1.Seek(100);
        Assert.Equal("200px", target["Width"]);

        from = "50px";
        to = "150px";
        animation1.Refresh().Restart().Seek(0);
        Assert.Equal("50px", target["Width"]);
        animation1.Seek(50);
        Assert.Equal("100px", target["Width"]);
        animation1.Seek(100);
        Assert.Equal("150px", target["Width"]);
    }

    /// <summary>Adapted: the DOM case converts <c>vw</c> to <c>px</c>; the core only converts angles, so the from value arrives in turns.</summary>
    [Fact]
    public void RefreshAnAnimationWithFromToUnitConversionAndRelativeOperator()
    {
        var from = "90deg";
        var target = new StyleTarget();
        var animation1 = Presto.Animate(target, new()
        {
            ["Rotate"] = TweenValue.FromTo(new FunctionValue((_, _, _) => from), new FunctionValue((_, _, _) => "+=100")),
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        animation1.Seek(0);
        Assert.Equal("90deg", target.Rotate);
        animation1.Seek(100);
        Assert.Equal("190deg", target.Rotate);

        from = "0.75turn";
        animation1.Refresh().Restart().Seek(0);
        Assert.Equal("270deg", target.Rotate);
        animation1.Seek(100);
        Assert.Equal("370deg", target.Rotate);
    }

    [Fact]
    public void RefreshATimeline()
    {
        var width = "128px";
        var target = new Dictionary<string, object?> { ["Width"] = "0px" };
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new() { ["Width"] = new FunctionValue((_, _, _) => width), Duration = 100 })
            .Add(new TimerParams { Duration = 100 });

        tl.Seek(200);
        Assert.Equal("128px", target["Width"]);

        width = "256px";
        tl.Refresh().Restart().Seek(200);
        Assert.Equal("256px", target["Width"]);
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
    public void StretchAnAnimation()
    {
        var animation1 = Presto.Animate(new Dictionary<string, object?>(), new()
        {
            ["Width"] = new TweenParams[] { new() { To = 100, Duration = 100 }, new() { To = 100, Duration = 200 } },
            Duration = 100,
            Autoplay = false,
        });
        Assert.Equal(300, animation1.Duration);
        Assert.Equal(100, TestHelpers.GetTweenAtIndex(animation1, 0)!._updateDuration);
        Assert.Equal(200, TestHelpers.GetTweenAtIndex(animation1, 1)!._updateDuration);

        animation1.Stretch(600);
        Assert.Equal(600, animation1.Duration);
        Assert.Equal(200, TestHelpers.GetTweenAtIndex(animation1, 0)!._updateDuration);
        Assert.Equal(400, TestHelpers.GetTweenAtIndex(animation1, 1)!._updateDuration);

        animation1.Stretch(30);
        Assert.Equal(30, animation1.Duration);
        Assert.Equal(10, TestHelpers.GetTweenAtIndex(animation1, 0)!._updateDuration);
        Assert.Equal(20, TestHelpers.GetTweenAtIndex(animation1, 1)!._updateDuration);

        animation1.Stretch(0);
        Assert.Equal(Constants.MinValue, animation1.Duration);
        Assert.Equal(Constants.MinValue, TestHelpers.GetTweenAtIndex(animation1, 0)!._updateDuration);
        Assert.Equal(Constants.MinValue, TestHelpers.GetTweenAtIndex(animation1, 1)!._updateDuration);

        // Once stretched to 0 every tween collapses onto the same duration.
        animation1.Stretch(30);
        Assert.Equal(30, animation1.Duration);
        Assert.Equal(30, TestHelpers.GetTweenAtIndex(animation1, 0)!._updateDuration);
        Assert.Equal(30, TestHelpers.GetTweenAtIndex(animation1, 1)!._updateDuration);
    }

    [Fact]
    public void StretchScalesKeyframeDelaysProportionally()
    {
        var animation1 = Presto.Animate(new Dictionary<string, object?>(), new()
        {
            ["Width"] = new TweenParams[]
            {
                new() { From = 0, To = 100, Duration = 100 },
                new() { To = 500, Delay = 200, Duration = 500 },
                new() { To = 300, Delay = 150, Duration = 100 },
            },
            Autoplay = false,
        });
        var tween0 = TestHelpers.GetTweenAtIndex(animation1, 0)!;
        var tween1 = TestHelpers.GetTweenAtIndex(animation1, 1)!;
        var tween2 = TestHelpers.GetTweenAtIndex(animation1, 2)!;

        Assert.Equal(1050, animation1.Duration);
        AssertTweenTimings(tween0, delay: 0, updateDuration: 100, startTime: 0);
        AssertTweenTimings(tween1, delay: 200, updateDuration: 500, startTime: 300);
        AssertTweenTimings(tween2, delay: 150, updateDuration: 100, startTime: 950);

        animation1.Stretch(2100);
        Assert.Equal(2100, animation1.Duration);
        AssertTweenTimings(tween0, delay: 0, updateDuration: 200, startTime: 0);
        AssertTweenTimings(tween1, delay: 400, updateDuration: 1000, startTime: 600);
        AssertTweenTimings(tween2, delay: 300, updateDuration: 200, startTime: 1900);

        animation1.Stretch(525);
        Assert.Equal(525, animation1.Duration);
        AssertTweenTimings(tween0, delay: 0, updateDuration: 50, startTime: 0);
        AssertTweenTimings(tween1, delay: 100, updateDuration: 250, startTime: 150);
        AssertTweenTimings(tween2, delay: 75, updateDuration: 50, startTime: 475);
    }

    [Fact]
    public void StretchScalesGlobalDelayAndKeyframeDelaysProportionally()
    {
        var animation1 = Presto.Animate(new Dictionary<string, object?>(), new()
        {
            ["Width"] = new TweenParams[]
            {
                new() { From = 0, To = 100, Duration = 100 },
                new() { To = 200, Delay = 200, Duration = 300 },
            },
            Delay = 50,
            Autoplay = false,
        });
        var tween0 = TestHelpers.GetTweenAtIndex(animation1, 0)!;
        var tween1 = TestHelpers.GetTweenAtIndex(animation1, 1)!;

        Assert.Equal(600, animation1.Duration);
        Assert.Equal(50, animation1._delay);
        AssertTweenTimings(tween0, delay: 0, updateDuration: 100, startTime: 0);
        AssertTweenTimings(tween1, delay: 200, updateDuration: 300, startTime: 300);

        animation1.Stretch(1200);
        Assert.Equal(1200, animation1.Duration);
        Assert.Equal(100, animation1._delay);
        AssertTweenTimings(tween0, delay: 0, updateDuration: 200, startTime: 0);
        AssertTweenTimings(tween1, delay: 400, updateDuration: 600, startTime: 600);
    }

    [Fact]
    public void StretchATimeline()
    {
        var target = new Dictionary<string, object?>();
        TweenValue keyframes = new TweenParams[] { new() { To = 100, Duration = 100 }, new() { To = 100, Duration = 200 } };
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new() { ["Width"] = keyframes })
            .Add(target, new() { ["Width"] = keyframes })
            .Label("middle", 300);

        Assert.Equal(600, tl.Duration);
        Assert.Equal(300, TestHelpers.GetChildAtIndex(tl, 0)!.Duration);
        Assert.Equal(300, TestHelpers.GetChildAtIndex(tl, 1)!.Duration);

        tl.Stretch(1200);
        Assert.Equal(1200, tl.Duration);
        Assert.Equal(600, TestHelpers.GetChildAtIndex(tl, 0)!.Duration);
        Assert.Equal(600, TestHelpers.GetChildAtIndex(tl, 1)!.Duration);
        Assert.Equal(600, tl.Labels["middle"]);

        tl.Stretch(300);
        Assert.Equal(300, tl.Duration);
        Assert.Equal(150, TestHelpers.GetChildAtIndex(tl, 0)!.Duration);
        Assert.Equal(150, TestHelpers.GetChildAtIndex(tl, 1)!.Duration);
        Assert.Equal(150, tl.Labels["middle"]);
    }

    private static void AssertTweenTimings(Tween tween, double delay, double updateDuration, double startTime)
    {
        Assert.Equal(delay, tween._delay);
        Assert.Equal(updateDuration, tween._updateDuration);
        Assert.Equal(startTime, tween._startTime);
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
    public void SeekAnAnimation()
    {
        var animation1 = Presto.Animate(new Dictionary<string, object?>(), new() { ["Width"] = 200, Duration = 200, Autoplay = false });
        Assert.Equal(0, animation1.CurrentTime);
        Assert.Equal(0, animation1.Progress);
        Assert.False(animation1.Began);
        Assert.False(animation1.Completed);

        animation1.Seek(100);
        Assert.Equal(100, animation1.CurrentTime);
        Assert.Equal(.5, animation1.Progress);
        Assert.True(animation1.Began);
        Assert.False(animation1.Completed);

        animation1.Seek(200);
        Assert.Equal(200, animation1.CurrentTime);
        Assert.Equal(1, animation1.Progress);
        Assert.True(animation1.Began);
        Assert.True(animation1.Completed);

        animation1.Seek(150);
        Assert.Equal(150, animation1.CurrentTime);
        Assert.Equal(.75, animation1.Progress);
        Assert.True(animation1.Began);
        Assert.False(animation1.Completed);

        animation1.Resume();
        var completed = false;
        Presto.CreateTimer(new() { Duration = 65, OnComplete = _ => completed = true });
        RunUntil(() => completed);
        animation1.Pause();

        Assert.Equal(200, animation1.CurrentTime);
        Assert.Equal(1, animation1.Progress);
        Assert.True(animation1.Completed);
    }

    [Fact]
    public void GetAndSetCanceledOnATimer()
    {
        var timer = Presto.CreateTimer(new() { Delay = 10, Duration = 55, LoopDelay = 10, Loop = 1, Autoplay = false });
        Assert.False(timer.Canceled);

        timer.Canceled = true;
        Assert.True(timer.Canceled);
        Assert.True(timer.Paused);

        timer.Canceled = false;
        Assert.False(timer.Canceled);
        Assert.False(timer.Paused);
    }

    /// <summary>The box the timeline cases animate; the suites read it back off the DOM element's inline style.</summary>
    private static Dictionary<string, object?> BoxTarget() => new()
    {
        ["Width"] = 0d,
        ["Height"] = "100px",
        ["TranslateX"] = "0px",
        ["TranslateY"] = "100px",
    };

    private static Timeline BoxTimeline(Dictionary<string, object?> target, TimelineParams parameters) =>
        Presto.CreateTimeline(parameters)
            .Add(target, new() { ["Width"] = 32, ["Height"] = 200 })
            .Add(target, new() { ["Width"] = 64, ["Height"] = 400, ["TranslateX"] = 200, ["TranslateY"] = 200 });

    [Fact]
    public void CompleteATimeline()
    {
        var target = BoxTarget();
        var tl = BoxTimeline(target, new() { Defaults = new() { Ease = "linear" } });
        var child0 = TestHelpers.GetChildAtIndex(tl, 0)!;
        var child1 = TestHelpers.GetChildAtIndex(tl, 1)!;

        tl.Complete();

        Assert.True(tl.Canceled);
        Assert.True(child0.Canceled);
        Assert.True(child1.Canceled);
        Assert.True(tl.Paused);
        Assert.True(child0.Paused);
        Assert.True(child1.Paused);
        Assert.Equal(tl.Duration, tl.CurrentTime);
        Assert.Equal(child0.Duration, child0.CurrentTime);
        Assert.Equal(child1.Duration, child1.CurrentTime);
        Assert.Equal(64d, target["Width"]);
        Assert.Equal("400px", target["Height"]);
        Assert.Equal("200px", target["TranslateX"]);
        Assert.Equal("200px", target["TranslateY"]);
    }

    [Fact]
    public void CancelATimeline()
    {
        var target = BoxTarget();
        var tl = BoxTimeline(target, new() { Defaults = new() { Ease = "linear" } });
        var child0 = TestHelpers.GetChildAtIndex(tl, 0)!;
        var child1 = TestHelpers.GetChildAtIndex(tl, 1)!;

        tl.Seek(1000);
        tl.Cancel();

        Assert.True(tl.Canceled);
        Assert.True(child0.Canceled);
        Assert.True(child1.Canceled);
        Assert.True(tl.Paused);
        Assert.True(child0.Paused);
        Assert.True(child1.Paused);
        Assert.Equal(32d, target["Width"]);
        Assert.Equal("200px", target["Height"]);
        Assert.Equal("0px", target["TranslateX"]);
        Assert.Equal("100px", target["TranslateY"]);
    }

    [Fact]
    public void ResetAPlayingTimeline()
    {
        var tl = BoxTimeline(BoxTarget(), new() { Autoplay = false, Duration = 1000, Loop = 1 });
        var child0 = TestHelpers.GetChildAtIndex(tl, 0)!;
        var child1 = TestHelpers.GetChildAtIndex(tl, 1)!;
        AssertTimelineIsAtTheStart(tl, child0, child1);

        tl.Seek(3000);
        tl.Resume();
        Assert.Equal(3000, tl.CurrentTime);
        Assert.Equal(.75, tl.Progress);
        Assert.False(tl.Paused);
        Assert.True(tl.Began);
        Assert.False(tl.Completed);
        Assert.Equal(1, tl.CurrentIteration);

        tl.Pause();
        tl.Seek(4000);
        Assert.Equal(4000, tl.CurrentTime);
        Assert.Equal(1, tl.Progress);
        Assert.True(tl.Paused);
        Assert.True(tl.Began);
        Assert.True(tl.Completed);
        Assert.Equal(1, tl.CurrentIteration);
        foreach (var child in new[] { child0, child1 })
        {
            Assert.True(child.Paused);
            Assert.True(child.Began);
            Assert.True(child.Completed);
        }

        tl.Seek(500);
        tl.Resume();
        tl.Reset();
        AssertTimelineIsAtTheStart(tl, child0, child1);
        tl.Pause();
    }

    [Fact]
    public void ResettingATimelineShouldSetItsValuesBackToTheirInitialStates()
    {
        var target = new StyleTarget();
        var originalY = target.Y;

        Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new() { ["Y"] = -100, Duration = 500 }, 1000)
            .Add(target, new() { ["Y"] = 100, Duration = 500 }, 2000)
            .Reset();

        Assert.Equal(originalY, target.Y);
    }

    [Fact]
    public void ResetACanceledTimeline()
    {
        var tl = BoxTimeline(BoxTarget(), new() { Duration = 1000, Loop = 1 });
        var child0 = TestHelpers.GetChildAtIndex(tl, 0)!;
        var child1 = TestHelpers.GetChildAtIndex(tl, 1)!;

        Assert.False(tl.Canceled);
        Assert.Equal(0, tl.CurrentTime);
        Assert.Equal(0, tl.Progress);
        Assert.False(tl.Paused);
        Assert.False(tl.Began);
        Assert.False(tl.Completed);
        Assert.Equal(0, tl.CurrentIteration);
        foreach (var child in new[] { child0, child1 })
        {
            Assert.False(child.Canceled);
            Assert.Equal(0, child.CurrentTime);
            Assert.Equal(0, child.Progress);
            Assert.True(child.Paused);
            Assert.False(child.Began);
            Assert.False(child.Completed);
        }

        tl.Seek(3000);
        tl.Cancel();
        Assert.True(tl.Canceled);
        Assert.Equal(3000, tl.CurrentTime);
        Assert.Equal(.75, tl.Progress);
        Assert.True(tl.Paused);
        Assert.True(tl.Began);
        Assert.False(tl.Completed);
        Assert.Equal(1, tl.CurrentIteration);
        foreach (var child in new[] { child0, child1 })
        {
            Assert.True(child.Canceled);
            Assert.True(child.Paused);
        }

        tl.Reset();
        Assert.False(tl.Canceled);
        AssertTimelineIsAtTheStart(tl, child0, child1);
        tl.Pause();
    }

    private static void AssertTimelineIsAtTheStart(Timeline tl, params Timer[] children)
    {
        Assert.Equal(0, tl.CurrentTime);
        Assert.Equal(0, tl.Progress);
        Assert.True(tl.Paused);
        Assert.False(tl.Began);
        Assert.False(tl.Completed);
        Assert.Equal(0, tl.CurrentIteration);
        foreach (var child in children)
        {
            Assert.False(child.Canceled);
            Assert.Equal(0, child.CurrentTime);
            Assert.Equal(0, child.Progress);
            Assert.True(child.Paused);
            Assert.False(child.Began);
            Assert.False(child.Completed);
            Assert.Equal(0, child.CurrentIteration);
        }
    }

    [Fact]
    public void StretchATimelineScalesKeyframeDelaysOfAnimationChildren()
    {
        var target = new Dictionary<string, object?>();
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new()
            {
                ["Width"] = new TweenParams[] { new() { From = 0, To = 100, Duration = 100 }, new() { To = 200, Delay = 200, Duration = 300 } },
            })
            .Add(target, new()
            {
                ["Height"] = new TweenParams[] { new() { From = 0, To = 100, Duration = 100 }, new() { To = 200, Delay = 100, Duration = 200 } },
            });
        var child0 = TestHelpers.GetChildAtIndex(tl, 0)!;
        var child1 = TestHelpers.GetChildAtIndex(tl, 1)!;
        var c0t0 = TestHelpers.GetTweenAtIndex(child0, 0)!;
        var c0t1 = TestHelpers.GetTweenAtIndex(child0, 1)!;
        var c1t0 = TestHelpers.GetTweenAtIndex(child1, 0)!;
        var c1t1 = TestHelpers.GetTweenAtIndex(child1, 1)!;

        Assert.Equal(600, child0.Duration);
        Assert.Equal(400, child1.Duration);
        Assert.Equal(1000, tl.Duration);
        Assert.Equal(200, c0t1._delay);
        Assert.Equal(300, c0t1._updateDuration);
        Assert.Equal(100, c1t1._delay);
        Assert.Equal(200, c1t1._updateDuration);

        tl.Stretch(2000);

        Assert.Equal(2000, tl.Duration);
        Assert.Equal(1200, child0.Duration);
        Assert.Equal(800, child1.Duration);
        AssertTweenTimings(c0t0, delay: 0, updateDuration: 200, startTime: 0);
        AssertTweenTimings(c0t1, delay: 400, updateDuration: 600, startTime: 600);
        AssertTweenTimings(c1t0, delay: 0, updateDuration: 200, startTime: 0);
        AssertTweenTimings(c1t1, delay: 200, updateDuration: 400, startTime: 400);
    }

    [Fact]
    public void SeekATimeline()
    {
        var target = new Dictionary<string, object?>();
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new() { ["Width"] = 200, Duration = 100 })
            .Add(target, new() { ["Width"] = 400, Duration = 100 });
        Assert.Equal(0, tl.CurrentTime);
        Assert.Equal(0, tl.Progress);
        Assert.False(tl.Began);
        Assert.False(tl.Completed);

        tl.Seek(100);
        Assert.Equal(100, tl.CurrentTime);
        Assert.Equal(.5, tl.Progress);
        Assert.True(tl.Began);
        Assert.False(tl.Completed);

        tl.Seek(200);
        Assert.Equal(200, tl.CurrentTime);
        Assert.Equal(1, tl.Progress);
        Assert.True(tl.Began);
        Assert.True(tl.Completed);

        tl.Seek(150);
        Assert.Equal(150, tl.CurrentTime);
        Assert.Equal(.75, tl.Progress);
        Assert.True(tl.Began);
        Assert.False(tl.Completed);

        tl.Resume();
        var completed = false;
        Presto.CreateTimer(new() { Duration = 65, OnComplete = _ => completed = true });
        RunUntil(() => completed);
        tl.Pause();

        Assert.Equal(200, tl.CurrentTime);
        Assert.Equal(1, tl.Progress);
        Assert.True(tl.Completed);
    }
}
