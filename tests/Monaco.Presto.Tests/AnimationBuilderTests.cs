using System.Reflection;

namespace Monaco.Presto.Tests;

/// <summary>The fluent <see cref="AnimationBuilder"/> against the <see cref="AnimationParams"/> indexer spelling it stands in for.</summary>
public sealed class AnimationBuilderTests : EngineTestBase
{
    [Fact]
    public void BareValuesMatchTheIndexer()
    {
        var stagger = Presto.Stagger(10);
        FunctionValue function = (_, i, _) => i * 5;

        AssertEquivalent(
            new() { ["X"] = 100, ["Y"] = "+=50", ["Opacity"] = stagger, ["Scale"] = function, ["Rotate"] = new[] { 0, 90 } },
            Presto.Build()
                .Property("X", 100)
                .Property("Y", "+=50")
                .Property("Opacity", stagger)
                .Property("Scale", function)
                .Property("Rotate", new[] { 0, 90 })
                .ToParams());
    }

    [Fact]
    public void PerPropertySettingsMatchTheIndexer()
    {
        Func<double, double> modifier = Math.Round;
        Func<double, string> textModifier = v => v.ToString();

        AssertEquivalent(
            new()
            {
                ["X"] = new TweenParams { From = -20, To = 0 },
                ["Y"] = new TweenParams { To = 50, Duration = 200, Delay = 100, Ease = "outQuad" },
                ["Opacity"] = new TweenParams { From = 0 },
                ["Scale"] = new TweenParams { To = 2, Composition = Composition.Blend, Modifier = modifier, TextModifier = textModifier },
            },
            Presto.Build()
                .Property("X", from: -20, to: 0)
                .Property("Y", 50, duration: 200, delay: 100, ease: "outQuad")
                .Property("Opacity", from: 0)
                .Property("Scale", 2, composition: Composition.Blend, modifier: modifier, textModifier: textModifier)
                .ToParams());
    }

    [Fact]
    public void FunctionValuesBindWithoutACast()
    {
        var parameters = Presto.Build().Property("X", (_, i, total) => i * total).ToParams();

        var function = Assert.IsType<FunctionValue>(parameters["X"].Raw);
        Assert.Equal(6d, function(new object(), 2, 3).Raw);
    }

    [Fact]
    public void PerPropertyKeyframesMatchTheIndexer()
    {
        AssertEquivalent(
            new()
            {
                ["X"] = TweenValue.Keyframes(
                    new TweenParams { To = 10 },
                    new TweenParams { To = 20, Duration = 100 },
                    new TweenParams { To = 30, Delay = 50, Ease = "inQuad" }),
                ["Y"] = TweenValue.Keyframes(new TweenParams { To = 1 }, new TweenParams { To = 2 }, new TweenParams { To = 3 }),
            },
            Presto.Build()
                .Property("X", k => k.To(10).To(20, duration: 100).To(30, delay: 50, ease: "inQuad"))
                .Property("Y", k => k.To(1).To(2).To(3))
                .ToParams());
    }

    [Fact]
    public void BareKeyframesStartFromTheCurrentValueRatherThanTheFirstFrame()
    {
        var target = new StyleTarget();
        var animation = Presto.Build()
            .Property("X", k => k.To(100).To(0))
            .Duration(200)
            .Ease("linear")
            .Animate(target);

        animation.Seek(50);
        Assert.Equal(50, target.X, 3);
        animation.Seek(100);
        Assert.Equal(100, target.X, 3);
        animation.Seek(200);
        Assert.Equal(0, target.X, 3);
    }

    [Fact]
    public void AnInertiaPassesAsAPropertyValue()
    {
        var target = new StyleTarget();
        var animation = Presto.Build().Property("X", new Inertia(1)).Animate(target);

        animation.Complete();

        Assert.Equal(350, target.X, 9);
    }

    [Fact]
    public void EveryTimerSettingAndCallbackMatchesTheIndexer()
    {
        Func<double, double> modifier = Math.Floor;
        Action<Timer> onBegin = _ => { }, onBeforeUpdate = _ => { }, onUpdate = _ => { }, onLoop = _ => { };
        Action<Timer> onPause = _ => { }, onComplete = _ => { }, onRender = _ => { };
        var delay = Presto.Stagger(50);

        AssertEquivalent(
            new()
            {
                ["X"] = 1,
                Duration = 400, Delay = delay, Ease = "outExpo", PlaybackEase = Eases.InQuad, Composition = Composition.None,
                Modifier = modifier, Id = "builder", Loop = 3, LoopDelay = 20, Alternate = true, Reversed = true,
                Autoplay = false, FrameRate = 30, PlaybackRate = 2, Priority = 5,
                OnBegin = onBegin, OnBeforeUpdate = onBeforeUpdate, OnUpdate = onUpdate, OnLoop = onLoop,
                OnPause = onPause, OnComplete = onComplete, OnRender = onRender,
            },
            Presto.Build()
                .Property("X", 1)
                .Duration(400).Delay(delay).Ease("outExpo").PlaybackEase(Eases.InQuad).Composition(Composition.None)
                .Modifier(modifier).Id("builder").Loop(3).LoopDelay(20).Alternate().Reversed()
                .Autoplay(false).FrameRate(30).PlaybackRate(2).Priority(5)
                .OnBegin(onBegin).OnBeforeUpdate(onBeforeUpdate).OnUpdate(onUpdate).OnLoop(onLoop)
                .OnPause(onPause).OnComplete(onComplete).OnRender(onRender)
                .ToParams());
    }

    [Fact]
    public void AnEmptyBuilderMatchesEmptyParams() => AssertEquivalent(new(), Presto.Build().ToParams());

    [Fact]
    public void BuilderAndIndexerAnimationsReadTheSameValuesAcrossLoopsAndAlternation()
    {
        var byIndexer = new StyleTarget();
        var byBuilder = new StyleTarget();
        Presto.Animate(byIndexer, new()
        {
            ["X"] = new TweenParams { From = -20, To = 100, Ease = "outQuad" },
            ["Y"] = new TweenParams[] { new() { To = 50, Duration = 200 }, new() { To = 0 } },
            ["Scale"] = 3,
            Duration = 400,
            Delay = 50,
            Loop = 2,
            Alternate = true,
            Ease = "inOutSine",
        });
        Presto.Build()
            .Property("X", from: -20, to: 100, ease: "outQuad")
            .Property("Y", k => k.To(50, duration: 200).To(0))
            .Property("Scale", 3)
            .Duration(400)
            .Delay(50)
            .Loop(2)
            .Alternate()
            .Ease("inOutSine")
            .Animate(byBuilder);

        for (var i = 0; i < 12; i++)
        {
            Frames.Advance(130);
            Assert.Equal(byIndexer.X, byBuilder.X);
            Assert.Equal(byIndexer.Y, byBuilder.Y);
            Assert.Equal(byIndexer.Scale, byBuilder.Scale);
        }
        Assert.NotEqual(0, byBuilder.X);
    }

    [Fact]
    public void PropertyWithOnlyToIsBareAndAnyOtherArgumentMakesTweenParams()
    {
        var parameters = Presto.Build()
            .Property("Bare", 1)
            .Property("NamedTo", to: 2)
            .Property("Empty")
            .Property("From", 3, from: 0)
            .Property("Duration", 3, duration: 10)
            .Property("Delay", 3, delay: 10)
            .Property("Ease", 3, ease: "linear")
            .Property("Composition", 3, composition: Composition.Replace)
            .Property("Modifier", 3, modifier: Math.Round)
            .Property("TextModifier", 3, textModifier: v => v.ToString())
            .ToParams();

        Assert.Equal(1d, parameters["Bare"].Raw);
        Assert.Equal(2d, parameters["NamedTo"].Raw);
        Assert.IsType<TweenParams>(parameters["Empty"].Raw);
        foreach (var name in new[] { "From", "Duration", "Delay", "Ease", "Composition", "Modifier", "TextModifier" })
        {
            Assert.Equal(3d, Assert.IsType<TweenParams>(parameters[name].Raw).To?.Raw);
        }
    }

    [Fact]
    public void DuplicatePropertyKeepsItsFirstPositionWithTheLastValue()
    {
        var parameters = Presto.Build()
            .Property("X", 1)
            .Property("Y", 2)
            .Property("X", 3, duration: 100)
            .ToParams();

        Assert.Equal(["X", "Y"], parameters.Properties.Keys);
        Assert.Equal(3d, Assert.IsType<TweenParams>(parameters["X"].Raw).To?.Raw);
    }

    [Fact]
    public void DuplicateSettingsAndCallbacksKeepTheLastValue()
    {
        var calls = new List<string>();
        var target = new StyleTarget();

        Presto.Build()
            .Property("X", 10)
            .Duration(1000).Duration(100)
            .Loop(5).Loop(false)
            .OnComplete(_ => calls.Add("first")).OnComplete(_ => calls.Add("second"))
            .Animate(target);
        RunUntil(() => calls.Count > 0, max: 500);

        Assert.Equal(["second"], calls);
        Assert.Equal(10, target.X);
    }

    [Fact]
    public void ToParamsReturnsIndependentSnapshots()
    {
        var builder = Presto.Build().Property("X", 1).Duration(100);

        var first = builder.ToParams();
        var second = builder.ToParams();
        builder.Property("X", 2).Property("Y", 3).Duration(200).Loop();

        Assert.NotSame(first.Properties, second.Properties);
        foreach (var parameters in new[] { first, second })
        {
            Assert.Equal(["X"], parameters.Properties.Keys);
            Assert.Equal(1d, parameters["X"].Raw);
            Assert.Equal(100d, parameters.Duration?.Raw);
            Assert.Null(parameters.Loop);
        }
    }

    [Fact]
    public void SetWritesImmediately()
    {
        var target = new StyleTarget();

        Presto.Build().Property("X", 42).Set(target);

        Assert.Equal(42, target.X);
    }

    [Fact]
    public void LoopOverloadsMatchLoopCountConversions()
    {
        Assert.True(Presto.Build().Loop().ToParams().Loop?.IsInfinite);
        Assert.Equal(3, Presto.Build().Loop(3).ToParams().Loop?.Value);
        Assert.Equal(0, Presto.Build().Loop(false).ToParams().Loop?.Value);
    }

    [Fact]
    public void DurationBasedKeyframesMatchTheIndexer()
    {
        Func<double, double> modifier = Math.Round;

        AssertEquivalent(
            new()
            {
                Keyframes = new Keyframe[]
                {
                    new() { ["X"] = 100, ["Y"] = 50, Duration = 200, Ease = "outQuad" },
                    new() { ["X"] = 0, Delay = 50, Composition = Composition.Blend, Modifier = modifier },
                },
                ["Scale"] = 2,
                Duration = 600,
            },
            Presto.Build()
                .Keyframes(k => k
                    .Frame(f => f.Property("X", 100).Duration(200).Property("Y", 50).Ease("outQuad"))
                    .Frame(f => f.Property("X", 0).Delay(50).Composition(Composition.Blend).Modifier(modifier)))
                .Property("Scale", 2)
                .Duration(600)
                .ToParams());
    }

    [Fact]
    public void PercentageBasedKeyframesMatchTheIndexerAndTheLastAtAndKeyframesCallWin()
    {
        AssertEquivalent(
            new()
            {
                Keyframes = new Dictionary<string, Keyframe>
                {
                    ["0%"] = new() { ["X"] = 0 },
                    ["21.5%"] = new() { ["X"] = 40, Ease = "inQuad" },
                    ["100%"] = new() { ["X"] = 100, ["Y"] = 10 },
                },
            },
            Presto.Build()
                .Keyframes(k => k.Frame(f => f.Property("Z", 1)))
                .Keyframes(k => k
                    .At(0, f => f.Property("X", 0))
                    .At(100, f => f.Property("X", -1))
                    .At(21.5, f => f.Property("X", 40).Ease("inQuad"))
                    .At(100, f => f.Property("X", 100).Property("Y", 10)))
                .ToParams());
    }

    [Fact]
    public void ToParamsBuildsFreshKeyframesEachCall()
    {
        var builder = Presto.Build().Keyframes(k => k.Frame(f => f.Property("X", 1)));

        Assert.NotSame(builder.ToParams().Keyframes!.DurationBased![0], builder.ToParams().Keyframes!.DurationBased![0]);
    }

    [Fact]
    public void MixingFrameAndAtThrows()
    {
        Assert.Throws<InvalidOperationException>(() => Presto.Build().Keyframes(k => k.Frame(_ => { }).At(50, _ => { })));
        Assert.Throws<InvalidOperationException>(() => Presto.Build().Keyframes(k => k.At(50, _ => { }).Frame(_ => { })));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void AtOutsideZeroToHundredThrows(double percent) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Presto.Build().Keyframes(k => k.At(percent, _ => { })));

    [Fact]
    public void KeyframedBuilderAndIndexerAnimationsReadTheSameValues()
    {
        var byIndexer = new[] { new StyleTarget(), new StyleTarget() };
        var byBuilder = new[] { new StyleTarget(), new StyleTarget() };
        Presto.Animate(byIndexer[0], new()
        {
            Keyframes = new Keyframe[] { new() { ["X"] = 100, Duration = 300, Ease = "outQuad" }, new() { ["X"] = 20, ["Y"] = 60 } },
            Duration = 800,
        });
        Presto.Animate(byIndexer[1], new()
        {
            Keyframes = new Dictionary<string, Keyframe>
            {
                ["0%"] = new() { ["X"] = 10 },
                ["30%"] = new() { ["X"] = 80, Ease = "inOutSine" },
                ["100%"] = new() { ["X"] = 0, ["Y"] = 40 },
            },
            Duration = 800,
        });
        Presto.Build()
            .Keyframes(k => k.Frame(f => f.Property("X", 100).Duration(300).Ease("outQuad")).Frame(f => f.Property("X", 20).Property("Y", 60)))
            .Duration(800)
            .Animate(byBuilder[0]);
        Presto.Build()
            .Keyframes(k => k
                .At(0, f => f.Property("X", 10))
                .At(30, f => f.Property("X", 80).Ease("inOutSine"))
                .At(100, f => f.Property("X", 0).Property("Y", 40)))
            .Duration(800)
            .Animate(byBuilder[1]);

        for (var i = 0; i < 8; i++)
        {
            Frames.Advance(110);
            for (var t = 0; t < 2; t++)
            {
                Assert.Equal(byIndexer[t].X, byBuilder[t].X);
                Assert.Equal(byIndexer[t].Y, byBuilder[t].Y);
            }
        }
        Assert.NotEqual(0, byBuilder[0].X);
    }

    [Fact]
    public void TimelinesBuiltWithBuildersLayOutLikeTimelinesBuiltWithParams()
    {
        var target = new StyleTarget();
        var targets = new[] { new StyleTarget(), new StyleTarget(), new StyleTarget() };
        var byParams = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Add(target, new() { ["X"] = 100 })
            .Add(target, new() { ["Y"] = 50, Duration = 30, Delay = 5 }, "+=20")
            .Add(targets, new() { ["X"] = 10 }, Presto.Stagger(15))
            .Set(target, new() { ["Scale"] = 2 }, 200);
        var byBuilder = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Add(target, Presto.Build().Property("X", 100))
            .Add(target, Presto.Build().Property("Y", 50).Duration(30).Delay(5), "+=20")
            .Add(targets, Presto.Build().Property("X", 10), Presto.Stagger(15))
            .Set(target, Presto.Build().Property("Scale", 2), 200);

        Assert.Equal(TestHelpers.GetChildLength(byParams), TestHelpers.GetChildLength(byBuilder));
        for (var i = 0; i < TestHelpers.GetChildLength(byParams); i++)
        {
            Assert.Equal(TestHelpers.OffsetOf(byParams, i), TestHelpers.OffsetOf(byBuilder, i));
        }
        Assert.Equal(byParams.Duration, byBuilder.Duration);
    }

    [Fact]
    public void CloneIsIndependentOfItsSourceBothWays()
    {
        var source = Recipe();
        var clone = source.Clone();
        Mutate(source);
        AssertEquivalent(Recipe().ToParams(), clone.ToParams());

        source = Recipe();
        clone = source.Clone();
        Mutate(clone);
        AssertEquivalent(Recipe().ToParams(), source.ToParams());

        static AnimationBuilder Recipe() => Presto.Build()
            .Property("X", 100)
            .Property("Y", 50, duration: 200, ease: "outQuad")
            .Keyframes(k => k.Frame(f => f.Property("Scale", 2).Duration(100)))
            .Duration(400)
            .Delay(20);

        static void Mutate(AnimationBuilder builder) => builder
            .Duration(900).Loop()
            .Property("X", 1).Property("Z", 3)
            .Property("Y", 0, duration: 10)
            .Keyframes(k => k.At(50, f => f.Property("Scale", 4)));
    }

    [Fact]
    public void WithChangesOnlyTheCopyAndAnOverrideKeepsItsPosition()
    {
        var source = Presto.Build().Property("X", 1).Property("Y", 2).Duration(100);

        var changed = source.With(b => b.Property("X", 10).Property("Z", 3).Delay(50)).ToParams();

        var original = source.ToParams();
        Assert.Equal(["X", "Y"], original.Properties.Keys);
        Assert.Equal(1d, original["X"].Raw);
        Assert.Null(original.Delay);
        Assert.Equal(["X", "Y", "Z"], changed.Properties.Keys);
        Assert.Equal(10d, changed["X"].Raw);
        Assert.Equal(100d, changed.Duration?.Raw);
        Assert.Equal(50d, changed.Delay?.Raw);
    }

    [Fact]
    public void LambdaAnimateAndSetMatchTheBuilderForm()
    {
        var byBuilder = new StyleTarget();
        var byLambda = new StyleTarget();
        Presto.Build().Property("X", 100).Property("Y", 40, ease: "outQuad").Duration(300).Animate(byBuilder);
        Presto.Animate(byLambda, b => b.Property("X", 100).Property("Y", 40, ease: "outQuad").Duration(300));

        for (var i = 0; i < 4; i++)
        {
            Frames.Advance(90);
            Assert.Equal(byBuilder.X, byLambda.X);
            Assert.Equal(byBuilder.Y, byLambda.Y);
        }
        Assert.Equal(100, byLambda.X);

        Presto.Build().Property("Scale", 3).Set(byBuilder);
        Presto.Set(byLambda, b => b.Property("Scale", 3));
        Assert.Equal(3, byLambda.Scale);
        Assert.Equal(byBuilder.Scale, byLambda.Scale);
    }

    [Fact]
    public void TimelinesBuiltWithLambdasLayOutLikeTimelinesBuiltWithBuilders()
    {
        var target = new StyleTarget();
        var targets = new[] { new StyleTarget(), new StyleTarget(), new StyleTarget() };
        var byBuilder = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Add(target, Presto.Build().Property("Y", 50).Duration(30).Delay(5), "+=20")
            .Add(targets, Presto.Build().Property("X", 10), Presto.Stagger(15))
            .Set(target, Presto.Build().Property("Scale", 2), 200);
        var byLambda = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Add(target, b => b.Property("Y", 50).Duration(30).Delay(5), "+=20")
            .Add(targets, b => b.Property("X", 10), Presto.Stagger(15))
            .Set(target, b => b.Property("Scale", 2), 200);

        Assert.Equal(5, TestHelpers.GetChildLength(byLambda));
        Assert.Equal(TestHelpers.GetChildLength(byBuilder), TestHelpers.GetChildLength(byLambda));
        for (var i = 0; i < TestHelpers.GetChildLength(byBuilder); i++)
        {
            Assert.Equal(TestHelpers.OffsetOf(byBuilder, i), TestHelpers.OffsetOf(byLambda, i));
        }
        Assert.Equal(byBuilder.Duration, byLambda.Duration);
    }

    [Fact]
    public void TargetTypedParamsStillBindBesideTheLambdaOverloads()
    {
        var target = new StyleTarget();

        Presto.Set(target, new() { ["X"] = 7 });
        Presto.Animate(target, new() { ["Y"] = 1, Duration = 10 });
        Presto.CreateTimeline().Add(target, new() { ["Scale"] = 2 }).Set(target, new() { ["X"] = 3 });

        Assert.Equal(7, target.X);
    }

    [Fact]
    public void WhenReducedIsPickedOnlyWhileReducedMotionIsSet()
    {
        var builder = Presto.Build().Property("X", 100).WhenReduced(b => b.Property("Opacity", 1));

        Assert.Equal(["X"], builder.ToParams().Properties.Keys);
        Presto.Defaults.ReducedMotion = true;
        Assert.Equal(["Opacity"], builder.ToParams().Properties.Keys);
        Assert.Equal(["X"], Presto.Build().Property("X", 100).ToParams().Properties.Keys);
        Presto.Defaults.ReducedMotion = false;
        Assert.Equal(["X"], builder.ToParams().Properties.Keys);
    }

    [Fact]
    public void SettingsChainedAfterWhenReducedCarryIntoTheReducedFormButMainPropertiesDoNot()
    {
        TweenValue delay = Presto.Stagger(60);
        Action<Timer> onComplete = _ => { };
        Presto.Defaults.ReducedMotion = true;

        var parameters = Presto.Build()
            .Property("X", 100)
            .Keyframes(k => k.Frame(f => f.Property("Y", 1)))
            .Duration(800)
            .Ease("outExpo")
            .WhenReduced(b => b.Property("Opacity", 1).Duration(150))
            .Delay(delay)
            .OnComplete(onComplete)
            .ToParams();

        Assert.Equal(["Opacity"], parameters.Properties.Keys);
        Assert.Null(parameters.Keyframes);
        Assert.Equal(150d, parameters.Duration?.Raw);
        Assert.Equal("outExpo", parameters.Ease?.Raw);
        Assert.Same(delay.Raw, parameters.Delay?.Raw);
        Assert.Same(onComplete, parameters.OnComplete);
    }

    [Fact]
    public void CloneAndWithCarryTheReducedFormAndWithLeavesTheSourceAlone()
    {
        var source = Presto.Build().Property("X", 100).WhenReduced(b => b.Property("Opacity", 1));

        var clone = source.Clone();
        var changed = source.With(b => b.WhenReduced(r => r.Property("Scale", 1)));
        Presto.Defaults.ReducedMotion = true;

        Assert.Equal(["Opacity"], clone.ToParams().Properties.Keys);
        Assert.Equal(["Scale"], changed.ToParams().Properties.Keys);
        Assert.Equal(["Opacity"], source.ToParams().Properties.Keys);
    }

    private static void AssertEquivalent(AnimationParams expected, AnimationParams actual)
    {
        Assert.Equal(expected.Properties.Keys, actual.Properties.Keys);
        foreach (var (name, value) in expected.Properties) AssertSameValue(value, actual.Properties[name]);
        foreach (var property in typeof(AnimationParams).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            AssertSameValue(property.GetValue(expected), property.GetValue(actual));
        }
    }

    private static void AssertSameValue(object? expected, object? actual)
    {
        switch (expected)
        {
            case TweenValue value:
                AssertSameValue(value.Raw, Assert.IsType<TweenValue>(actual).Raw);
                break;
            case EaseValue ease:
                AssertSameValue(ease.Raw, Assert.IsType<EaseValue>(actual).Raw);
                break;
            case TweenValue[] frames:
                var actualFrames = Assert.IsType<TweenValue[]>(actual);
                Assert.Equal(frames.Length, actualFrames.Length);
                for (var i = 0; i < frames.Length; i++) AssertSameValue(frames[i], actualFrames[i]);
                break;
            case TweenParams tween:
                var actualTween = Assert.IsType<TweenParams>(actual);
                foreach (var property in typeof(TweenParams).GetProperties())
                {
                    AssertSameValue(property.GetValue(tween), property.GetValue(actualTween));
                }
                break;
            case Keyframes keyframes:
                var actualKeyframes = Assert.IsType<Keyframes>(actual);
                AssertSameValue(keyframes.DurationBased, actualKeyframes.DurationBased);
                AssertSameValue(keyframes.PercentageBased, actualKeyframes.PercentageBased);
                break;
            case Keyframe[] keyframeArray:
                var actualArray = Assert.IsType<Keyframe[]>(actual);
                Assert.Equal(keyframeArray.Length, actualArray.Length);
                for (var i = 0; i < keyframeArray.Length; i++) AssertSameValue(keyframeArray[i], actualArray[i]);
                break;
            case Dictionary<string, Keyframe> percentages:
                var actualPercentages = Assert.IsType<Dictionary<string, Keyframe>>(actual);
                Assert.Equal(percentages.Keys.Order(), actualPercentages.Keys.Order());
                foreach (var (key, frame) in percentages) AssertSameValue(frame, actualPercentages[key]);
                break;
            case Keyframe frame:
                var actualFrame = Assert.IsType<Keyframe>(actual);
                Assert.Equal(frame.Properties.Keys, actualFrame.Properties.Keys);
                foreach (var (name, value) in frame.Properties) AssertSameValue(value, actualFrame.Properties[name]);
                foreach (var property in typeof(Keyframe).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.GetIndexParameters().Length > 0) continue;
                    AssertSameValue(property.GetValue(frame), property.GetValue(actualFrame));
                }
                break;
            default:
                Assert.Equal(expected, actual);
                break;
        }
    }
}
