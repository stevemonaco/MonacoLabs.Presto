using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MonacoLabs.Presto.Text;
using SkiaSharp;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="GlyphShards.Of"/>: shards tile the outline, are closed, about as many as asked, and the same for the same seed.</summary>
public sealed class GlyphShardsTests : BridgeTestBase
{
    private static readonly Typeface Font = new(FontFamily.Default);

    private static Geometry Outline(string text) => PrestoAvalonia.TextOutline(text, Font, 100);

    private static double Area(Geometry geometry) => GlyphContours.From(geometry)!.Contours.Sum(c => c.SignedArea);

    [AvaloniaTheory]
    [InlineData("O")]
    [InlineData("B")]
    public void ShardAreasSumToTheOutlineArea(string text)
    {
        var outline = Outline(text);
        var contours = GlyphContours.From(outline)!.Contours;
        Assert.Contains(contours, c => c.IsHole);
        var expected = contours.Sum(c => (c.IsHole ? -1 : 1) * Math.Abs(c.SignedArea));

        var shards = GlyphShards.Of(outline, 8, seed: 1);

        Assert.All(shards, s => Assert.True(Area(s) > 0));
        Assert.Equal(expected, shards.Sum(Area), expected * 1e-6);
    }

    [AvaloniaFact]
    public void EveryShardIsClosed()
    {
        foreach (var shard in GlyphShards.Of(Outline("B"), 8, seed: 1))
        {
            using var iterator = GlyphContours.PathOf(shard)!.CreateRawIterator();
            var points = new SKPoint[4];
            var (open, figures) = (false, 0);
            SKPathVerb verb;
            while ((verb = iterator.Next(points)) != SKPathVerb.Done)
            {
                if (verb == SKPathVerb.Move)
                {
                    Assert.False(open);
                    (open, figures) = (true, figures + 1);
                }
                else if (verb == SKPathVerb.Close) open = false;
            }
            Assert.False(open);
            Assert.True(figures > 0);
        }
    }

    [AvaloniaFact]
    public void TheCountIsHonoredExceptThatSeparatePartsNeverMerge()
    {
        Assert.Equal(8, GlyphShards.Of(Outline("O"), 8, seed: 1).Count);
        Assert.Equal(5, GlyphShards.Of(Outline("B"), 5, seed: 1).Count);
        Assert.Equal(2, GlyphShards.Of(Outline("i"), 1, seed: 1).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => GlyphShards.Of(Outline("O"), 0, seed: 1));
    }

    [AvaloniaFact]
    public void TheSameSeedGivesTheSameShardsAndAnotherSeedOthers()
    {
        var outline = Outline("B");
        Rect[] Cut(int seed) => GlyphShards.Of(outline, 8, seed).Select(s => s.Bounds).ToArray();

        Assert.Equal(Cut(1), Cut(1));
        Assert.NotEqual(Cut(1), Cut(2));
    }
}
