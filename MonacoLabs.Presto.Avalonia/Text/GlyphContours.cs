using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;

namespace MonacoLabs.Presto.Text;

/// <summary>
/// An outline as closed figures flattened into points at most <see cref="MaxSegment"/> apart, straight edges included,
/// each classified as an outer contour or a hole.
/// </summary>
internal sealed class GlyphContours
{
    /// <summary>The longest segment flattening leaves, in DIPs.</summary>
    internal const double MaxSegment = 3;

    private GlyphContours(Contour[] contours)
    {
        Contours = contours;
        foreach (var contour in contours) LongestLength = Math.Max(LongestLength, contour.Length);
    }

    /// <summary>The figures in path order.</summary>
    public Contour[] Contours { get; }

    /// <summary>The perimeter of the longest figure.</summary>
    public double LongestLength { get; }

    /// <summary>Flattens every figure of <paramref name="path"/>, closing open ones as a fill does.</summary>
    public static GlyphContours From(SKPath path)
    {
        var contours = new List<Contour>();
        var points = new List<Point>();
        var p = new SKPoint[4];
        using var iterator = path.CreateRawIterator();
        SKPathVerb verb;
        while ((verb = iterator.Next(p)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move:
                    Close();
                    points.Add(new Point(p[0].X, p[0].Y));
                    break;
                case SKPathVerb.Line:
                    AddLine(points, new Point(p[1].X, p[1].Y));
                    break;
                case SKPathVerb.Quad:
                    AddCurve(points, p, 2, 1);
                    break;
                case SKPathVerb.Conic:
                    AddCurve(points, p, 2, iterator.ConicWeight());
                    break;
                case SKPathVerb.Cubic:
                    AddCurve(points, p, 3, 1);
                    break;
                case SKPathVerb.Close:
                    Close();
                    break;
            }
        }
        Close();
        Classify(contours);
        return new GlyphContours([.. contours]);

        void Close()
        {
            if (points.Count > 0 && Point.Distance(points[^1], points[0]) < 1e-9) points.RemoveAt(points.Count - 1);
            if (points.Count >= 3)
            {
                AddLine(points, points[0]);
                points.RemoveAt(points.Count - 1);
                contours.Add(new Contour([.. points]));
            }
            points.Clear();
        }
    }

    /// <summary>Flattens every figure of <paramref name="geometry"/>, or returns null when Skia does not draw it.</summary>
    public static GlyphContours? From(Geometry geometry) => PathOf(geometry) is { } path ? From(path) : null;

    /// <summary>The path Avalonia.Skia fills <paramref name="geometry"/> with, or null when Skia does not draw it.</summary>
    public static SKPath? PathOf(Geometry geometry)
    {
        var impl = PlatformImpl(geometry);
        return impl?.GetType().GetProperty("FillPath")?.GetValue(impl) as SKPath;
    }

    // A Geometry hides its figures; Avalonia.Skia keeps them on its internal implementation as an SKPath.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_PlatformImpl")]
    private static extern IGeometryImpl? PlatformImpl(Geometry geometry);

    // Containment decides the nesting and winding decides the kind, so a font of either winding convention classifies
    // alike, and a contour overlapping its container with the same winding stays the container's kind.
    private static void Classify(List<Contour> contours)
    {
        var bySize = contours.OrderByDescending(c => Math.Abs(c.SignedArea)).ToArray();
        for (var i = 0; i < bySize.Length; i++)
        {
            for (var j = i - 1; j >= 0; j--)
            {
                if (!bySize[j].Contains(bySize[i])) continue;
                bySize[i].Parent = bySize[j];
                bySize[i].IsHole = Math.Sign(bySize[i].SignedArea) == Math.Sign(bySize[j].SignedArea) ? bySize[j].IsHole : !bySize[j].IsHole;
                break;
            }
        }
    }

    private static void AddLine(List<Point> points, Point to)
    {
        var from = points[^1];
        var steps = Math.Max(1, (int)Math.Ceiling(Point.Distance(from, to) / MaxSegment));
        for (var k = 1; k <= steps; k++) points.Add(from + (to - from) * ((double)k / steps));
    }

    // Steps by the control polygon's length, an upper bound of the arc's; AddLine caps any chord that still comes out long.
    private static void AddCurve(List<Point> points, SKPoint[] p, int degree, float weight)
    {
        var length = 0d;
        for (var i = 0; i < degree; i++) length += SKPoint.Distance(p[i], p[i + 1]);
        var steps = Math.Max(1, (int)Math.Ceiling(length / MaxSegment));
        for (var k = 1; k <= steps; k++)
        {
            var t = (double)k / steps;
            var u = 1 - t;
            if (degree == 3)
            {
                var (a, b, c, d) = (u * u * u, 3 * u * u * t, 3 * u * t * t, t * t * t);
                AddLine(points, new Point(a * p[0].X + b * p[1].X + c * p[2].X + d * p[3].X, a * p[0].Y + b * p[1].Y + c * p[2].Y + d * p[3].Y));
            }
            else
            {
                var (a, b, c) = (u * u, 2 * weight * u * t, t * t);
                var w = a + b + c;
                AddLine(points, new Point((a * p[0].X + b * p[1].X + c * p[2].X) / w, (a * p[0].Y + b * p[1].Y + c * p[2].Y) / w));
            }
        }
    }
}

/// <summary>One closed figure of <see cref="GlyphContours"/>; its last point joins its first.</summary>
internal sealed class Contour
{
    public Contour(Point[] points)
    {
        Points = points;
        double area = 0, cx = 0, cy = 0, left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
        for (var i = 0; i < points.Length; i++)
        {
            var (a, b) = (points[i], points[(i + 1) % points.Length]);
            var cross = a.X * b.Y - b.X * a.Y;
            area += cross;
            Length += Point.Distance(a, b);
            cx += (a.X + b.X) * cross;
            cy += (a.Y + b.Y) * cross;
            (left, top, right, bottom) = (Math.Min(left, a.X), Math.Min(top, a.Y), Math.Max(right, a.X), Math.Max(bottom, a.Y));
        }
        SignedArea = area / 2;
        Bounds = new Rect(left, top, right - left, bottom - top);
        Centroid = Math.Abs(area) > 1e-9 ? new Point(cx / (3 * area), cy / (3 * area)) : Bounds.Center;
    }

    public Point[] Points { get; }

    /// <summary>Positive when the figure runs clockwise on screen (y down).</summary>
    public double SignedArea { get; }

    public Rect Bounds { get; }

    public Point Centroid { get; }

    public double Length { get; }

    public bool IsHole { get; set; }

    /// <summary>The smallest figure containing this one.</summary>
    public Contour? Parent { get; set; }

    /// <summary>Whether <paramref name="point"/> is inside the figure, by the even-odd rule.</summary>
    public bool Contains(Point point)
    {
        var inside = false;
        for (int i = 0, j = Points.Length - 1; i < Points.Length; j = i++)
        {
            var (a, b) = (Points[i], Points[j]);
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    /// <summary>Whether <paramref name="other"/> lies inside this figure, assuming the two do not cross.</summary>
    public bool Contains(Contour other) => Bounds.Contains(other.Bounds) && Contains(other.Points[0]);

    /// <summary><paramref name="count"/> points evenly spaced by arc length around the figure, from its first point.</summary>
    public Point[] Sample(int count)
    {
        var samples = new Point[count];
        var (edge, walked) = (0, 0d);
        for (var i = 0; i < count; i++)
        {
            var distance = Length * i / count;
            var (from, to) = (Points[edge], Points[(edge + 1) % Points.Length]);
            var length = Point.Distance(from, to);
            while (walked + length < distance && edge < Points.Length - 1)
            {
                walked += length;
                edge++;
                (from, to) = (Points[edge], Points[(edge + 1) % Points.Length]);
                length = Point.Distance(from, to);
            }
            samples[i] = length > 0 ? from + (to - from) * Math.Min((distance - walked) / length, 1) : from;
        }
        return samples;
    }
}
