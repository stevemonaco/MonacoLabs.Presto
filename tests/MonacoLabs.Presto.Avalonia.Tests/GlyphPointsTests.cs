using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MonacoLabs.Presto.Text;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="GlyphPoints.Sample"/>: edge points spaced along every contour, fill points inside the outline only, the same for the same seed.</summary>
public sealed class GlyphPointsTests : BridgeTestBase
{
    private static readonly Typeface Font = new(FontFamily.Default);

    // A 100 square with a 40 square hole.
    private static Geometry Frame() => Geometry.Parse("M0,0 L100,0 L100,100 L0,100 Z M30,30 L70,30 L70,70 L30,70 Z");

    [AvaloniaFact]
    public void EdgePointsAreSpacedAlongEveryContour()
    {
        var points = GlyphPoints.Sample(Frame(), 5, GlyphSampling.Edge);

        Assert.Equal(80 + 32, points.Count);
        Assert.All(points, p => Assert.True(OnSquare(p, 0, 100) || OnSquare(p, 30, 70), $"{p} is on no contour"));

        var o = PrestoAvalonia.TextOutline("O", Font, 100);
        var contours = GlyphContours.From(o)!.Contours;
        var edge = GlyphPoints.Sample(o, 4, GlyphSampling.Edge);
        var start = 0;
        foreach (var contour in contours)
        {
            var count = (int)Math.Round(contour.Length / 4);
            for (var i = 0; i < count; i++)
            {
                var gap = Point.Distance(edge[start + i], edge[start + (i + 1) % count]);
                Assert.InRange(gap, 3.5, contour.Length / count + 1e-9);
            }
            start += count;
        }
        Assert.Equal(edge.Count, start);
    }

    [AvaloniaFact]
    public void FillPointsAreInsideTheOutlineAndNoneInsideHoles()
    {
        var points = GlyphPoints.Sample(Frame(), 5, GlyphSampling.Fill, seed: 1);

        Assert.All(points, p => Assert.True(p is { X: > 0 and < 100, Y: > 0 and < 100 } && !(p is { X: > 30 and < 70, Y: > 30 and < 70 }), $"{p} is outside the fill"));
        Assert.InRange(points.Count, 336 - 20, 336 + 20);

        var o = PrestoAvalonia.TextOutline("O", Font, 100);
        var contours = GlyphContours.From(o)!.Contours;
        var fill = GlyphPoints.Sample(o, 3, GlyphSampling.Fill);
        Assert.NotEmpty(fill);
        Assert.All(fill, p =>
        {
            Assert.Contains(contours, c => !c.IsHole && c.Contains(p));
            Assert.DoesNotContain(contours, c => c.IsHole && c.Contains(p));
        });
    }

    [AvaloniaFact]
    public void TheSameSeedGivesTheSamePointsAndAnotherSeedOthers()
    {
        var first = GlyphPoints.Sample(Frame(), 5, GlyphSampling.Fill, seed: 1);

        Assert.Equal(first, GlyphPoints.Sample(Frame(), 5, GlyphSampling.Fill, seed: 1));
        Assert.NotEqual(first, GlyphPoints.Sample(Frame(), 5, GlyphSampling.Fill, seed: 2));
        Assert.Equal(112 + first.Count, GlyphPoints.Sample(Frame(), 5, GlyphSampling.Edge | GlyphSampling.Fill, seed: 1).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => GlyphPoints.Sample(Frame(), 0, GlyphSampling.Fill));
    }

    private static bool OnSquare(Point p, double min, double max) =>
        p.X >= min - 1e-6 && p.X <= max + 1e-6 && p.Y >= min - 1e-6 && p.Y <= max + 1e-6 &&
        (Math.Abs(p.X - min) < 1e-6 || Math.Abs(p.X - max) < 1e-6 || Math.Abs(p.Y - min) < 1e-6 || Math.Abs(p.Y - max) < 1e-6);
}
