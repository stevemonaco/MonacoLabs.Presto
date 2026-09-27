using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

/// <summary>Port of anime.js tests/suites/animations.test.js, minus its DOM specific tween type cases.</summary>
public sealed class AnimationsTests : EngineTestBase
{
    [Fact]
    public void TweensTimingInheritance()
    {
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["TranslateX"] = new TweenParams[]
            {
                new() { To = 50, Delay = 15, Duration = 10 },
                new() { To = 200, Delay = 35, Duration = 30 },
                new() { To = 350, Delay = 15, Duration = 10 },
            },
            Autoplay = false,
        });

        // The first delay is not counted in the total duration.
        Assert.Equal(100, animation.Duration);
        Assert.Equal(100, animation.IterationDuration);
    }

    [Fact]
    public void CurrentTimeIsZeroIfNotPlayed()
    {
        var animation = Presto.Animate(new StyleTarget(), new() { ["Opacity"] = TweenValue.FromTo(0, 1), Duration = 300, Autoplay = false });

        Assert.Equal(0, animation.CurrentTime);
    }

    [Fact]
    public void CompletesInstantlyIfNoAnimatablePropsProvided()
    {
        var animation = Presto.Animate(new StyleTarget(), new() { Duration = 15, Loop = true });

        Frames.Advance(30, 16);

        Assert.Equal(Constants.MinValue, animation.Duration);
        Assert.True(animation.Paused);
        Assert.True(animation.Completed);
    }

    [Fact]
    public void ValuesEndAtTheirCorrectEndPositionWhenSeeked()
    {
        IndexedTarget[] targets = [new(), new(), new(), new()];
        var animation = Presto.Animate(targets, new()
        {
            ["TranslateX"] = 270,
            Delay = new FunctionValue((_, i, _) => i * 10),
            Ease = "inOutSine",
            Autoplay = false,
        });

        foreach (var target in targets) Assert.Equal("0px", target.TranslateX);

        var completed = false;
        Presto.CreateTimer(new()
        {
            Duration = 35,
            OnUpdate = seeker => animation.Seek(seeker.Progress * animation.Duration),
            OnComplete = _ => completed = true,
        });
        RunUntil(() => completed);
        animation.Pause();

        foreach (var target in targets) Assert.Equal("270px", target.TranslateX);
    }

    [Fact]
    public void ValuesEndAtTheirCorrectStartPositionWhenSeekedInReverse()
    {
        IndexedTarget[] targets = [new(), new(), new(), new()];
        var completed = false;
        var animation = Presto.Animate(targets, new()
        {
            ["TranslateX"] = 270,
            Reversed = true,
            Ease = "linear",
            Duration = 35,
            OnComplete = _ => completed = true,
        });

        animation.Seek(0);
        foreach (var target in targets) Assert.Equal("270px", target.TranslateX);

        RunUntil(() => completed);

        foreach (var target in targets) Assert.Equal("0px", target.TranslateX);
    }

    [Fact]
    public void CanceledTweenShouldUpdate()
    {
        var target = new StyleTarget();
        Presto.Animate(target, new()
        {
            ["TranslateX"] = new TweenParams[] { new() { To = TweenValue.FromTo(0, 200), Duration = 20 }, new() { To = 300, Duration = 20 } },
        });

        Presto.CreateTimer(new() { Duration = 20, OnComplete = _ => Presto.Animate(target, new() { ["TranslateX"] = -100, Duration = 20 }) });
        var done = false;
        Presto.CreateTimer(new() { Duration = 80, OnComplete = _ => done = true });
        RunUntil(() => done);

        Assert.Equal("-100px", target.TranslateX);
    }

    [Fact]
    public void AnimateTheProgressOfAnAnimationWithZeroDurationTweens()
    {
        StyleTarget[] targets = [new(), new(), new(), new()];
        var inner = Presto.Animate(targets, new()
        {
            ["Opacity"] = TweenValue.FromTo(0, 1),
            Duration = 0,
            Delay = new FunctionValue((_, i, _) => i * 10),
            Autoplay = false,
        });

        var completed = false;
        var outer = Presto.Animate(inner, new()
        {
            ["Progress"] = TweenValue.FromTo(0, 1),
            Ease = "linear",
            Duration = 40,
            OnComplete = _ => completed = true,
        });
        RunUntil(() => completed);

        Assert.Equal(1, outer.Progress);
        Assert.Equal(Helpers.Round(outer.Duration, 6), outer.CurrentTime);
        Assert.Equal(1, inner.Progress);
        Assert.Equal(Helpers.Round(inner.Duration, 6), inner.CurrentTime);
    }

    [Fact]
    public void AdvancesByOneFrameImmediatelyAfterBeingPlayed()
    {
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["Opacity"] = TweenValue.FromTo(0, 1),
            FrameRate = 60,
            Duration = 300,
            Autoplay = false,
        });
        animation.Play();

        Frames.Advance(16);

        var currentTime = animation.CurrentTime;
        Assert.True(currentTime >= 16 && currentTime < 33, $"Expected one frame of progress, got {currentTime}.");
    }
}
