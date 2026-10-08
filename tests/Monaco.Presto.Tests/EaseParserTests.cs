using System.Globalization;

namespace Monaco.Presto.Tests;

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
    [InlineData("bogus")]
    [InlineData("steps(4)")]
    [InlineData("cubicBezier(0,0,1,1)")]
    [InlineData("linear(0, 1)")]
    [InlineData("irregular(5)")]
    public void ResolvesToEasesNone(string ease) => Assert.Same(Eases.None, EaseParser.Parse(ease));

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
