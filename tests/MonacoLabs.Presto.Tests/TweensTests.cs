using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

/// <summary>Port of anime.js tests/suites/tweens.test.js.</summary>
public sealed class TweensTests : EngineTestBase
{
    [Fact]
    public void SingleTweenTimings()
    {
        var animation = Presto.Animate(new StyleTarget(), new() { ["TranslateX"] = "100%", Delay = 200, Duration = 300, Autoplay = false });

        var first = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Equal(0, first._startTime);
        Assert.Equal(300, first._startTime + first._updateDuration);
        Assert.Equal(300, animation.Duration);
    }

    [Fact]
    public void KeyframesTweenTimings()
    {
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["TranslateX"] = new TweenParams[]
            {
                new() { To = "100%", Delay = 200, Duration = 300 },
                new() { To = "200%", Delay = 300, Duration = 400 },
            },
            Autoplay = false,
        });

        var first = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Equal(0, first._startTime);
        Assert.Equal(300, first._startTime + first._updateDuration);

        var second = TestHelpers.GetTweenAtIndex(animation, 1)!;
        Assert.Equal(600, second._startTime);
        Assert.Equal(1000, second._startTime + second._updateDuration);

        Assert.Equal(1000, animation.Duration);
    }

    [Fact]
    public void SimpleTweenEase()
    {
        var animation = Presto.Animate(new StyleTarget(), new() { ["TranslateX"] = "100%", Ease = "linear", Autoplay = false });

        Assert.Equal(.5, TestHelpers.GetTweenAtIndex(animation, 0)!._ease(.5));
    }

    [Fact]
    public void ColorTween()
    {
        var animation = Presto.Animate(new StyleTarget(), new() { ["TranslateX"] = "100%", ["BackgroundColor"] = "#000", Autoplay = false });

        Assert.Equal(ValueKind.Color, TestHelpers.GetTweenAtIndex(animation, 1)!._valueType);
    }

    [Fact]
    public void DoNotRemoveTweenSiblingsOnAnimationPause()
    {
        var target = new StyleTarget();
        var animation1 = Presto.Animate(target, new()
        {
            ["TranslateX"] = new TweenParams[] { new() { To = -50 }, new() { To = 200 } },
            Duration = 100,
        });
        var animation2 = Presto.Animate(target, new()
        {
            ["TranslateX"] = new TweenParams[] { new() { To = 100 }, new() { To = -100 } },
            Duration = 100,
            Delay = 50,
        });

        var done = false;
        Presto.CreateTimer(new()
        {
            Duration = 50,
            OnComplete = _ =>
            {
                animation1.Pause();
                animation2.Pause();
                done = true;
            },
        });
        RunUntil(() => done);

        Assert.True(animation1.Paused);
        Assert.Null(animation1._headTween!._nextRep);
        Assert.True(animation2.Paused);
        Assert.NotNull(animation2._headTween!._nextRep);
    }

    [Fact]
    public void RemoveTweenSiblingsOnAnimationComplete()
    {
        var target = new StyleTarget();
        var animation1 = Presto.Animate(target, new() { ["TranslateX"] = 200, Duration = 50 });
        var animation2 = Presto.Animate(target, new() { ["TranslateX"] = 200, Duration = 50, Delay = 10 });

        var done = false;
        Presto.CreateTimer(new() { Duration = 100, OnComplete = _ => done = true });
        RunUntil(() => done);

        Assert.True(animation1.Paused);
        Assert.True(animation1._canceled);
        Assert.Null(animation1._headTween!._prevRep);
        Assert.True(animation2.Paused);
        Assert.True(animation2._canceled);
        Assert.Null(animation2._headTween!._nextRep);
    }

    [Theory]
    [InlineData(TimeUnit.Milliseconds, 1)]
    [InlineData(TimeUnit.Seconds, .001)]
    public void ReAddTweenSiblingsOnRestartAfterAnimationCompletes(TimeUnit unit, double scale)
    {
        Presto.Engine.TimeUnit = unit;
        var target = new StyleTarget();
        var animation1 = Presto.Animate(target, new() { ["TranslateX"] = 200, Duration = 50 * scale });
        var animation2 = Presto.Animate(target, new() { ["TranslateX"] = 200, Duration = 50 * scale, Delay = 10 * scale });

        var done = false;
        Presto.CreateTimer(new()
        {
            Duration = 100 * scale,
            OnComplete = _ =>
            {
                Assert.True(animation1._canceled);
                Assert.True(animation2._canceled);
                animation1.Restart();
                animation2.Restart();
                Presto.CreateTimer(new()
                {
                    Duration = 20 * scale,
                    OnComplete = _ =>
                    {
                        animation1.Pause();
                        animation2.Pause();
                        done = true;
                    },
                });
            },
        });
        RunUntil(() => done);

        Assert.True(animation1.Paused);
        Assert.False(animation1._canceled);
        Assert.NotNull(animation1._headTween!._prevRep);
        Assert.True(animation2.Paused);
        Assert.False(animation2._canceled);
        Assert.NotNull(animation2._headTween!._nextRep);
    }

    [Fact]
    public void ProperlyOverrideLoopedTween()
    {
        var target = new StyleTarget();
        var animation1 = Presto.Animate(target, new()
        {
            ["Scale"] = 2,
            Alternate = true,
            Loop = true,
            Duration = 100,
            Composition = Composition.Replace,
        });

        var done = false;
        Presto.Animate(target, new() { ["Scale"] = 4, Duration = 40, Composition = Composition.Replace, OnComplete = _ => done = true });
        RunUntil(() => done);

        Assert.False(animation1.Completed);
        Assert.True(animation1._canceled);
    }

    [Fact]
    public void ProperlyOverrideTimelineTweens()
    {
        var target = new StyleTarget();
        var tl1 = Presto.CreateTimeline()
            .Add(target, new() { ["X"] = 200, Duration = 60 }, 0)
            .Add(target, new() { ["Y"] = 200, Duration = 60 }, 0);

        var done = false;
        Presto.CreateTimeline(new() { Delay = 10 })
            .Add(target, new() { ["X"] = 100, Duration = 30 }, 0)
            .Add(target, new() { ["Y"] = 100, Duration = 30 }, 0)
            .Then(_ => done = true);
        RunUntil(() => done);

        Assert.True(tl1._canceled);
    }

    [Fact]
    public void DoNotPauseAnimationWithPartiallyOverriddenTweens()
    {
        var target = new StyleTarget();
        var animation1 = Presto.Animate(target, new()
        {
            ["X"] = 100,
            ["Scale"] = 2,
            Alternate = true,
            Loop = true,
            Duration = 80,
            Composition = Composition.Replace,
        });

        var done = false;
        Presto.Animate(target, new() { ["Scale"] = 4, Duration = 40, Composition = Composition.Replace, OnComplete = _ => done = true });
        RunUntil(() => done);

        Assert.False(animation1.Completed);
        Assert.False(animation1._canceled);
        animation1.Pause();
    }

    [Fact]
    public void DoNotPauseTimelineWithPartiallyOverriddenTweens()
    {
        var target = new StyleTarget();
        var tl1 = Presto.CreateTimeline()
            .Add(target, new() { ["X"] = 200, Duration = 60 }, 0)
            .Add(target, new() { ["Y"] = 200, Duration = 60 }, 0);

        var done = false;
        Presto.CreateTimeline(new() { Delay = 10 })
            .Add(target, new() { ["X"] = 100, Duration = 30 }, 0)
            .Then(_ => done = true);
        RunUntil(() => done);

        Assert.False(tl1._canceled);
        tl1.Pause();
    }

    [Fact]
    public void DoNotOverrideTweensWithCompositionOverlap()
    {
        var target = new StyleTarget();
        var shortDone = false;
        var longDone = false;
        var animation1 = Presto.Animate(target, new()
        {
            ["X"] = 100,
            ["Scale"] = 2,
            Duration = 80,
            Composition = Composition.Overlap,
            OnComplete = _ => longDone = true,
        });

        Presto.Animate(target, new()
        {
            ["Scale"] = 4,
            Duration = 40,
            Composition = Composition.Overlap,
            OnComplete = _ =>
            {
                Assert.False(animation1.Completed);
                Assert.False(animation1._canceled);
                Assert.Equal(4, target.Scale);
                shortDone = true;
            },
        });

        RunUntil(() => shortDone && longDone);

        Assert.Equal(100, target.X);
        Assert.Equal(2, target.Scale);
    }

    [Fact]
    public void ProperlySumTweensWithCompositionAdditive()
    {
        var target = new StyleTarget();
        var shortDone = false;
        var longDone = false;
        var animation1 = Presto.Animate(target, new()
        {
            ["X"] = 100,
            ["Scale"] = 2,
            Duration = 200,
            Composition = Composition.Additive,
            Ease = "linear",
            OnComplete = _ => longDone = true,
        });

        Presto.Animate(target, new()
        {
            ["X"] = 200,
            ["Scale"] = 4,
            Duration = 100,
            Composition = Composition.Additive,
            Ease = "linear",
            OnComplete = _ =>
            {
                Assert.False(animation1.Completed);
                Assert.False(animation1._canceled);
                Assert.InRange(target.X, 120, 150);
                Assert.InRange(target.Scale, 3, 3.5);
                shortDone = true;
            },
        });

        RunUntil(() => shortDone && longDone);

        Assert.True(target.X > 180, $"Expected the summed x above 180, got {target.X}.");
        Assert.True(target.Scale > 3.9, $"Expected the summed scale above 3.9, got {target.Scale}.");
    }

    [Fact]
    public void ProperlyAssignSpecificTweenProperties()
    {
        Ease easeA = t => t;
        Ease easeB = t => t * 2;
        Func<double, double> modifierA = v => v;
        Func<double, double> modifierB = v => v * 2;

        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["X"] = new TweenParams { To = 100, Modifier = modifierA, Composition = Composition.Additive, Ease = easeA },
            ["Y"] = 100,
            Composition = Composition.Overlap,
            Ease = easeB,
            Modifier = modifierB,
            Autoplay = false,
        });

        var tweenX = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Same(modifierA, tweenX._modifier);
        Assert.Same(easeA, tweenX._ease);
        Assert.Equal(Composition.Additive, tweenX._composition);

        var tweenY = TestHelpers.GetTweenAtIndex(animation, 1)!;
        Assert.Same(modifierB, tweenY._modifier);
        Assert.Same(easeB, tweenY._ease);
        Assert.Equal(Composition.Overlap, tweenY._composition);
    }
}
