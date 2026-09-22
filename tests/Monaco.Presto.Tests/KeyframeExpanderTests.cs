using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

/// <summary>Port of the generateKeyframes half of anime.js keyframes.test.js; the tween-level expectations follow in the Animation item.</summary>
public sealed class KeyframeExpanderTests
{
    private static IReadOnlyList<KeyValuePair<string, TweenValue>> Expand(AnimationParams parameters) =>
        KeyframeExpander.Expand(parameters.Keyframes!, parameters);

    private static TweenParams[] Entries(IReadOnlyList<KeyValuePair<string, TweenValue>> properties, int index, string name)
    {
        Assert.Equal(name, properties[index].Key);
        return Array.ConvertAll(Assert.IsType<TweenValue[]>(properties[index].Value.Raw), v => Assert.IsType<TweenParams>(v.Raw));
    }

    private static object?[] Raw(IEnumerable<TweenValue?> values) => values.Select(v => v?.Raw).ToArray();

    [Fact]
    public void PercentageKeyframesSortByOffsetAndShiftEasesToTheFollowingSegment()
    {
        var parameters = new AnimationParams
        {
            Keyframes = new Dictionary<string, Keyframe>
            {
                ["100%"] = new() { ["x"] = 100, Ease = "outQuad" },
                ["0%"] = new() { ["x"] = 0, Ease = "inQuad" },
                ["50%"] = new() { ["x"] = 50, Ease = "linear" },
            },
            Duration = 1000,
        };

        var x = Entries(Expand(parameters), 0, "x");

        Assert.Equal([50.0, 100.0], Raw(x.Select(e => e.To)));
        Assert.Equal([500.0, 500.0], Raw(x.Select(e => e.Duration)));
        Assert.Equal("inQuad", x[0].Ease?.Raw);
        Assert.Equal("linear", x[1].Ease?.Raw);
    }

    [Fact]
    public void ParametersPropertiesFollowGeneratedOnesUnlessAlreadyGenerated()
    {
        var parameters = new AnimationParams
        {
            ["x"] = 999,
            ["opacity"] = 1,
            Keyframes = new Keyframe[] { new() { ["x"] = 10 }, new() { ["y"] = 20 } },
        };

        var properties = Expand(parameters);

        Assert.Equal(["x", "y", "opacity"], properties.Select(p => p.Key));
        Assert.Equal(2, Entries(properties, 0, "x").Length);
        Assert.Equal(1.0, properties[2].Value.Raw);
    }
}
