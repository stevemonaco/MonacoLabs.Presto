using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="TweenKeyframesBuilder.Color"/>: a color tween expanded into sliced-ease segments.</summary>
public sealed class ColorKeyframesTests : EngineTestBase
{
    private const string Blue = "#0000ff";
    private const string Yellow = "#ffff00";

    // Springs overshoot saturated pairs past the sRGB gamut, where the gamut-mapped path itself wavers by up to 0.02.
    public static TheoryData<string, ColorSpace, string, string, double> PathCases => new()
    {
        { "linear", ColorSpace.Oklab, Blue, Yellow, .025 }, { "linear", ColorSpace.Oklch, Blue, Yellow, .025 },
        { "outCubic", ColorSpace.Oklab, Blue, Yellow, .025 }, { "outCubic", ColorSpace.Oklch, Blue, Yellow, .025 },
        { "inOutBack", ColorSpace.Oklab, Blue, Yellow, .025 }, { "inOutBack", ColorSpace.Oklch, Blue, Yellow, .025 },
        { "outCubic", ColorSpace.Oklch, "#ff0000", "#00ff00", .025 }, { "inOutBack", ColorSpace.Srgb, "#ff0000", "#00ff00", .025 },
        { "spring", ColorSpace.Oklab, "#2563eb", "#f59e0b", .025 }, { "spring", ColorSpace.Oklch, "#2563eb", "#f59e0b", .025 },
        { "spring", ColorSpace.Oklab, Blue, Yellow, .07 }, { "spring", ColorSpace.Oklch, Blue, Yellow, .07 },
    };

    [Theory]
    [MemberData(nameof(PathCases))]
    public void TheRenderedColorStaysNearThePath(string easeName, ColorSpace space, string from, string to, double tolerance)
    {
        var spring = new Spring(bounce: .4, duration: 500);
        var (ease, easeValue, duration) = easeName switch
        {
            "linear" => (Eases.None, (EaseValue)Eases.None, 1000d),
            "outCubic" => (Eases.OutCubic, (EaseValue)Eases.OutCubic, 1000d),
            "inOutBack" => (Eases.InOutBack(), (EaseValue)Eases.InOutBack(), 1000d),
            _ => (spring.PureEase, (EaseValue)spring, spring.SettlingDuration),
        };
        var target = new StyleTarget();
        var animation = Presto.Build().Property("BackgroundColor", k => k.Color(from, to, space, duration: 1000, ease: easeValue)).Animate(target);

        for (var i = 1; i < 400; i++)
        {
            var t = i / 400.0;
            animation.Seek(t * duration);
            var expected = PrestoColor.Mix(PrestoColor.Parse(from), PrestoColor.Parse(to), ease(t), space);
            Assert.True(DeltaEok(expected, PrestoColor.Parse(target.BackgroundColor)) < tolerance, $"t = {t}: {target.BackgroundColor} vs {expected}");
        }
    }

    [Fact]
    public void FittedSegmentsStayFewOnEasyPathsAndCappedOnHardOnes()
    {
        Assert.InRange(Frames(new TweenKeyframesBuilder().Color("#2563eb", "#f59e0b", ease: Eases.None)).Length, 1, 2);
        Assert.Equal(16, Frames(new TweenKeyframesBuilder().Color(Blue, Yellow, ColorSpace.Oklch, ease: new Spring(bounce: .4, duration: 500))).Length);
    }

    [Fact]
    public void FittedSegmentsSplitTheDurationExactly()
    {
        var frames = Frames(new TweenKeyframesBuilder().Color(Blue, Yellow, ColorSpace.Oklch, duration: 1000, ease: Eases.InOutBack()));
        Assert.True(frames.Length > 2);
        Assert.Equal(1000, frames.Sum(f => JsNumber.ToNumber(((TweenParams)f.Raw!).Duration!.Value.Raw, 0)));
    }

    [Fact]
    public void StepsHoldBetweenSteps()
    {
        var target = new StyleTarget();
        var animation = Presto.Build().Property("BackgroundColor", k => k.Color(Blue, Yellow, duration: 1000, ease: Eases.Steps(4))).Animate(target);

        animation.Seek(10);
        var first = target.BackgroundColor;
        animation.Seek(240);
        Assert.Equal(first, target.BackgroundColor);
        animation.Seek(260);
        var second = target.BackgroundColor;
        animation.Seek(490);
        Assert.Equal(second, target.BackgroundColor);

        Assert.Equal(PrestoColor.Parse(Blue).ToString(), first);
        Assert.Equal(PrestoColor.Mix(PrestoColor.Parse(Blue), PrestoColor.Parse(Yellow), .25).ToString(), second);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheTweenCompletesExactlyOnTo(bool reduceAll)
    {
        Presto.Defaults.ReduceAll = reduceAll;
        var target = new StyleTarget();
        var animation = Presto.Build()
            .Property("BackgroundColor", k => k.Color("#2563eb", "#f59e0b", ColorSpace.Oklch, duration: 600, ease: Eases.InOutBack()))
            .Animate(target);

        RunUntil(() => animation.Completed);

        Assert.True(animation.Completed);
        Assert.Equal("rgba(245,158,11,1)", target.BackgroundColor);
    }

    [Fact]
    public void AnOklchFullTurnCirclesTheHue()
    {
        var start = PrestoColor.FromOklch(.7, .15, 30);
        var target = new StyleTarget();
        var animation = Presto.Build()
            .Property("BackgroundColor", k => k.Color(start, start, ColorSpace.Oklch, HueInterpolation.Increasing, duration: 1200, ease: Eases.None))
            .Animate(target);

        animation.Seek(600);
        Assert.InRange(PrestoColor.Parse(target.BackgroundColor).ToOklch().H, 190, 230);
    }

    public static TheoryData<ColorSpace, HueInterpolation, string, string, int?, int> SegmentCases => new()
    {
        { ColorSpace.Srgb, HueInterpolation.Shorter, Blue, Yellow, null, 4 },
        { ColorSpace.LinearSrgb, HueInterpolation.Shorter, Blue, Yellow, null, 4 },
        { ColorSpace.Oklab, HueInterpolation.Shorter, Blue, Yellow, null, 4 },
        { ColorSpace.Oklch, HueInterpolation.Shorter, "#ff0000", "#ff8000", null, 4 },
        { ColorSpace.Oklch, HueInterpolation.Shorter, Blue, Yellow, null, 6 },
        { ColorSpace.Oklch, HueInterpolation.Longer, Blue, Yellow, null, 7 },
        { ColorSpace.Oklch, HueInterpolation.Shorter, "#ffffff", Blue, null, 4 },
        { ColorSpace.Oklab, HueInterpolation.Shorter, Blue, Yellow, 7, 7 },
        { ColorSpace.Oklch, HueInterpolation.Longer, Blue, Yellow, 1, 1 },
    };

    [Theory]
    [MemberData(nameof(SegmentCases))]
    public void PerTargetSegmentCountsFollowTheHueArc(ColorSpace space, HueInterpolation hue, string from, string to, int? segments, int expected)
    {
        var perTarget = (FunctionValue)((_, _, _) => 1000);
        Assert.Equal(expected, Frames(new TweenKeyframesBuilder().Color(from, to, space, hue, duration: perTarget, segments: segments)).Length);
    }

    [Fact]
    public void AnExplicitSegmentCountIsKept() =>
        Assert.Equal(7, Frames(new TweenKeyframesBuilder().Color(Blue, Yellow, segments: 7)).Length);

    [Fact]
    public void FunctionValuedEndsResolvePerTargetUnderAStagger()
    {
        string[] starts = ["#ff0000", "#00ff00", Blue];
        var targets = new[] { new StyleTarget(), new StyleTarget(), new StyleTarget() };
        var animation = Presto.Build()
            .Property("BackgroundColor", k => k.Color((FunctionValue)((_, i, _) => starts[i]), Yellow, ColorSpace.Oklch, duration: 1000, ease: Eases.None))
            .Delay(Presto.Stagger(100))
            .Animate(targets);

        animation.Seek(600);
        for (var i = 0; i < targets.Length; i++)
        {
            var expected = PrestoColor.Mix(PrestoColor.Parse(starts[i]), PrestoColor.Parse(Yellow), (600 - 100 * i) / 1000.0, ColorSpace.Oklch);
            Assert.True(DeltaEok(expected, PrestoColor.Parse(targets[i].BackgroundColor)) < .02, $"target {i}: {targets[i].BackgroundColor} vs {expected}");
        }

        animation.Seek(animation.Duration);
        Assert.All(targets, t => Assert.Equal("rgba(255,255,0,1)", t.BackgroundColor));
    }

    [Fact]
    public void AnEndThatIsNotAColorThrows() =>
        Assert.Throws<ArgumentException>(() => new TweenKeyframesBuilder().Color("10px", Yellow));

    [Fact]
    public void UnsetTimingComesFromTheDefaultsTheAnimationIsAddedUnder()
    {
        var builder = Presto.Build().Property("BackgroundColor", k => k.Color(Blue, Yellow)).Duration(900);
        var timeline = Presto.CreateTimeline(new() { Defaults = new() { Duration = 400 } });

        timeline.Add(new StyleTarget(), builder);
        Presto.Defaults.Duration = 700;
        var animation = builder.Animate(new StyleTarget());

        Assert.Equal(400, timeline.Duration);
        Assert.Equal(700, animation.Duration);
    }

    private static TweenValue[] Frames(TweenKeyframesBuilder builder) => TweenKeyframesBuilder.Expand(builder.ToArray(), Presto.Defaults);

    private static double DeltaEok(PrestoColor x, PrestoColor y)
    {
        var p = x.ToOklab();
        var q = y.ToOklab();
        return ColorConversions.DeltaEok(p.L, p.A, p.B, q.L, q.A, q.B);
    }
}
