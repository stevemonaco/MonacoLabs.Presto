using System.Globalization;

namespace Monaco.Presto.Tests;

/// <summary>
/// No upstream suite covers the parser directly; the expected behaviour is that of <c>parseEaseString</c> and
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
    public void MalformedStringsThrow(string ease) => Assert.Throws<FormatException>(() => EaseParser.Parse(ease));

    [Fact]
    public void SameStringReturnsSameInstance() => Assert.Same(EaseParser.Parse("out(2)"), EaseParser.Parse("out(2)"));

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
