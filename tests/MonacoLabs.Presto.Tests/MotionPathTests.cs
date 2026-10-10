namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="MotionPath"/>, after GSAP MotionPath's <c>curviness</c>; there is no anime.js upstream.</summary>
public sealed class MotionPathTests : EngineTestBase
{
    private static readonly (double X, double Y)[] Zigzag = [(0, 0), (100, 50), (200, 0), (300, 80), (400, 20)];

    private sealed class Rider
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Rotate { get; set; }
    }

    private static (double X, double Y) PointAt(MotionPath path, double distance)
    {
        path.Sample(distance, out var x, out var y, out _);
        return (x, y);
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    public void TheCurvePassesThroughEveryPoint(double curviness, bool closed)
    {
        var path = MotionPath.Through(Zigzag, curviness, closed);

        for (var i = 0; i < Zigzag.Length; i++) Assert.Equal(Zigzag[i], path.BezierPoints[i * 3]);
    }

    [Fact]
    public void CurvinessZeroIsThePolyline()
    {
        var path = MotionPath.Through(Zigzag, 0);
        var length = 0d;
        for (var i = 1; i < Zigzag.Length; i++) length += Distance(Zigzag[i - 1], Zigzag[i]);

        Assert.Equal(length, path.Length, 1e-9);
        var midpoint = PointAt(path, Distance(Zigzag[0], Zigzag[1]) / 2);
        Assert.Equal(50, midpoint.X, 1e-6);
        Assert.Equal(25, midpoint.Y, 1e-6);
    }

    [Fact]
    public void AClosedPathsEndTangentMeetsItsStart()
    {
        var path = MotionPath.Through(Zigzag, 1, closed: true);
        path.Sample(0, out var startX, out var startY, out var startAngle);
        path.Sample(path.Length, out var endX, out var endY, out var endAngle);

        Assert.Equal(startX, endX, 1e-9);
        Assert.Equal(startY, endY, 1e-9);
        Assert.Equal(startAngle, endAngle, 1e-6);
    }

    [Fact]
    public void EqualDistanceStepsGiveEqualChords()
    {
        var path = MotionPath.Through(Zigzag, 1.5);
        const int steps = 200;
        var step = path.Length / steps;
        var previous = PointAt(path, 0);
        for (var i = 1; i <= steps; i++)
        {
            var point = PointAt(path, i * step);
            Assert.Equal(step, Distance(previous, point), step * 0.02);
            previous = point;
        }
    }

    [Fact]
    public void AHorizontalSegmentsTangentIsZeroAndAutoRotateAddsToIt()
    {
        var path = MotionPath.Through([(0, 10), (100, 10)]);
        path.Sample(50, out _, out _, out var angle);
        var tweens = path.Tweens(rotateOffset: 90);

        Assert.Equal(0, angle, 1e-9);
        Assert.Equal(90, tweens.Rotate.Modifier!(50), 1e-9);
    }

    [Fact]
    public void ZeroOffsetClampsAndAnyOtherWraps()
    {
        var path = MotionPath.Through(Zigzag);
        var clamped = path.Tweens();
        var wrapped = path.Tweens(.5);

        Assert.Equal(Zigzag[0].X, clamped.X.Modifier!(-50), 1e-9);
        Assert.Equal(Zigzag[^1].X, clamped.X.Modifier!(path.Length + 50), 1e-9);
        var middle = PointAt(path, path.Length / 2);
        Assert.Equal(middle.X, wrapped.X.Modifier!(0), 1e-9);
        Assert.Equal(middle.Y, wrapped.Y.Modifier!(path.Length), 1e-9);
        Assert.Equal(PointAt(path, path.Length / 4).X, wrapped.X.Modifier!(path.Length * .75), 1e-9);
    }

    [Fact]
    public void AnAnimationThroughTheTweensLandsOnTheLastPoint()
    {
        var rider = new Rider();
        var motion = MotionPath.Through(Zigzag).Tweens();
        var animation = Presto.Animate(rider, new()
        {
            ["X"] = motion.X,
            ["Y"] = motion.Y,
            ["Rotate"] = motion.Rotate,
            Duration = 500,
        });

        RunUntil(() => animation.Completed);

        Assert.True(animation.Completed);
        Assert.Equal(Zigzag[^1].X, rider.X, 1e-3);
        Assert.Equal(Zigzag[^1].Y, rider.Y, 1e-3);
    }
}
