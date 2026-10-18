using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

/// <summary>Port of anime.js tests/suites/keyframes.test.js.</summary>
public sealed class KeyframesTests : EngineTestBase
{
    [Fact]
    public void AnArrayOfOneRawValueIsASimpleValue()
    {
        var animation = Presto.Animate(new StyleTarget(), new() { ["TranslateX"] = new double[] { 50 }, Autoplay = false });

        var tween = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Equal(ValueKind.Unit, tween._valueType);
        Assert.Equal(0, tween._fromNumber);
        Assert.Equal(50, tween._toNumber);
        Assert.Equal("px", tween._unit);
    }

    [Fact]
    public void AnArrayOfTwoRawValuesBecomesFromToValues()
    {
        var animation = Presto.Animate(new StyleTarget(), new() { ["TranslateX"] = new double[] { -100, 100 }, Autoplay = false });

        var tween = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Equal(ValueKind.Unit, tween._valueType);
        Assert.Equal(-100, tween._fromNumber);
        Assert.Equal(100, tween._toNumber);
        Assert.Equal("px", tween._unit);
    }

    [Fact]
    public void TheFirstOfMoreThanTwoRawValuesIsUsedAsAFromValue()
    {
        var animation = Presto.Animate(new StyleTarget(), new() { ["TranslateX"] = new double[] { -100, 100, 50 }, Autoplay = false });

        var first = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Equal(-100, first._fromNumber);
        Assert.Equal(100, first._toNumber);
        Assert.Equal("px", first._unit);

        var second = TestHelpers.GetTweenAtIndex(animation, 1)!;
        Assert.Equal(100, second._fromNumber);
        Assert.Equal(50, second._toNumber);
        Assert.Equal("px", second._unit);
    }

    [Fact]
    public void AnArrayOfTwoObjectValuesBecomesKeyframes()
    {
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["TranslateX"] = new TweenParams[] { new() { To = -100 }, new() { To = 100 } },
            Autoplay = false,
        });

        var first = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Equal(0, first._fromNumber);
        Assert.Equal(-100, first._toNumber);

        var second = TestHelpers.GetTweenAtIndex(animation, 1)!;
        Assert.Equal(-100, second._fromNumber);
        Assert.Equal(100, second._toNumber);
    }

    [Fact]
    public void UnspecifiedKeyframeDurationIsTheInstanceDurationDividedByTheKeyframeCount()
    {
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["TranslateX"] = new TweenParams[] { new() { To = -100 }, new() { To = 100 }, new() { To = 50 }, new() { To = 0 } },
            Duration = 2000,
            Autoplay = false,
        });

        for (var i = 0; i < 4; i++) Assert.Equal(500, TestHelpers.GetTweenAtIndex(animation, i)!._changeDuration);
    }

    [Fact]
    public void MixedUnspecifiedKeyframeDurationsKeepTheSpecifiedOnes()
    {
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["TranslateX"] = new TweenParams[]
            {
                new() { To = -100, Duration = 800 },
                new() { To = 100 },
                new() { To = 50 },
                new() { To = 0, Duration = 1200 },
            },
            Duration = 2000,
            Autoplay = false,
        });

        Assert.Equal(800, TestHelpers.GetTweenAtIndex(animation, 0)!._changeDuration);
        Assert.Equal(500, TestHelpers.GetTweenAtIndex(animation, 1)!._changeDuration);
        Assert.Equal(500, TestHelpers.GetTweenAtIndex(animation, 2)!._changeDuration);
        Assert.Equal(1200, TestHelpers.GetTweenAtIndex(animation, 3)!._changeDuration);
    }

    [Fact]
    public void ASingleKeyframeInheritsTheWholeInstanceDuration()
    {
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["TranslateX"] = new TweenParams[] { new() { To = -100 } },
            Duration = 2000,
            Autoplay = false,
        });

        Assert.Equal(2000, TestHelpers.GetTweenAtIndex(animation, 0)!._changeDuration);
    }

    [Fact]
    public void TheFirstKeyframeDelayIsTransferredToTheAnimationDelay()
    {
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["TranslateX"] = new TweenParams[] { new() { To = -100 }, new() { To = 100 } },
            Delay = 200,
            Autoplay = false,
        });

        Assert.Equal(200, animation._delay);
        Assert.Equal(0, TestHelpers.GetTweenAtIndex(animation, 0)!._delay);
        Assert.Equal(0, TestHelpers.GetTweenAtIndex(animation, 1)!._delay);
    }

    [Fact]
    public void GeneralKeyframesInheritTheInstanceParameters()
    {
        Func<double, double> keyframeModifier = v => v;
        Func<double, double> instanceModifier = v => v;

        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["TranslateX"] = new TweenParams[]
            {
                new() { To = -100 },
                new() { To = 100, Duration = 100, Delay = 300, Ease = "linear", Modifier = keyframeModifier },
                new() { To = 50 },
            },
            ["TranslateY"] = new TweenParams[] { new() { To = -200 }, new() { To = 200 }, new() { To = 100 } },
            Duration = 1500,
            Delay = 200,
            Modifier = instanceModifier,
            Ease = "outQuad",
            Autoplay = false,
        });

        var first = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Equal(500, first._changeDuration);
        Assert.Equal(0, first._delay);
        Assert.Equal(.75, first._ease(.5));
        Assert.Same(instanceModifier, first._modifier);

        var second = TestHelpers.GetTweenAtIndex(animation, 1)!;
        Assert.Equal(100, second._changeDuration);
        Assert.Equal(300, second._delay);
        Assert.Equal(.5, second._ease(.5));
        Assert.Same(keyframeModifier, second._modifier);

        double[] expectedFrom = [0, -100, 100, 0, -200, 200];
        double[] expectedTo = [-100, 100, 50, -200, 200, 100];
        for (var i = 0; i < expectedTo.Length; i++)
        {
            var tween = TestHelpers.GetTweenAtIndex(animation, i)!;
            Assert.Equal(expectedFrom[i], tween._fromNumber);
            Assert.Equal(expectedTo[i], tween._toNumber);
        }
    }

    [Fact]
    public void ArrayKeyframesInheritTheInstanceParameters()
    {
        Func<double, double> keyframeModifier = v => v;
        Func<double, double> instanceModifier = v => v;

        var animation = Presto.Animate(new StyleTarget(), new()
        {
            Keyframes = new Keyframe[]
            {
                new() { ["TranslateY"] = -40 },
                new() { ["TranslateX"] = 250, Duration = 100, Delay = 300, Ease = "linear", Modifier = keyframeModifier },
                new() { ["TranslateY"] = 40 },
                new() { ["TranslateX"] = 0 },
                new() { ["TranslateY"] = 0 },
            },
            Duration = 1500,
            Delay = 200,
            Modifier = instanceModifier,
            Ease = "outQuad",
            Autoplay = false,
        });

        var first = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Equal(300, first._changeDuration);
        Assert.Equal(0, first._delay);
        Assert.Equal(.75, first._ease(.5));
        Assert.Same(instanceModifier, first._modifier);

        var second = TestHelpers.GetTweenAtIndex(animation, 1)!;
        Assert.Equal(100, second._changeDuration);
        Assert.Equal(300, second._delay);
        Assert.Equal(.5, second._ease(.5));
        Assert.Same(keyframeModifier, second._modifier);

        double[] expectedFrom = [0, -40, -40, 40, 40, 0, 0, 250, 250, 0];
        double[] expectedTo = [-40, -40, 40, 40, 0, 0, 250, 250, 0, 0];
        for (var i = 0; i < expectedTo.Length; i++)
        {
            var tween = TestHelpers.GetTweenAtIndex(animation, i)!;
            Assert.Equal(expectedFrom[i], tween._fromNumber);
            Assert.Equal(expectedTo[i], tween._toNumber);
        }
    }

    [Fact]
    public void ArrayKeyframesInheritUnitsFromTheKeyframeBeforeThem()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            ["TranslateX"] = new TweenParams[]
            {
                new() { To = TweenValue.FromTo(-20, -40) },
                new() { To = "5rem" },
                new() { To = "100%" },
                new() { To = 0 },
                new() { To = "10%" },
                new() { To = TweenValue.FromTo(50, 200) },
                new() { To = TweenValue.FromTo(25, "100px") },
            },
            Autoplay = false,
        });

        string[] expected = ["px", "rem", "%", "%", "%", "%", "px"];
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], TestHelpers.GetTweenAtIndex(animation, i)!._unit);

        Assert.Equal("-20px", target.TranslateX);
    }

    [Fact]
    public void ArrayBasedKeyframesWithFloatingPointDurations()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            ["X"] = new double[] { 100, 200, 300, 400 },
            Ease = "linear",
            Duration = 4000,
            Autoplay = false,
        });

        var keyDuration = Helpers.Round(4000d / 3, 12);
        Assert.Equal(keyDuration * 3, animation.Duration);

        double[] expected = [100, 200, 300, 400];
        for (var i = 0; i < expected.Length; i++)
        {
            animation.Seek(keyDuration * i);
            Assert.Equal(expected[i], target.X);
        }
    }

    [Fact]
    public void ArrayKeyframesWithPlaybackEase()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            Keyframes = new Keyframe[]
            {
                new() { ["Y"] = -40 },
                new() { ["X"] = 250 },
                new() { ["Y"] = 40 },
                new() { ["X"] = 0, Ease = "outQuad" },
                new() { ["Y"] = 0 },
            },
            Duration = 1000,
            PlaybackEase = "inOutQuad",
            Autoplay = false,
        });

        // Every tween defaults to linear, except the one that declares its own ease.
        Assert.Equal(.5, TestHelpers.GetTweenAtIndex(animation, 0)!._ease(.5));
        Assert.Equal(.5, TestHelpers.GetTweenAtIndex(animation, 1)!._ease(.5));
        Assert.Equal(.5, TestHelpers.GetTweenAtIndex(animation, 2)!._ease(.5));
        Assert.Equal(.75, TestHelpers.GetTweenAtIndex(animation, 3)!._ease(.5));

        animation.Seek(250);
        Assert.Equal(0, target.X);
        Assert.Equal(-25, target.Y);

        animation.Seek(500);
        Assert.Equal(250, target.X);
        Assert.Equal(0, target.Y);

        animation.Seek(750);
        Assert.Equal(0, target.X);
        Assert.Equal(25, target.Y);
    }

    [Fact]
    public void PercentageBasedKeyframesValues()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            Keyframes = new Dictionary<string, Keyframe>
            {
                ["0%"] = new() { ["X"] = 100, ["Y"] = 100 },
                ["20%"] = new() { ["X"] = -100 },
                ["50%"] = new() { ["X"] = 100 },
                ["80%"] = new() { ["X"] = -100 },
                ["100%"] = new() { ["X"] = 100, ["Y"] = -100 },
            },
            Duration = 1000,
            Ease = "linear",
            Autoplay = false,
        });

        double[] times = [0, 200, 500, 800, 1000];
        double[] expectedX = [100, -100, 100, -100, 100];
        double[] expectedY = [100, 60, 0, -60, -100];
        for (var i = 0; i < times.Length; i++)
        {
            animation.Seek(times[i]);
            Assert.Equal(expectedX[i], target.X);
            Assert.Equal(expectedY[i], target.Y);
        }
    }

    [Theory]
    [InlineData(nameof(Keyframe.Duration))]
    [InlineData(nameof(Keyframe.Delay))]
    [InlineData(nameof(Keyframe.Modifier))]
    [InlineData(nameof(Keyframe.Composition))]
    public void PercentageKeyframesRejectPerFrameSettings(string setting)
    {
        var frame = setting switch
        {
            nameof(Keyframe.Duration) => new Keyframe { ["X"] = 100, Duration = 200 },
            nameof(Keyframe.Delay) => new Keyframe { ["X"] = 100, Delay = 200 },
            nameof(Keyframe.Modifier) => new Keyframe { ["X"] = 100, Modifier = v => v },
            _ => new Keyframe { ["X"] = 100, Composition = Composition.Additive },
        };

        var error = Assert.Throws<ArgumentException>(() => { Keyframes _ = new Dictionary<string, Keyframe> { ["50%"] = frame }; });
        Assert.Contains("\"50%\"", error.Message);
        Assert.Contains(setting, error.Message);

        var builderError = Assert.Throws<ArgumentException>(() => Presto.Build().Keyframes(k => k.At(50, f =>
        {
            f.Property("X", 100);
            _ = setting switch
            {
                nameof(Keyframe.Duration) => f.Duration(200),
                nameof(Keyframe.Delay) => f.Delay(200),
                nameof(Keyframe.Modifier) => f.Modifier(v => v),
                _ => f.Composition(Composition.Additive),
            };
        })));
        Assert.Contains(setting, builderError.Message);
    }

    [Fact]
    public void PercentageKeyframesKeepAFramesEase()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            Keyframes = new Dictionary<string, Keyframe> { ["0%"] = new() { ["X"] = 0, Ease = "inQuad" }, ["100%"] = new() { ["X"] = 100 } },
            Duration = 1000,
            Ease = "linear",
            Autoplay = false,
        });

        animation.Seek(500);
        Assert.Equal(25, target.X, 6);
    }

    [Fact]
    public void PercentageBasedKeyframesWithFloatPercentageValues()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            Keyframes = new Dictionary<string, Keyframe>
            {
                ["0%"] = new() { ["X"] = 0 },
                ["21.5%"] = new() { ["X"] = 50 },
                ["100%"] = new() { ["X"] = 100 },
            },
            Duration = 1000,
            Ease = "linear",
            Autoplay = false,
        });

        animation.Seek(215);

        Assert.Equal(50, target.X);
    }

    [Fact]
    public void BackwardSeekUpdatesASingleObjectFormKeyframeAlongsideAMultiKeyframeProperty()
    {
        var target = new Dictionary<string, object?> { ["X"] = 0d, ["Y"] = 0d };
        var animation = Presto.Animate(target, new()
        {
            ["X"] = new TweenParams { To = 100 },
            ["Y"] = new TweenParams[] { new() { To = 50, Duration = 50 }, new() { To = 100, Duration = 50 } },
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        animation.Seek(100);
        Assert.Equal(100d, target["X"]);
        Assert.Equal(100d, target["Y"]);

        animation.Seek(50);
        Assert.Equal(50d, target["X"]);
        Assert.Equal(50d, target["Y"]);

        animation.Seek(0);
        Assert.Equal(0d, target["X"]);
        Assert.Equal(0d, target["Y"]);
    }

    [Fact]
    public void PercentageKeyframesSpanTheEngineDefaultDurationAndNotTheParentTimelines()
    {
        var tl = Presto.CreateTimeline(new() { Autoplay = false, Defaults = new() { Duration = 2000 } })
            .Add(new StyleTarget(), new()
            {
                Keyframes = new Dictionary<string, Keyframe> { ["0%"] = new() { ["X"] = 0 }, ["100%"] = new() { ["X"] = 100 } },
            });

        Assert.Equal(1000, TestHelpers.GetChildAtIndex(tl, 0)!.Duration);
    }

    [Fact]
    public void APercentageFrameAfterZeroStartsFromTheCurrentValue()
    {
        var target = new StyleTarget { X = 50 };
        var animation = Presto.Animate(target, new()
        {
            Keyframes = new Dictionary<string, Keyframe>
            {
                ["50%"] = new() { ["X"] = 150 },
                ["100%"] = new() { ["X"] = 0 },
            },
            Duration = 1000,
            Ease = "linear",
            Autoplay = false,
        });

        double[] times = [0, 250, 500, 750];
        double[] expected = [50, 100, 150, 75];
        for (var i = 0; i < times.Length; i++)
        {
            animation.Seek(times[i]);
            Assert.Equal(expected[i], target.X);
        }
    }

    [Theory]
    [InlineData("half")]
    [InlineData("%50")]
    [InlineData("50px")]
    public void AMalformedPercentageKeyThrows(string key) =>
        Assert.Throws<FormatException>(() => Presto.Animate(new StyleTarget(), new()
        {
            Keyframes = new Dictionary<string, Keyframe> { [key] = new() { ["X"] = 100 } },
            Duration = 1000,
            Autoplay = false,
        }));
}
