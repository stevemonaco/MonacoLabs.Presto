using System.Globalization;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// No upstream suite covers the parser directly; the expected behavior is that of <c>parseEaseString</c> and
/// <c>parseEase</c> in src/easings/eases/parser.js.
/// </summary>
public sealed class EaseParserTests
{
    private static void AssertSameSamples(Ease expected, Ease actual)
    {
        foreach (var t in new[] { 0, .25, .5, .75, 1 })
        {
            Assert.Equal(expected(t), actual(t));
        }
    }

    public static TheoryData<string, Ease> Arguments => new()
    {
        { "inOutBack(2.5)", Eases.InOutBack(2.5) },
        { "outElastic(1.5, 1)", Eases.OutElastic(1.5, 1) },
        { "inOutElastic(2,.5)", Eases.InOutElastic(2, .5) },
        { "in()", Eases.In() },
        { " outBack ( 2 ) ", Eases.OutBack(2) },
        { "InOutQuint", Eases.InOutQuint },
        { "Out(2)", Eases.Out(2) },
        { "wiggle(6)", Eases.Wiggle(6) },
        { "Wiggle(6, anticipate)", Eases.Wiggle(6, WiggleType.Anticipate) },
        { "wiggle(4, EaseInOut)", Eases.Wiggle(4, WiggleType.EaseInOut) },
        { "wiggle(5, random)", Eases.Wiggle(5, WiggleType.Random, new Random(1)) },
        { "rough(12, 2)", Eases.Rough(12, 2, random: new Random(1)) },
        { "Rough()", Eases.Rough(random: new Random(1)) },
        { "slowMo(.5)", Eases.SlowMo(.5) },
        { "slowMo(.5, .3, true)", Eases.SlowMo(.5, .3, true) },
        { "expoScale(1, 10)", Eases.ExpoScale(1, 10) },
        { "ExpoScale(2, .5, outElastic(1.5, .5))", Eases.ExpoScale(2, .5, Eases.OutElastic(1.5, .5)) },
    };

    [Theory]
    [MemberData(nameof(Arguments))]
    public void ParsesToMatchingEase(string ease, Ease expected) => AssertSameSamples(expected, EaseParser.Parse(ease));

    [Theory]
    [InlineData("linear")]
    [InlineData("none")]
    [InlineData("")]
    [InlineData("  ")]
    public void ResolvesToEasesNone(string ease) => Assert.Same(Eases.None, EaseParser.Parse(ease));

    [Theory]
    [InlineData("bogus")]
    [InlineData("outQaud")]
    [InlineData("linear(0, 1)")]
    [InlineData("irregular(5)")]
    [InlineData("cubicBezier(.5, 0)")]
    public void UnknownNamesAndCodeOnlyFormsThrow(string ease)
    {
        Assert.Throws<FormatException>(() => EaseParser.Parse(ease));
        Assert.False(EaseParser.TryParse(ease, out _));
    }

    [Fact]
    public void StepsAndCubicBezierParseToTheirFactories()
    {
        Ease[] parsed = [EaseParser.Parse("steps(4)"), EaseParser.Parse("steps(4, true)"), EaseParser.Parse("cubicBezier(.17, .67, .83, .67)")];
        Ease[] built = [Eases.Steps(4), Eases.Steps(4, true), Eases.CubicBezier(.17, .67, .83, .67)];
        for (var i = 0; i < parsed.Length; i++)
        {
            for (var t = 0.0; t <= 1; t += .05) Assert.Equal(built[i](t), parsed[i](t), 12);
        }
        Assert.True(EaseParser.TryParse("Steps(3)", out var pascal));
        Assert.Equal(Eases.Steps(3)(.5), pascal(.5));
    }

    [Theory]
    [InlineData("steps(0)")]
    [InlineData("steps(-1)")]
    [InlineData("steps(0.5, true)")]
    [InlineData("steps(NaN)")]
    public void ParsedStepsBelowOneIsAFormatError(string ease)
    {
        Assert.Throws<FormatException>(() => EaseParser.Parse(ease));
        Assert.False(EaseParser.TryParse(ease, out var result));
        Assert.Null(result);
    }

    [Fact]
    public void NullEaseStringThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => EaseParser.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => EaseParser.TryParse(null!, out _));
    }

    [Theory]
    [InlineData("inQuad(2)")]
    [InlineData("in(abc)")]
    [InlineData("in(2")]
    [InlineData("wiggle(6, wobbly)")]
    [InlineData("slowMo(.5, .7, maybe)")]
    [InlineData("expoScale(1)")]
    public void MalformedStringsThrow(string ease) => Assert.Throws<FormatException>(() => EaseParser.Parse(ease));

    [Theory]
    [InlineData("fromPath(M0,0 C0.2,0 0.4,1 1,1)")]
    [InlineData("FromPath( M0,0 C0.2,0 0.4,1 1,1 )")]
    public void FromPathTakesTheRawPath(string ease) =>
        AssertSameSamples(Eases.FromPath("M0,0 C0.2,0 0.4,1 1,1"), EaseParser.Parse(ease));

    [Theory]
    [InlineData("out(2)")]
    [InlineData("fromPath(M0,0 L1,1)")]
    public void SameStringReturnsSameInstance(string ease) => Assert.Same(EaseParser.Parse(ease), EaseParser.Parse(ease));

    [Fact]
    public void ParsesArgumentsCultureInvariantly()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            AssertSameSamples(Eases.Out(2.5), EaseParser.Parse("out(2.5)"));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
