namespace MonacoLabs.Presto;

/// <summary>
/// A Catmull-Rom spline through a list of points, after GSAP MotionPath's <c>curviness</c>, sampled at constant speed
/// along its arc length.
/// </summary>
public sealed class MotionPath
{
    private const int SamplesPerSegment = 64;

    private readonly (double X, double Y)[] _bezier;
    private readonly double[] _lengths;

    private MotionPath((double X, double Y)[] bezier)
    {
        _bezier = bezier;
        var segments = (bezier.Length - 1) / 3;
        _lengths = new double[segments * SamplesPerSegment + 1];
        var (px, py) = bezier[0];
        for (var i = 1; i < _lengths.Length; i++)
        {
            var segment = (i - 1) / SamplesPerSegment;
            var t = (i - segment * SamplesPerSegment) / (double)SamplesPerSegment;
            Evaluate(segment, t, out var x, out var y, out _, out _);
            _lengths[i] = _lengths[i - 1] + Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
            (px, py) = (x, y);
        }
    }

    /// <summary>The arc length of the whole path.</summary>
    public double Length => _lengths[^1];

    /// <summary>
    /// The cubic Bézier form of the path for drawing: its start point, then each segment's two control points and end point.
    /// </summary>
    public IReadOnlyList<(double X, double Y)> BezierPoints => _bezier;

    /// <summary>
    /// Builds the spline through <paramref name="points"/>: <paramref name="curviness"/> 0 is the straight polyline, 1 the
    /// standard Catmull-Rom curve, and larger values loosen it. A <paramref name="closed"/> path joins the last point back
    /// to the first smoothly.
    /// </summary>
    public static MotionPath Through(IReadOnlyList<(double X, double Y)> points, double curviness = 1, bool closed = false)
    {
        var count = points.Count;
        if (count < 2) throw new ArgumentException("A motion path needs at least two points.", nameof(points));
        var segments = closed ? count : count - 1;
        var bezier = new (double X, double Y)[segments * 3 + 1];
        bezier[0] = points[0];
        var k = curviness / 6;
        for (var i = 0; i < segments; i++)
        {
            var p0 = points[closed ? (i - 1 + count) % count : Math.Max(i - 1, 0)];
            var p1 = points[i];
            var p2 = points[(i + 1) % count];
            var p3 = points[closed ? (i + 2) % count : Math.Min(i + 2, count - 1)];
            bezier[i * 3 + 1] = (p1.X + (p2.X - p0.X) * k, p1.Y + (p2.Y - p0.Y) * k);
            bezier[i * 3 + 2] = (p2.X - (p3.X - p1.X) * k, p2.Y - (p3.Y - p1.Y) * k);
            bezier[i * 3 + 3] = p2;
        }

        return new MotionPath(bezier);
    }

    /// <summary>
    /// The point <paramref name="distance"/> along the path (clamped to its ends) and the tangent's angle there, in degrees
    /// clockwise from the positive X axis when Y points down.
    /// </summary>
    public void Sample(double distance, out double x, out double y, out double angle)
    {
        distance = Math.Clamp(distance, 0, Length);
        var index = Array.BinarySearch(_lengths, distance);
        if (index < 0) index = ~index - 1;
        index = Math.Clamp(index, 0, _lengths.Length - 2);
        var span = _lengths[index + 1] - _lengths[index];
        var fraction = span > 0 ? (distance - _lengths[index]) / span : 0;
        var segment = index / SamplesPerSegment;
        Evaluate(segment, (index - segment * SamplesPerSegment + fraction) / SamplesPerSegment, out x, out y, out var dx, out var dy);
        if (dx * dx + dy * dy < 1e-18)
        {
            // A zero-length control arm (curviness 0) has no derivative at the segment's ends; its chord has the direction.
            dx = _bezier[segment * 3 + 3].X - _bezier[segment * 3].X;
            dy = _bezier[segment * 3 + 3].Y - _bezier[segment * 3].Y;
        }

        angle = Math.Atan2(dy, dx) * 180 / Math.PI;
    }

    /// <summary>
    /// The "X", "Y" and "Rotate" tweens that ride the path, as <c>svg.createMotionPath</c>: <paramref name="offset"/> (0-1)
    /// shifts where along it the animation starts, clamping at the two ends when it is 0 and wrapping around otherwise, and
    /// <paramref name="rotateOffset"/> degrees are added to the tangent angle (GSAP's <c>autoRotate: 90</c>).
    /// </summary>
    public (TweenParams X, TweenParams Y, TweenParams Rotate) Tweens(double offset = 0, double rotateOffset = 0)
    {
        var length = Length;
        var distance = OffsetDistance(length, offset);
        return (
            new TweenParams { From = 0, To = length, Modifier = p => { Sample(distance(p), out var x, out _, out _); return x; } },
            new TweenParams { From = 0, To = length, Modifier = p => { Sample(distance(p), out _, out var y, out _); return y; } },
            new TweenParams { From = 0, To = length, Modifier = p => { Sample(distance(p), out _, out _, out var a); return a + rotateOffset; } });
    }

    internal static Func<double, double> OffsetDistance(double length, double offset)
    {
        var shift = offset * length;
        if (offset == 0) return progress => Math.Clamp(progress, 0, length);
        return progress => (((progress + shift) % length) + length) % length;
    }

    private void Evaluate(int segment, double t, out double x, out double y, out double dx, out double dy)
    {
        var (x0, y0) = _bezier[segment * 3];
        var (x1, y1) = _bezier[segment * 3 + 1];
        var (x2, y2) = _bezier[segment * 3 + 2];
        var (x3, y3) = _bezier[segment * 3 + 3];
        var u = 1 - t;
        x = u * u * u * x0 + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t * x3;
        y = u * u * u * y0 + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * y3;
        dx = 3 * (u * u * (x1 - x0) + 2 * u * t * (x2 - x1) + t * t * (x3 - x2));
        dy = 3 * (u * u * (y1 - y0) + 2 * u * t * (y2 - y1) + t * t * (y3 - y2));
    }
}
