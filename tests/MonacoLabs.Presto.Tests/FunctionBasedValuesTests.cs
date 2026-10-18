using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

/// <summary>Port of anime.js tests/suites/function-based-values.test.js.</summary>
public sealed class FunctionBasedValuesTests : EngineTestBase
{
    private static IndexedTarget[] CreateTargets() => [new() { Index = 0 }, new() { Index = 1 }, new() { Index = 2 }, new() { Index = 3 }];

    private static int IndexOf(object target) => ((IndexedTarget)target).Index;

    [Fact]
    public void BasicFunctionBasedValues()
    {
        var targets = CreateTargets();
        var animation = Presto.Animate(targets, new()
        {
            ["TranslateX"] = new FunctionValue((target, _, _) => IndexOf(target).ToString()),
            Duration = new FunctionValue((target, i, total) => total + ((i + IndexOf(target)) * 100)),
            Delay = new FunctionValue((target, i, total) => total + ((i + IndexOf(target)) * 100)),
            Ease = new FunctionValue((_, _, _) => (Ease)(t => t)),
            Autoplay = false,
        });

        double[] expectedDurations = [4, 204, 404, 604];
        double[] expectedDelays = [0, 200, 400, 600];
        for (var i = 0; i < 4; i++)
        {
            var tween = TestHelpers.GetTweenAtIndex(animation, i)!;
            Assert.Equal(ValueKind.Unit, tween._valueType);
            Assert.Equal(0, tween._fromNumber);
            Assert.Equal(i, tween._toNumber);
            Assert.Equal("px", tween._unit);
            Assert.Equal(expectedDurations[i], tween._changeDuration);
            Assert.Equal(expectedDelays[i], tween._delay);
            Assert.Equal("0px", targets[i].TranslateX);
        }

        animation.Seek(animation.Duration);

        string[] expectedEndValues = ["0px", "1px", "2px", "3px"];
        for (var i = 0; i < 4; i++) Assert.Equal(expectedEndValues[i], targets[i].TranslateX);
    }

    [Fact]
    public void FunctionBasedKeyframesValues()
    {
        var stagger = Presto.Stagger(100);
        var animation = Presto.Animate(CreateTargets(), new()
        {
            ["TranslateX"] = new TweenParams[]
            {
                new() { To = new FunctionValue((target, _, _) => IndexOf(target) * 100), Duration = stagger, Delay = stagger, Ease = new FunctionValue((_, _, _) => (Ease)(t => t)) },
                new() { To = new FunctionValue((target, _, _) => IndexOf(target) * 50), Duration = stagger, Delay = stagger, Ease = new FunctionValue((_, _, _) => (Ease)(t => t)) },
            },
            Autoplay = false,
        });

        double[] expectedDurations = [Constants.MinValue, 100, 200, 300];
        for (var i = 0; i < 4; i++)
        {
            var first = TestHelpers.GetTweenAtIndex(animation, i * 2)!;
            Assert.Equal(i * 100, first._toNumber);
            Assert.Equal(expectedDurations[i], first._changeDuration);
            Assert.Equal(i * 100, first._delay);

            var second = TestHelpers.GetTweenAtIndex(animation, (i * 2) + 1)!;
            Assert.Equal(i * 50, second._toNumber);
            Assert.Equal(expectedDurations[i], second._changeDuration);
            Assert.Equal(i * 100, second._delay);
        }
    }

    [Fact]
    public void FunctionBasedStringValuesAreConvertedToNumbers()
    {
        var animation = Presto.Animate(CreateTargets(), new()
        {
            ["TranslateX"] = 10,
            Delay = new FunctionValue((target, _, _) => IndexOf(target).ToString()),
            Autoplay = false,
        });

        for (var i = 0; i < 4; i++) Assert.Equal(i, TestHelpers.GetTweenAtIndex(animation, i)!._delay);
    }

    [Fact]
    public void FunctionBasedValuesReturningFromToArrayValues()
    {
        var animation = Presto.Animate(CreateTargets(), new()
        {
            ["TranslateX"] = new FunctionValue((target, i, total) => TweenValue.FromTo(IndexOf(target).ToString(), (total - 1) - i)),
            Autoplay = false,
        });

        for (var i = 0; i < 4; i++)
        {
            var tween = TestHelpers.GetTweenAtIndex(animation, i)!;
            Assert.Equal(i, tween._fromNumber);
            Assert.Equal(3 - i, tween._toNumber);
        }
    }

    [Fact]
    public void FunctionBasedValuesInsideFromToArrayValues()
    {
        var animation = Presto.Animate(CreateTargets(), new()
        {
            ["TranslateX"] = new TweenValue[]
            {
                new FunctionValue((target, _, _) => IndexOf(target).ToString()),
                new FunctionValue((_, i, total) => (total - 1) - i),
            },
            Autoplay = false,
        });

        for (var i = 0; i < 4; i++)
        {
            var tween = TestHelpers.GetTweenAtIndex(animation, i)!;
            Assert.Equal(i, tween._fromNumber);
            Assert.Equal(3 - i, tween._toNumber);
        }
    }

    [Fact]
    public void ChainedFunctionValuesInKeyframesReadThePreviousKeyframeEndValue()
    {
        var animation = Presto.Animate(new TestObject(), new()
        {
            ["PlainValue"] = new TweenParams[]
            {
                new() { To = 50 },
                new() { To = new ChainedFunctionValue((_, _, _, previous) => (double)previous!._value! * 2) },
            },
            Duration = 100,
            Autoplay = false,
        });

        Assert.Equal(50, TestHelpers.GetTweenAtIndex(animation, 0)!._toNumber);
        Assert.Equal(100, TestHelpers.GetTweenAtIndex(animation, 1)!._toNumber);
    }

    [Fact]
    public void ChainedFunctionValuesReceiveTheIndexAndTotalLikeFunctionValues()
    {
        var chained = new List<(object, int, int)>();
        var plain = new List<(object, int, int)>();
        var targets = CreateTargets();
        Presto.Animate(targets, new()
        {
            ["TranslateX"] = new ChainedFunctionValue((target, i, total, _) =>
            {
                chained.Add((target, i, total));
                return i * 10 + total;
            }),
            Duration = 100,
            Autoplay = false,
        });
        Presto.Animate(targets, new()
        {
            ["TranslateX"] = new FunctionValue((target, i, total) =>
            {
                plain.Add((target, i, total));
                return 0;
            }),
            Duration = 100,
            Autoplay = false,
        });

        Assert.Equal([(targets[0], 0, 4), (targets[1], 1, 4), (targets[2], 2, 4), (targets[3], 3, 4)], chained);
        Assert.Equal(plain, chained);
    }

    [Fact]
    public void ChainedFunctionValuesOnTimelineChildrenReadThePreviousChildEndValue()
    {
        var target = new TestObject();
        var timeline = Presto.CreateTimeline(new() { Autoplay = false });
        timeline.Add(target, new() { ["PlainValue"] = 50, Duration = 100 });
        timeline.Add(target, new()
        {
            ["PlainValue"] = new ChainedFunctionValue((_, _, _, previous) => (double)previous!._value! * 2),
            Duration = 100,
        });

        var child = TestHelpers.GetChildAtIndex(timeline, 1)!;
        Assert.Equal(100, TestHelpers.GetTweenAtIndex(child, 0)!._toNumber);
    }

    [Fact]
    public void TextModifiersWriteStringsAndSettleOnTheValueAtOne()
    {
        const string text = "presto";
        var target = new TextTarget();
        var written = new List<string>();
        var animation = Presto.Animate(target, new()
        {
            ["Text"] = new TweenParams { From = 0, To = 1, TextModifier = v => text[..(int)(v * text.Length)] },
            Duration = 100,
            Ease = Eases.None,
            OnUpdate = _ => written.Add(target.Text),
        });

        RunUntil(() => animation.Completed);

        Assert.NotEmpty(written);
        Assert.All(written, value => Assert.Equal(text[..value.Length], value));
        Assert.Equal(text, target.Text);
    }

    [Fact]
    public void RefreshConvertsTheFromValueToARefreshedEndsUnit()
    {
        var target = new StyleTarget { Rotate = "180deg" };
        var to = "90deg";
        var animation = Presto.Animate(target, new() { ["Rotate"] = new FunctionValue((_, _, _) => to), Duration = 100, Ease = "linear", Autoplay = false });

        to = "1turn";
        animation.Refresh().Seek(50);

        Assert.Equal("0.75turn", target.Rotate);
    }

    [Fact]
    public void RefreshPadsAFromValueShorterThanARefreshedEnd()
    {
        var target = new TestObject();
        var to = "0 0";
        var animation = Presto.Animate(target, new() { ["MultiplePlainValues"] = new FunctionValue((_, _, _) => to), Duration = 100, Ease = "linear", Autoplay = false });

        to = "32 64 128 256 512";
        animation.Refresh().Seek(50);

        Assert.Equal("24 48 96 192 256", target.MultiplePlainValues);
    }

    [Fact]
    public void AFunctionReturningTweenParamsGivesEachTargetItsOwnModifiers()
    {
        TextTarget[] counters = [new(), new()];
        int[] decimals = [0, 2];
        var animation = Presto.Animate(counters, new()
        {
            ["Text"] = new FunctionValue((_, i, _) => new TweenParams
            {
                From = 0,
                To = 10,
                TextModifier = v => Utils.RoundPad(v, decimals[i]),
            }),
            Duration = 100,
            Ease = Eases.None,
            Autoplay = false,
        });

        animation.Seek(50);

        Assert.Equal("5", counters[0].Text);
        Assert.Equal("5.00", counters[1].Text);
    }

    [Fact]
    public void AFunctionReturningTweenParamsGivesEachTargetItsOwnTween()
    {
        var targets = CreateTargets();
        var animation = Presto.Animate(targets, new()
        {
            ["TranslateX"] = new FunctionValue((target, _, _) => new TweenParams { To = IndexOf(target) * 100, Duration = (IndexOf(target) + 1) * 100, Ease = "linear" }),
            Autoplay = false,
        });

        for (var i = 0; i < 4; i++)
        {
            var tween = TestHelpers.GetTweenAtIndex(animation, i)!;
            Assert.Equal(i * 100, tween._toNumber);
            Assert.Equal((i + 1) * 100, tween._changeDuration);
        }

        animation.Seek(200);

        Assert.Equal(["0px", "100px", "133.3333px", "150px"], targets.Select(t => t.TranslateX));
    }
}
