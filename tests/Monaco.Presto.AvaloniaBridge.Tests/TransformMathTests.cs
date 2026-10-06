using Avalonia;
using Monaco.Presto.AvaloniaBridge.Gestures;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary><see cref="TransformMath"/> (GESTURES-PLAN §4): focal zoom and rotate, the scale rubber band and pan bounds.</summary>
public sealed class TransformMathTests
{
    [Fact]
    public void FocalUpdateKeepsTheContentPointUnderTheFocalPointFixed()
    {
        var random = new Random(35);
        for (var i = 0; i < 500; i++)
        {
            var layout = new Point(Next(random, -200, 200), Next(random, -200, 200));
            var origin = new Point(Next(random, 0, 300), Next(random, 0, 300));
            var t = new Vector(Next(random, -500, 500), Next(random, -500, 500));
            var s = Next(random, 0.2, 5);
            var theta = Next(random, -360, 360);
            var focal = new Point(Next(random, -400, 400), Next(random, -400, 400));
            var s2 = Next(random, 0.2, 5);
            var theta2 = Next(random, -360, 360);

            var q = origin + Rotate(focal - layout - origin - t, -theta) / s;
            var t2 = TransformMath.FocalTranslation(layout, origin, t, s, theta, focal, s2, theta2);
            var mapped = layout + origin + t2 + Rotate(q - origin, theta2) * s2;

            Assert.Equal(focal.X, mapped.X, 1e-6);
            Assert.Equal(focal.Y, mapped.Y, 1e-6);
        }
    }

    [Fact]
    public void ZoomThenUnzoomAboutTheSamePointReturnsTheStartTranslation()
    {
        var layout = new Point(12, 34);
        var origin = new Point(50, 25);
        var start = new Vector(-17.5, 42.25);
        var focal = new Point(80, 60);

        var zoomed = TransformMath.FocalTranslation(layout, origin, start, 1.3, 20, focal, 2.9, 65);
        var back = TransformMath.FocalTranslation(layout, origin, zoomed, 2.9, 65, focal, 1.3, 20);

        Assert.Equal(start.X, back.X, 1e-9);
        Assert.Equal(start.Y, back.Y, 1e-9);
    }

    [Fact]
    public void ContainCentersContentSmallerThanTheContainer()
    {
        var range = TransformMath.PanRange(PanBounds.Contain, new Rect(0, 0, 400, 300), default, new Size(100, 50),
            new Point(50, 25), 1, 0);

        Assert.Equal(new Rect(150, 125, 0, 0), range);
    }

    [Fact]
    public void ContainKeepsLargerContentCoveringTheContainer()
    {
        var range = TransformMath.PanRange(PanBounds.Contain, new Rect(0, 0, 100, 100), default, new Size(100, 100),
            new Point(50, 50), 2, 0);

        Assert.Equal(new Rect(-50, -50, 100, 100), range);
    }

    [Fact]
    public void InsideKeepsContentWithinTheContainer()
    {
        var container = new Rect(10, 20, 400, 300);
        var layout = new Point(10, 20);
        var size = new Size(100, 50);
        var range = TransformMath.PanRange(PanBounds.Inside, container, layout, size, default, 1.5, 0);

        Assert.Equal(new Rect(0, 0, 250, 225), range);
        foreach (var corner in new[] { range.TopLeft, range.BottomRight, new Point(9999, -9999) })
        {
            var t = TransformMath.Clamp(corner, range);
            var content = new Rect(layout.X + t.X, layout.Y + t.Y, size.Width * 1.5, size.Height * 1.5);
            Assert.True(container.Contains(content), $"{content} escapes {container}");
        }
    }

    [Fact]
    public void RotatedBoundsUseTheAxisAlignedBox()
    {
        var container = new Rect(0, 0, 400, 300);
        var rotated = TransformMath.PanRange(PanBounds.Inside, container, default, new Size(100, 50), new Point(50, 25), 1, 90);
        var upright = TransformMath.PanRange(PanBounds.Inside, container, default, new Size(50, 100), new Point(25, 50), 1, 0);

        Assert.Equal(upright.X - 25, rotated.X, 1e-9);
        Assert.Equal(upright.Y + 25, rotated.Y, 1e-9);
        Assert.Equal(upright.Width, rotated.Width, 1e-9);
        Assert.Equal(upright.Height, rotated.Height, 1e-9);
    }

    [Fact]
    public void FreeIsUnbounded()
    {
        var range = TransformMath.PanRange(PanBounds.Free, new Rect(0, 0, 100, 100), default, new Size(10, 10), default, 1, 0);

        Assert.Equal(new Vector(-1e9, 1e9), TransformMath.Clamp(new Vector(-1e9, 1e9), range));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2.5)]
    [InlineData(4)]
    public void RubberBandIsTheIdentityInsideTheLimits(double scale) =>
        Assert.Equal(scale, TransformMath.RubberBand(scale, 1, 4, 0.35));

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void RubberBandIsContinuousAtTheLimits(double limit)
    {
        Assert.Equal(limit, TransformMath.RubberBand(limit * (1 + 1e-9), 1, 4, 0.35), 1e-8);
        Assert.Equal(limit, TransformMath.RubberBand(limit * (1 - 1e-9), 1, 4, 0.35), 1e-8);
    }

    [Fact]
    public void RubberBandDampsTheExcessBeyondTheLimits()
    {
        var over = TransformMath.RubberBand(8, 1, 4, 0.35);
        var under = TransformMath.RubberBand(0.5, 1, 4, 0.35);

        Assert.InRange(over, 4.0001, 7.9999);
        Assert.InRange(under, 0.5001, 0.9999);
    }

    private static double Next(Random random, double min, double max) => min + random.NextDouble() * (max - min);

    private static Vector Rotate(Vector v, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Vector(v.X * Math.Cos(radians) - v.Y * Math.Sin(radians), v.X * Math.Sin(radians) + v.Y * Math.Cos(radians));
    }
}
