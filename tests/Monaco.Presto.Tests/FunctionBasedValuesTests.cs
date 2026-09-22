using Monaco.Presto.Core;
using Monaco.Presto.Values;

namespace Monaco.Presto.Tests;

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
}
