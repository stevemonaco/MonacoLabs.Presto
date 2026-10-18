using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

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

    [Fact]
    public void ReadingAPropertyThatWasNeverAssignedThrows() =>
        Assert.Throws<KeyNotFoundException>(() => new AnimationParams { ["X"] = 1 }["Y"]);

    [Fact]
    public void TheIdIsTheParameterOrElseTheNextAnimationNumber()
    {
        var named = Presto.Animate(new StyleTarget(), new() { ["X"] = 1, Id = "named" });
        var first = Presto.Animate(new StyleTarget(), new() { ["X"] = 1 });
        Presto.CreateTimer(new() { Duration = 100 });
        var second = Presto.Animate(new StyleTarget(), new() { ["X"] = 1 });

        Assert.Equal("named", named.Id);
        Assert.Equal(int.Parse(first.Id) + 1, int.Parse(second.Id));
    }

    [Fact]
    public void AThousandTargetsOverlapUnlessACompositionIsSet()
    {
        StyleTarget[] Targets(int count) => Enumerable.Range(0, count).Select(_ => new StyleTarget()).ToArray();

        var many = Presto.Animate(Targets(1000), new() { ["X"] = 100, Autoplay = false });
        var fewer = Presto.Animate(Targets(999), new() { ["X"] = 100, Autoplay = false });
        var named = Presto.Animate(Targets(1000), new() { ["X"] = 100, Composition = Composition.Replace, Autoplay = false });

        Assert.Equal(Composition.Overlap, TestHelpers.GetTweenAtIndex(many, 0)!.Composition);
        Assert.Equal(Composition.Replace, TestHelpers.GetTweenAtIndex(fewer, 0)!.Composition);
        Assert.Equal(Composition.Replace, TestHelpers.GetTweenAtIndex(named, 0)!.Composition);
    }

    [Fact]
    public void ANonNumericDurationOrDelayReadsAsZero()
    {
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["X"] = 100,
            Duration = new FunctionValue((_, _, _) => "abc"),
            Delay = new FunctionValue((_, _, _) => "abc"),
            Autoplay = false,
        });

        Assert.Equal(Constants.MinValue, TestHelpers.GetTweenAtIndex(animation, 0)!.Duration);
        Assert.Equal(0, animation.Delay);
    }

    [Fact]
    public void TheSimplerEndIsPromotedToTheOthersKind()
    {
        var complex = Presto.Animate(new TestObject(), new() { ["MultiplePlainValues"] = 10, Autoplay = false });
        var unit = Presto.Animate(new StyleTarget { TranslateX = "10" }, new() { ["TranslateX"] = "50px", Autoplay = false });
        var color = Presto.Animate(new StyleTarget { BackgroundColor = "0" }, new() { ["BackgroundColor"] = "rgb(255,0,0)", Autoplay = false });

        var complexTween = TestHelpers.GetTweenAtIndex(complex, 0)!;
        Assert.Equal(ValueKind.Complex, complexTween.ValueKind);
        Assert.Equal<double[]>([10, 10, 10, 10], complexTween.GetTo().Channels);
        Assert.Equal("10 10 10 10", complexTween.GetTo().Text);
        var unitTween = TestHelpers.GetTweenAtIndex(unit, 0)!;
        Assert.Equal(ValueKind.Unit, unitTween.ValueKind);
        Assert.Equal("10px", unitTween.GetFrom().Text);
        var colorTween = TestHelpers.GetTweenAtIndex(color, 0)!;
        Assert.Equal(ValueKind.Color, colorTween.ValueKind);
        Assert.Equal<double[]>([0, 0, 0, 0], colorTween.GetFrom().Channels);
    }

    [Fact]
    public void TheShorterComplexEndIsPaddedWithZerosAndTakesTheLongersStrings()
    {
        var shorterFrom = Presto.Animate(new TestObject { MultiplePlainValues = "16 32" }, new() { ["MultiplePlainValues"] = "20, 40, 60, 80", Autoplay = false });
        var shorterTo = Presto.Animate(new TestObject(), new() { ["MultiplePlainValues"] = "20, 40", Autoplay = false });

        var fromTween = TestHelpers.GetTweenAtIndex(shorterFrom, 0)!;
        Assert.Equal<double[]>([16, 32, 0, 0], fromTween.GetFrom().Channels);
        Assert.Equal("16, 32, 0, 0", fromTween.GetFrom().Text);
        var toTween = TestHelpers.GetTweenAtIndex(shorterTo, 0)!;
        Assert.Equal<double[]>([20, 40, 0, 0], toTween.GetTo().Channels);
        Assert.Equal("20 40 0 0", toTween.GetTo().Text);
    }

    [Fact]
    public void NumbersAreWrittenUnroundedAndUnitAndComplexValuesToFourDecimals()
    {
        var style = new StyleTarget();
        var complex = new TestObject { MultiplePlainValues = "0 0" };
        Presto.Animate(style, new() { ["X"] = 1, ["TranslateX"] = "1px", Duration = 3, Ease = Eases.None, Autoplay = false }).Seek(1);
        Presto.Animate(complex, new() { ["MultiplePlainValues"] = "1 1", Duration = 3, Ease = Eases.None, Autoplay = false }).Seek(1);

        Assert.Equal(1d / 3, style.X);
        Assert.Equal("0.3333px", style.TranslateX);
        Assert.Equal("0.3333 0.3333", complex.MultiplePlainValues);
    }

    [Fact]
    public void RefreshingAZeroDurationAnimationWritesTheRefreshedValues()
    {
        var target = new StyleTarget();
        var value = 10;
        var animation = Presto.Animate(target, new() { ["X"] = new FunctionValue((_, _, _) => value), Duration = 0 });
        Assert.Equal(10, target.X);

        value = 20;
        animation.Refresh();

        Assert.Equal(20, target.X);
    }
}
