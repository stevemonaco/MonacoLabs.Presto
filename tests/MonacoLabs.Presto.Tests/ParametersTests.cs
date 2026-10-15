using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Port of anime.js parameters.test.js (frameRate and playbackRate cases) adapted to timers, the per-property parameters
/// of an animation, plus the engine-wide Speed and Fps equivalents.
/// </summary>
public sealed class ParametersTests : EngineTestBase
{
    [Fact]
    public void PlaybackRateOnTimer()
    {
        var timer = Presto.CreateTimer(new() { PlaybackRate = .5, Duration = 100 });
        double? probed = null;
        Presto.CreateTimer(new() { Duration = 100, OnComplete = _ => probed = timer.CurrentTime });

        RunUntil(() => probed != null);

        Assert.NotNull(probed);
        Assert.InRange(probed.Value, 40, 60);
    }

    [Fact]
    public void FrameRateThrottlesUpdates()
    {
        var updates = 0;
        Presto.CreateTimer(new() { FrameRate = 1, Duration = 10000, OnUpdate = _ => updates++ });

        Frames.Advance(1500, 16);

        Assert.InRange(updates, 1, 2);
    }

    [Fact]
    public void EngineSpeedDoublesTimerPlayback()
    {
        Presto.Engine.Speed = 2;
        double? completedAt = null;
        Presto.CreateTimer(new() { Duration = 100, OnComplete = _ => completedAt = Frames.Now });

        RunUntil(() => completedAt != null);

        Assert.NotNull(completedAt);
        Assert.InRange(completedAt.Value, 50 - 17, 50 + 17);
    }

    [Fact]
    public void ChangingEngineSpeedContinuesRunningTimersFromTheirCurrentTime()
    {
        var timer = Presto.CreateTimer(new() { Duration = 100 });
        Frames.Advance(40);
        Assert.Equal(52, timer.CurrentTime);

        Presto.Engine.Speed = 2;
        Assert.Equal(2, Presto.Engine._speed);
        Frames.Advance(16);

        Assert.Equal(52 + 16 * 2, timer.CurrentTime);
        Frames.Advance(16);
        Assert.True(timer.Completed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EngineSpeedBelowMinValueClampsAndKeepsTimersFinite(double speed)
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(40);
        var before = timer.CurrentTime;

        Presto.Engine.Speed = speed;
        Assert.Equal(Constants.MinValue, Presto.Engine.Speed);
        Frames.Advance(16);
        Frames.Advance(16);

        Assert.True(double.IsFinite(timer.CurrentTime));
        Assert.InRange(timer.CurrentTime, before, before + 1);

        Presto.Engine.Speed = 1;
        var paused = timer.CurrentTime;
        Frames.Advance(16);
        Assert.Equal(paused + 16, timer.CurrentTime, 6);
    }

    [Fact]
    public void EngineFpsThrottlesUpdates()
    {
        var updates = 0;
        Presto.CreateTimer(new() { Duration = 10000, OnUpdate = _ => updates++ });
        Frames.Advance(100, 16);
        var updatesAt240Fps = updates;

        Presto.Engine.Fps = 30;
        updates = 0;
        Frames.Advance(100, 16);

        Assert.True(updatesAt240Fps >= 6, $"Expected every 16 ms frame to tick at 240 fps, got {updatesAt240Fps} updates.");
        Assert.InRange(updates, 1, updatesAt240Fps / 2);
    }

    [Fact]
    public void SpecificPropertyParameters()
    {
        const double duration = 10;
        Func<double, double> propertyModifier = v => Helpers.Round(v, 1);
        Func<double, double> instanceModifier = v => Helpers.Round(v, 2);

        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            ["TranslateX"] = new TweenParams { To = 100, Ease = "linear", Modifier = propertyModifier, Delay = duration * .25, Duration = duration * .60 },
            ["Rotate"] = new TweenParams { To = 360, Duration = duration * .50 },
            ["TranslateY"] = 200,
            Ease = "outQuad",
            Modifier = instanceModifier,
            Delay = duration * .35,
            Duration = duration * .70,
            Autoplay = false,
        });

        var translateX = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Equal(.5, translateX._ease(.5));
        Assert.Same(propertyModifier, translateX._modifier);
        Assert.Equal(0, translateX._delay);
        Assert.Equal(duration * .60, translateX._changeDuration);

        var rotate = TestHelpers.GetTweenAtIndex(animation, 1)!;
        Assert.Equal(.75, rotate._ease(.5));
        Assert.Same(instanceModifier, rotate._modifier);
        Assert.Equal(duration * .1, rotate._delay);
        Assert.Equal(duration * .50, rotate._changeDuration);

        var translateY = TestHelpers.GetTweenAtIndex(animation, 2)!;
        Assert.Equal(.75, translateY._ease(.5));
        Assert.Same(instanceModifier, translateY._modifier);
        Assert.Equal(duration * .1, translateY._delay);
        Assert.Equal(duration * .70, translateY._changeDuration);

        Assert.Equal("0px", target.TranslateX);
        Assert.Equal("0px", target.TranslateY);
        Assert.Equal("0deg", target.Rotate);

        animation.Pause().Seek(animation.Duration * .5);

        Assert.Equal("66.7px", target.TranslateX);
        Assert.Equal("134.69px", target.TranslateY);
        Assert.Equal("302.4deg", target.Rotate);
    }

    [Fact]
    public void SpecificPropertyParametersWhenTheLastPropertyUpdatesAfterEverythingElse()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            ["TranslateX"] = new TweenParams { To = 250, Duration = 400, Ease = "linear" },
            ["Rotate"] = new TweenParams { To = 360, Duration = 900, Ease = "linear" },
            ["Scale"] = new TweenParams { To = 2, Duration = 800, Delay = 400, Ease = "inOutQuart" },
            Delay = 100,
        });

        var probed = false;
        Presto.CreateTimer(new() { Duration = 200, OnComplete = _ => probed = true });
        RunUntil(() => probed);
        animation.Pause();

        Assert.InRange(JsNumber.ToNumber(target.TranslateX.AsSpan()[..^2]), 55, 75);
        Assert.InRange(JsNumber.ToNumber(target.Rotate.AsSpan()[..^3]), 30, 50);
        Assert.InRange(target.Scale, 0, 2);
    }

    [Fact]
    public void Modifier()
    {
        const double duration = 10;
        Func<double, double> round = v => Helpers.Round(v, 0);
        var target = new TestObject();

        Presto.Animate(target, new() { ["PlainValue"] = 3.14159265359, Duration = duration, Autoplay = false }).Seek(duration);
        Assert.Equal(3.14159265359, target.PlainValue);

        Presto.Animate(target, new() { ["PlainValue"] = 3.14159265359, Duration = duration, Modifier = round, Autoplay = false }).Seek(duration);
        Assert.Equal(3, target.PlainValue);

        Presto.Animate(target, new() { ["ValueWithUnit"] = "3.14159265359px", Duration = duration, Modifier = round, Autoplay = false }).Seek(duration);
        Assert.Equal("3px", target.ValueWithUnit);

        Presto.Animate(target, new()
        {
            ["MultiplePlainValues"] = "26.11111111 42.11111111 74.11111111 138.11111111",
            Duration = duration,
            Modifier = round,
            Autoplay = false,
        }).Seek(duration);
        Assert.Equal("26 42 74 138", target.MultiplePlainValues);
    }

    [Fact]
    public void FrameRateThrottlesAnimationRendering()
    {
        var target = new TestObject();
        var animation = Presto.Animate(target, new()
        {
            ["PlainValue"] = TweenValue.FromTo(0, 100),
            FrameRate = 1,
            Duration = 10000,
            Ease = "linear",
        });

        // A 1 fps animation renders nothing before its first tick a second in, so the value holds at the from value.
        RunUntil(() =>
        {
            Assert.InRange(target.PlainValue, -2, 2);
            return animation.Progress >= .05;
        });

        Assert.True(animation.Progress >= .05);
    }

    [Fact]
    public void PlaybackRateOnAnimation()
    {
        var target = new TestObject();
        Presto.Animate(target, new()
        {
            ["PlainValue"] = TweenValue.FromTo(0, 100),
            PlaybackRate = .5,
            Duration = 100,
            Ease = "linear",
        });

        var probed = double.NaN;
        Presto.CreateTimer(new() { Duration = 100, OnComplete = _ => probed = target.PlainValue });
        RunUntil(() => !double.IsNaN(probed));

        Assert.InRange(probed, 40, 60);
    }

    [Fact]
    public void PlaybackEaseOnAnimation()
    {
        var target = new TestObject();
        Presto.Animate(target, new()
        {
            ["PlainValue"] = TweenValue.FromTo(0, 100),
            PlaybackEase = "outQuad",
            Duration = 100,
            Ease = "linear",
        });

        var probed = double.NaN;
        Presto.CreateTimer(new() { Duration = 50, OnComplete = _ => probed = target.PlainValue });
        RunUntil(() => !double.IsNaN(probed));

        Assert.InRange(probed, 70, 90);
    }

    [Fact]
    public void PlaybackRateOnTimeline()
    {
        var target = new TestObject();
        Presto.CreateTimeline(new() { PlaybackRate = .5 })
            .Add(target, new()
            {
                ["PlainValue"] = TweenValue.FromTo(0, 100),
                PlaybackRate = .5,
                Duration = 100,
                Ease = "linear",
            });

        var probed = double.NaN;
        Presto.CreateTimer(new() { Duration = 100, OnComplete = _ => probed = target.PlainValue });
        RunUntil(() => !double.IsNaN(probed));

        Assert.InRange(probed, 15, 35);
    }

    [Fact]
    public void PlaybackEaseOnTimeline()
    {
        var target = new TestObject();
        Presto.CreateTimeline(new() { PlaybackEase = "outQuad" })
            .Add(target, new()
            {
                ["PlainValue"] = TweenValue.FromTo(0, 100),
                PlaybackEase = "outQuad",
                Duration = 100,
                Ease = "linear",
            });

        var probed = double.NaN;
        Presto.CreateTimer(new() { Duration = 50, OnComplete = _ => probed = target.PlainValue });
        RunUntil(() => !double.IsNaN(probed));

        Assert.InRange(probed, 85, 100);
    }

    [Fact]
    public void Delay()
    {
        var animation = Presto.Animate(new TestObject(), new()
        {
            ["PlainValue"] = TweenValue.FromTo(0, 100),
            Delay = 100,
            Duration = 100,
        });

        double[] probes = [double.NaN, double.NaN, double.NaN];
        Presto.CreateTimer(new() { Duration = 50, OnComplete = _ => probes[0] = animation.CurrentTime });
        Presto.CreateTimer(new() { Duration = 150, OnComplete = _ => probes[1] = animation.CurrentTime });
        Presto.CreateTimer(new() { Duration = 200, OnComplete = _ => probes[2] = animation.CurrentTime });
        RunUntil(() => !double.IsNaN(probes[2]));

        Assert.InRange(probes[0], -60, -20);
        Assert.InRange(probes[1], 30, 70);
        Assert.Equal(100, probes[2]);
    }

    [Fact]
    public void ZeroDurationAnimation()
    {
        var target = new StyleTarget();
        Presto.Animate(target, new() { ["X"] = 100, Duration = 0 });

        Assert.Equal(100, target.X);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    public void ZeroDurationAnimationWaitsOutItsDelay(double delay)
    {
        var target = new StyleTarget();
        var completions = 0;
        Timer animation = Presto.Animate(target, new() { ["X"] = 100, Duration = 0, Delay = delay, OnComplete = _ => completions++ });
        var then = animation.Then();

        Assert.Equal(0, target.X);
        Frames.Advance(delay / 2 - 12, 5);
        Assert.Equal(0, target.X);
        Assert.False(then.IsCompleted);

        Frames.Advance(delay, 5);
        Assert.Equal(100, target.X);
        Assert.Equal(1, completions);
        Assert.True(then.IsCompleted);
    }

    [Fact]
    public void ZeroDurationTimerWithInfiniteLoop() =>
        Assert.Equal(Constants.MinValue, Presto.CreateTimer(new() { Duration = 0, Loop = true, Autoplay = false }).Duration);

    [Fact]
    public void ZeroDurationAnimationWithInfiniteLoop()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            ["X"] = TweenValue.FromTo(-100, 100),
            ["Y"] = TweenValue.FromTo(-100, 100),
            Duration = 0,
            Loop = true,
        });

        Assert.Equal(Constants.MinValue, animation.Duration);
        Assert.Equal(100, target.X);
        Assert.Equal(100, target.Y);
    }

    [Fact]
    public void ZeroDurationTimelineWithInfiniteLoop()
    {
        var tl = Presto.CreateTimeline(new() { Loop = true, Autoplay = false })
            .Add(new StyleTarget(), new() { ["X"] = 100, Duration = 0, Loop = true });

        Assert.Equal(Constants.MinValue, tl.Duration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void FrameRateZeroOrNegativeRendersEveryFrame(double frameRate)
    {
        var updates = 0;
        Presto.CreateTimer(new() { FrameRate = frameRate, Duration = 10000, OnUpdate = _ => updates++ });
        Frames.Advance(160, 16);

        Assert.Equal(10, updates);
    }
}
