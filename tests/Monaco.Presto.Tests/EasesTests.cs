using System.Globalization;
using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

/// <summary>
/// Ports tests/suites/eases.test.js by evaluating the ease functions directly, rounding to 2 decimals like the
/// suite's <c>getOpacityValue()</c>; <see cref="AnAnimationAppliesItsEase"/> covers the suite's animate-driven form.
/// </summary>
public sealed class EasesTests : EngineTestBase
{
    private static double Sample(Ease ease, double t) => Helpers.Round(ease(t), 2);

    public static TheoryData<Ease, Ease, Ease, Ease> BuiltIns => new()
    {
        { Eases.In(), Eases.Out(), Eases.InOut(), Eases.OutIn() },
        { Eases.InQuad, Eases.OutQuad, Eases.InOutQuad, Eases.OutInQuad },
        { Eases.InCubic, Eases.OutCubic, Eases.InOutCubic, Eases.OutInCubic },
        { Eases.InQuart, Eases.OutQuart, Eases.InOutQuart, Eases.OutInQuart },
        { Eases.InQuint, Eases.OutQuint, Eases.InOutQuint, Eases.OutInQuint },
        { Eases.InSine, Eases.OutSine, Eases.InOutSine, Eases.OutInSine },
        { Eases.InCirc, Eases.OutCirc, Eases.InOutCirc, Eases.OutInCirc },
        { Eases.InExpo, Eases.OutExpo, Eases.InOutExpo, Eases.OutInExpo },
        { Eases.InBounce, Eases.OutBounce, Eases.InOutBounce, Eases.OutInBounce },
        { Eases.InBack(), Eases.OutBack(), Eases.InOutBack(), Eases.OutInBack() },
        { Eases.InElastic(), Eases.OutElastic(), Eases.InOutElastic(), Eases.OutInElastic() },
    };

    [Theory]
    [MemberData(nameof(BuiltIns))]
    public void BuiltInEaseTypesAtMidpoint(Ease easeIn, Ease easeOut, Ease easeInOut, Ease easeOutIn)
    {
        Assert.True(Sample(easeIn, .5) < .5);
        Assert.True(Sample(easeOut, .5) > .5);
        Assert.Equal(.5, Sample(easeInOut, .5));
        Assert.Equal(.5, Sample(easeOutIn, .5));
    }

    public static TheoryData<Ease> AnimatedEases => new(Eases.None, Eases.InQuad, Eases.OutQuad, Eases.InOutQuad, Eases.OutElastic());

    [Theory]
    [MemberData(nameof(AnimatedEases))]
    public void AnAnimationAppliesItsEase(Ease ease)
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new()
        {
            ["Opacity"] = TweenValue.FromTo(0, 1),
            Duration = 100,
            Ease = ease,
            Autoplay = false,
        });

        animation.Seek(50);

        Assert.Equal(Sample(ease, .5), Helpers.Round(target.Opacity, 2));
    }

    [Fact]
    public void CustomPowerEase()
    {
        Assert.Equal(.5, Sample(Eases.In(1), .5));
        Assert.Equal(.35, Sample(Eases.In(1.5), .5));
        Assert.Equal(.25, Sample(Eases.In(2), .5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(.5)]
    [InlineData(1)]
    public void NoneIsIdentity(double t) => Assert.Equal(t, Eases.None(t));

    [Fact]
    public void CompositionMirrorsAndHalvesACustomEase()
    {
        Ease f = t => Math.Pow(t, 1.5);

        foreach (var t in new[] { 0, .25, .5, .75, 1 })
        {
            Assert.Equal(1 - f(1 - t), Eases.Out(f)(t));
        }
        Assert.Equal(.5, Eases.InOut(f)(.5));
    }

    [Fact]
    public void DefaultEaseIsOutTwo()
    {
        var expected = Eases.Out(2);
        var actual = new PrestoDefaults().Ease.Resolve(new object(), 0, 1, out _);

        foreach (var t in new[] { 0, .25, .5, .75, 1 })
        {
            Assert.Equal(expected(t), actual(t));
        }
    }

    [Fact]
    public void LinearWithoutStopsIsNone() => Assert.Same(Eases.None, Eases.Linear());

    [Fact]
    public void LinearZeroToOne()
    {
        var ease = Eases.Linear(0, 1);
        Assert.Equal(0, Sample(ease, 0));
        Assert.Equal(.5, Sample(ease, .5));
        Assert.Equal(1, Sample(ease, 1));
    }

    [Fact]
    public void CustomLinear()
    {
        var ease = Eases.Linear(0, .25, 1);
        Assert.Equal(0, Sample(ease, 0));
        Assert.Equal(.25, Sample(ease, .5));
        Assert.Equal(1, Sample(ease, 1));
    }

    [Fact]
    public void CustomUnevenLinear()
    {
        var ease = Eases.Linear(0, "0.25 75%", 1);
        Assert.Equal(0, Sample(ease, 0));
        Assert.Equal(.25, Sample(ease, .75));
        Assert.Equal(1, Sample(ease, 1));
    }

    [Fact]
    public void LinearReturnsLastValuePastTheEnd() => Assert.Equal(1, Eases.Linear(0, .5, .5, 1)(1.5));

    [Theory]
    [InlineData("0.25 75%", .25, .75)]
    [InlineData("0.25 75", .25, .75)]
    [InlineData("0.25", .25, null)]
    public void LinearStopParsesValueAndPercent(string s, double value, double? position)
    {
        LinearStop stop = s;
        Assert.Equal(value, stop.Value);
        Assert.Equal(position, stop.Position);
    }

    [Fact]
    public void LinearStopRejectsGarbage() => Assert.Throws<FormatException>(() => (LinearStop)"garbage");

    [Fact]
    public void LinearStopParsesCultureInvariantly()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            Assert.Throws<FormatException>(() => (LinearStop)"0,5");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void CubicBezierIn() => Assert.True(Sample(Eases.CubicBezier(1, 0, 1, 0), .5) < .5);

    [Fact]
    public void CubicBezierOut() => Assert.True(Sample(Eases.CubicBezier(0, 1, 0, 1), .5) > .5);

    [Fact]
    public void CubicBezierInOut() => Assert.Equal(.5, Sample(Eases.CubicBezier(1, 0, 0, 1), .5));

    [Fact]
    public void CubicBezierOnTheDiagonalIsNone()
    {
        Assert.NotSame(Eases.None, Eases.CubicBezier());
        Assert.Same(Eases.None, Eases.CubicBezier(0, 0, 1, 1));
        Assert.Same(Eases.None, Eases.CubicBezier(.3, .3, .7, .7));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(.24, 0)]
    [InlineData(.25, .25)]
    [InlineData(.49, .25)]
    [InlineData(.5, .5)]
    [InlineData(.74, .5)]
    [InlineData(.75, .75)]
    [InlineData(.99, .75)]
    [InlineData(1, 1)]
    public void StepsFromEnd(double t, double expected) => Assert.Equal(expected, Sample(Eases.Steps(4), t));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(.01, .25)]
    [InlineData(.24, .25)]
    [InlineData(.25, .25)]
    [InlineData(.49, .5)]
    [InlineData(.5, .5)]
    [InlineData(.74, .75)]
    [InlineData(.75, .75)]
    [InlineData(.99, 1)]
    [InlineData(1, 1)]
    public void StepsFromStart(double t, double expected) => Assert.Equal(expected, Sample(Eases.Steps(4, true), t));

    [Fact]
    public void StepsClampsOutOfRangeInput()
    {
        Assert.Equal(0, Eases.Steps(4)(-1));
        Assert.Equal(1, Eases.Steps(4)(2));
    }

    [Fact]
    public void IrregularStartsAtZeroEndsAtOneAndNeverDecreases()
    {
        var ease = Eases.Irregular();
        Assert.Equal(0, ease(0));
        Assert.Equal(1, ease(1));

        var previous = 0d;
        for (var i = 0; i <= 1000; i++)
        {
            var value = ease(i / 1000d);
            Assert.True(value >= previous);
            previous = value;
        }
    }

    [Fact]
    public void IrregularWithoutRandomnessIsIdentity()
    {
        var ease = Eases.Irregular(randomness: 0);
        for (var i = 0; i <= 100; i++)
        {
            Assert.Equal(i / 100d, ease(i / 100d), 1e-12);
        }
    }

    [Fact]
    public void IrregularIsDeterministicForASeed()
    {
        var first = Eases.Irregular(random: new Random(42));
        var second = Eases.Irregular(random: new Random(42));
        for (var i = 0; i <= 100; i++)
        {
            Assert.Equal(first(i / 100d), second(i / 100d));
        }
    }

    [Fact]
    public void IrregularWithTwoStepsIsIdentity()
    {
        var ease = Eases.Irregular(2);
        foreach (var t in new[] { 0, .25, .5, .75, 1 })
        {
            Assert.Equal(t, ease(t));
        }
    }
}
