using System.Text.Json;
using Monaco.Presto.Values;

namespace Monaco.Presto.Tests;

/// <summary>
/// CSS Color 4 conversions, gamut mapping, <see cref="PrestoColor.Mix"/> and the relative-color helpers against culori,
/// dumped to colors.json by tools/anime-fixtures (docs/COLOR-SPACES-PLAN.md decision 2 and §4).
/// </summary>
public sealed class ColorConversionTests
{
    private static readonly ColorFixtures _fixtures = JsonSerializer.Deserialize<ColorFixtures>(
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "colors.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static readonly PrestoColor _red = new(255, 0, 0);
    private static readonly PrestoColor _blue600 = new(37, 99, 235);
    private static readonly PrestoColor _white = new(255, 255, 255);

    public static TheoryData<string> ColorNames => new(_fixtures.Colors.Select(c => c.Name));
    public static TheoryData<int> GamutCases => new(Enumerable.Range(0, _fixtures.Gamut.Length));
    public static TheoryData<int> MixCases => new(Enumerable.Range(0, _fixtures.Mixes.Length));
    public static TheoryData<int> RelativeCases => new(Enumerable.Range(0, _fixtures.Relative.Length));
    public static TheoryData<ColorSpace> Spaces => new(Enum.GetValues<ColorSpace>());

    [Theory]
    [MemberData(nameof(ColorNames))]
    public void ConversionsMatchCulori(string name)
    {
        var fixture = _fixtures.Colors.Single(c => c.Name == name);
        var color = ColorOf(fixture.Rgba);

        Assert.Equal(fixture.Linear[0], ColorConversions.SrgbToLinear(color.R / 255), 1e-6);
        Assert.Equal(fixture.Linear[1], ColorConversions.SrgbToLinear(color.G / 255), 1e-6);
        Assert.Equal(fixture.Linear[2], ColorConversions.SrgbToLinear(color.B / 255), 1e-6);

        var lab = color.ToOklab();
        Assert.Equal(fixture.Oklab[0], lab.L, 1e-6);
        Assert.Equal(fixture.Oklab[1], lab.A, 1e-6);
        Assert.Equal(fixture.Oklab[2], lab.B, 1e-6);
        Assert.Equal(color.A, lab.Alpha);

        var lch = color.ToOklch();
        Assert.Equal(fixture.Oklch[0], lch.L, 1e-6);
        Assert.Equal(fixture.Oklch[1], lch.C, 1e-6);
        if (lch.C >= ColorConversions.AchromaticChroma)
        {
            var dh = Math.Abs(fixture.Oklch[2] - lch.H) % 360;
            Assert.True(Math.Min(dh, 360 - dh) < 1e-6, $"hue {lch.H} vs {fixture.Oklch[2]}");
        }
        Assert.InRange(lch.H, 0, 360);
    }

    [Theory]
    [MemberData(nameof(ColorNames))]
    public void InGamutColorsRoundTrip(string name)
    {
        var color = ColorOf(_fixtures.Colors.Single(c => c.Name == name).Rgba);
        var lab = color.ToOklab();
        var lch = color.ToOklch();

        AssertChannels(color, PrestoColor.FromOklab(lab.L, lab.A, lab.B, lab.Alpha), 1e-9);
        AssertChannels(color, PrestoColor.FromOklch(lch.L, lch.C, lch.H, lch.A), 1e-9);
    }

    // culori bisects to within 1e-4 of chroma like we do, so the same steps reach the same clipped color.
    [Theory]
    [MemberData(nameof(GamutCases))]
    public void GamutMappingMatchesCulori(int index)
    {
        var fixture = _fixtures.Gamut[index];
        var o = fixture.Oklch;
        var mapped = PrestoColor.FromOklch(o[0], o[1], o[2], o[3]);

        AssertChannels(ColorOf(fixture.Rgba), mapped);
        AssertInGamut(mapped);
    }

    [Theory]
    [MemberData(nameof(MixCases))]
    public void MixMatchesCulori(int index)
    {
        var fixture = _fixtures.Mixes[index];
        var space = Enum.Parse<ColorSpace>(fixture.Space, ignoreCase: true);
        var hue = Enum.Parse<HueInterpolation>(fixture.Hue, ignoreCase: true);

        var mixed = PrestoColor.Mix(ColorOf(fixture.From), ColorOf(fixture.To), fixture.T, space, hue);

        AssertChannels(ColorOf(fixture.Result), mixed);
    }

    [Theory]
    [MemberData(nameof(RelativeCases))]
    public void RelativeHelpersMatchCulori(int index)
    {
        var fixture = _fixtures.Relative[index];
        var color = ColorOf(fixture.Base);

        var result = fixture.Op == "with"
            ? color.WithOklch(fixture.L, fixture.C, fixture.H)
            : color.ShiftOklch(fixture.L ?? 0, fixture.C ?? 0, fixture.H ?? 0);

        AssertChannels(ColorOf(fixture.Result), result);
        AssertInGamut(result);
    }

    [Fact]
    public void TransparentToRedIsRedAtHalfAlphaHalfway()
    {
        var mixed = PrestoColor.Mix(new(0, 0, 0, 0), _red, .5);

        AssertChannels(new(255, 0, 0, .5), mixed);
    }

    [Fact]
    public void WhiteToBlueInOklchKeepsBluesHue()
    {
        var blueHue = _blue600.ToOklch().H;

        foreach (var t in (double[])[.25, .5, .75])
            Assert.Equal(blueHue, PrestoColor.Mix(_white, _blue600, t, ColorSpace.Oklch).ToOklch().H, 1e-3);
    }

    [Theory]
    [InlineData(HueInterpolation.Shorter, 0)]
    [InlineData(HueInterpolation.Longer, 180)]
    [InlineData(HueInterpolation.Increasing, 0)]
    [InlineData(HueInterpolation.Decreasing, 180)]
    public void HueModesTakeTheirArcFrom350To10(HueInterpolation mode, double halfway)
    {
        var from = PrestoColor.FromOklch(.7, .1, 350);
        var to = PrestoColor.FromOklch(.7, .1, 10);

        AssertHue(halfway, PrestoColor.Mix(from, to, .5, ColorSpace.Oklch, mode));
        if (mode == HueInterpolation.Increasing) AssertHue(355, PrestoColor.Mix(from, to, .25, ColorSpace.Oklch, mode));
        if (mode == HueInterpolation.Decreasing) AssertHue(265, PrestoColor.Mix(from, to, .25, ColorSpace.Oklch, mode));
    }

    [Theory]
    [InlineData(HueInterpolation.Increasing, 90)]
    [InlineData(HueInterpolation.Decreasing, -90)]
    public void EqualHuesMakeAFullTurnWhenIncreasingOrDecreasing(HueInterpolation mode, double quarter)
    {
        var color = PrestoColor.FromOklch(.7, .1, 40);
        var h = color.ToOklch().H;

        AssertHue(h + 180, PrestoColor.Mix(color, color, .5, ColorSpace.Oklch, mode));
        AssertHue(h + quarter, PrestoColor.Mix(color, color, .25, ColorSpace.Oklch, mode));
    }

    [Theory]
    [MemberData(nameof(Spaces))]
    public void EndsAreExact(ColorSpace space)
    {
        var a = new PrestoColor(37.3, 99.1, 235.7, .4);
        var b = new PrestoColor(245, 158.2, 11, .9);

        Assert.Equal(a, PrestoColor.Mix(a, b, 0, space, HueInterpolation.Longer));
        Assert.Equal(b, PrestoColor.Mix(a, b, 1, space, HueInterpolation.Longer));
    }

    [Theory]
    [MemberData(nameof(Spaces))]
    public void ExtrapolationStaysInGamut(ColorSpace space)
    {
        var from = new PrestoColor(37, 99, 235, .5);
        var to = new PrestoColor(245, 158, 11);

        foreach (var t in (double[])[-1.5, -.25, 1.25, 2.5])
        {
            var mixed = PrestoColor.Mix(from, to, t, space, HueInterpolation.Longer);
            AssertInGamut(mixed);
            Assert.InRange(mixed.A, 0, 1);
        }
    }

    private static PrestoColor ColorOf(double[] rgba) => new(rgba[0], rgba[1], rgba[2], rgba[3]);

    private static void AssertHue(double expected, PrestoColor color)
    {
        var d = Math.Abs(ColorConversions.NormalizeHue(expected) - color.ToOklch().H);
        Assert.True(Math.Min(d, 360 - d) < .5, $"hue {color.ToOklch().H} vs {expected}");
    }

    private static void AssertChannels(PrestoColor expected, PrestoColor actual, double tolerance = 1e-6)
    {
        Assert.Equal(expected.R, actual.R, tolerance);
        Assert.Equal(expected.G, actual.G, tolerance);
        Assert.Equal(expected.B, actual.B, tolerance);
        Assert.Equal(expected.A, actual.A, 1e-12);
    }

    private static void AssertInGamut(PrestoColor color)
    {
        foreach (var channel in (double[])[color.R, color.G, color.B])
        {
            Assert.False(double.IsNaN(channel));
            Assert.InRange(channel, 0, 255);
        }
    }

    private sealed record ColorFixtures(ColorFixture[] Colors, MixFixture[] Mixes, GamutFixture[] Gamut, RelativeFixture[] Relative);

    private sealed record ColorFixture(string Name, double[] Rgba, double[] Linear, double[] Oklab, double[] Oklch);

    private sealed record MixFixture(double[] From, double[] To, string Space, string Hue, double T, double[] Result);

    private sealed record GamutFixture(double[] Oklch, double[] Rgba);

    private sealed record RelativeFixture(double[] Base, string Op, double? L, double? C, double? H, double[] Result);
}
