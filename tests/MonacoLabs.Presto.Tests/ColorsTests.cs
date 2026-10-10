using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

/// <summary>Port of anime.js tests/suites/colors.test.js at parser level and through the tweens <c>animate</c> builds from the same table.</summary>
public sealed class ColorsTests : EngineTestBase
{
    public static TheoryData<string, string, double[], double[]> FromToPairs()
    {
        string[][] fromInputs = [["#f99", "#ff9999", "rgb(255, 153, 153)", "hsl(0, 100%, 80%)"], ["#f999", "#ff999999", "rgba(255, 153, 153, .6)", "hsla(0, 100%, 80%, .6)"]];
        double[][] fromOutputs = [[255, 153, 153, 1], [255, 153, 153, .6]];
        string[][] toInputs = [["#0FF", "#00FFFF", "rgb(0, 255, 255)", "hsl(180, 100%, 50%)"], ["#0FFC", "#00FFFFCC", "rgba(0, 255, 255, .8)", "hsla(180, 100%, 50%, .8)"]];
        double[][] toOutputs = [[0, 255, 255, 1], [0, 255, 255, .8]];

        var pairs = new TheoryData<string, string, double[], double[]>();
        for (var fromType = 0; fromType < fromInputs.Length; fromType++)
        {
            for (var toType = 0; toType < toInputs.Length; toType++)
            {
                foreach (var from in fromInputs[fromType])
                {
                    foreach (var to in toInputs[toType]) pairs.Add(from, to, fromOutputs[fromType], toOutputs[toType]);
                }
            }
        }
        return pairs;
    }

    [Theory]
    [MemberData(nameof(FromToPairs))]
    public void ConvertsFromToColorPairs(string from, string to, double[] expectedFrom, double[] expectedTo)
    {
        var target = new Dictionary<string, object?>();
        var animation = Presto.Animate(target, new() { ["Color"] = TweenValue.FromTo(from, to), Autoplay = false });

        var tween = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Equal(expectedFrom, tween._fromNumbers);
        Assert.Equal(ValueKind.Color, tween._valueType);
        Assert.Equal(expectedTo, tween._toNumbers);

        Assert.Equal(PrestoColor.FromChannels(expectedFrom).ToString(), target["Color"]);
        animation.Seek(animation.Duration);
        Assert.Equal(PrestoColor.FromChannels(expectedTo).ToString(), target["Color"]);
    }

    [Theory]
    [InlineData("#FF4B4B", 255, 75, 75, 1)]
    [InlineData("rgb(255, 168, 40)", 255, 168, 40, 1)]
    [InlineData("hsl(44, 100%, 59%)", 255, 199, 46, 1)]
    [InlineData("#FF4B4B33", 255, 75, 75, .2)]
    [InlineData("rgba(255, 168, 40, .2)", 255, 168, 40, .2)]
    [InlineData("hsla(44, 100%, 59%, .2)", 255, 199, 46, .2)]
    public void AnimatesATypedColorMemberToTheParsedChannels(string to, double r, double g, double b, double a)
    {
        var target = new ColorTarget();
        var animation = Presto.Animate(target, new() { ["Background"] = to, Autoplay = false });

        var tween = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Equal(ValueKind.Color, tween._valueType);
        Assert.Equal([r, g, b, a], tween._toNumbers);

        animation.Seek(animation.Duration);
        Assert.Equal(new PrestoColor(r, g, b, a), target.Background);
    }

    [Fact]
    public void BoxShadowShouldNotBeParsedAsColorValue()
    {
        const string shadow = "rgb(255, 0, 0) 0px 0px 6px 0px";
        Assert.False(ValueParser.IsColor(shadow));
        var value = new DecomposedValue();
        ValueParser.Decompose(shadow, value);
        Assert.Equal(ValueKind.Complex, value.Kind);
    }

    [Fact]
    public void UnknownStringsParseToOpaqueBlack()
    {
        Assert.False(PrestoColor.TryParse("red", out _));
        Assert.Equal(new PrestoColor(0, 0, 0, 1), PrestoColor.Parse("red"));
    }

    [Fact]
    public void AchromaticHslKeepsUpstreamUnscaledChannels() =>
        Assert.Equal(new PrestoColor(.5, .5, .5, 1), PrestoColor.Parse("hsl(0, 0%, 50%)"));

    [Fact]
    public void ToStringWritesCompactRgba()
    {
        Assert.Equal("rgba(255,153,153,0.6)", new PrestoColor(255, 153, 153, .6).ToString());
        Assert.Equal("rgba(0,255,255,1)", new PrestoColor(0, 255, 255).ToString());
    }

    [Fact]
    public void FormatRgbaRoundsColorChannelsOnly() =>
        Assert.Equal("rgba(255,0,100,0.5)", PrestoColor.FormatRgba([254.6, 0.4, 100, .5]));

    [Fact]
    public void ChannelsRoundTrip()
    {
        var color = new PrestoColor(1, 2, 3, .5);
        Assert.Equal(new double[] { 1, 2, 3, .5 }, color.ToChannels());
        Assert.Equal(color, PrestoColor.FromChannels(color.ToChannels()));
    }
}
