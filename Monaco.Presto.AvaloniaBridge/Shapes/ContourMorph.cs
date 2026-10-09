using System.Text;
using Avalonia;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Text;
using Monaco.Presto.AvaloniaBridge.Values;

namespace Monaco.Presto.AvaloniaBridge.Shapes;

/// <summary>
/// <see cref="PrestoAvalonia.MorphTo"/> between geometries with several figures: pairs their contours outer with outer and
/// hole with hole, and samples each pair into two closed polylines of one point count.
/// </summary>
internal static class ContourMorph
{
    /// <summary>
    /// The path data of both ends, one <c>M…Z</c> figure per contour pair, and their total point count; or null when neither
    /// geometry has more than one figure, or Skia does not draw one of them.
    /// </summary>
    public static (string From, string To, int Points)? Of(Geometry from, Geometry to, double precision)
    {
        if (GlyphContours.From(from)?.Contours is not { } a || GlyphContours.From(to)?.Contours is not { } b) return null;
        if (a.Length < 2 && b.Length < 2) return null;
        var (boundsA, boundsB) = (BoundsOf(a), BoundsOf(b));
        var (fromData, toData) = (new StringBuilder(), new StringBuilder());
        var (outersA, outersB) = (Outers(a), Outers(b));
        var total = 0;
        foreach (var (outerA, outerB) in Match(outersA, boundsA, outersB, boundsB))
        {
            // A contour without a partner collapses to, or grows from, the centroid of the contour its group morphs with.
            var anchorA = outerA?.Centroid ?? Nearest(outerB!, boundsB, outersA, boundsA);
            var anchorB = outerB?.Centroid ?? Nearest(outerA!, boundsA, outersB, boundsB);
            Add(outerA, anchorA, outerB, anchorB);
            foreach (var (holeA, holeB) in Match(HolesOf(outerA, a), boundsA, HolesOf(outerB, b), boundsB)) Add(holeA, anchorA, holeB, anchorB);
        }
        return (fromData.ToString(), toData.ToString(), total);

        void Add(Contour? source, Point sourceAnchor, Contour? destination, Point destinationAnchor)
        {
            var count = GeometryCodec.PointCount(Math.Max(source?.Length ?? 0, destination?.Length ?? 0), precision);
            var sourcePoints = Sample(source, sourceAnchor, count);
            Append(fromData, sourcePoints);
            Append(toData, Align(sourcePoints, Sample(destination, destinationAnchor, count)));
            total += count;
        }
    }

    private static List<Contour> Outers(Contour[] contours) => contours.Where(c => !c.IsHole).ToList();

    private static List<Contour> HolesOf(Contour? outer, Contour[] contours) =>
        outer == null ? [] : contours.Where(c => c.IsHole && OuterOf(c) == outer).ToList();

    private static Contour OuterOf(Contour hole)
    {
        while (hole.IsHole) hole = hole.Parent!;
        return hole;
    }

    private static Rect BoundsOf(Contour[] contours) => contours.Length == 0 ? default : contours.Skip(1).Aggregate(contours[0].Bounds, (r, c) => r.Union(c.Bounds));

    private static double Distance(Contour a, Rect boundsA, Contour b, Rect boundsB)
    {
        var (p, q) = (Normalize(a.Centroid, boundsA), Normalize(b.Centroid, boundsB));
        return (p - q).SquaredLength;
    }

    private static Vector Normalize(Point point, Rect bounds) =>
        new((point.X - bounds.X) / Math.Max(bounds.Width, 1), (point.Y - bounds.Y) / Math.Max(bounds.Height, 1));

    private static Point Nearest(Contour contour, Rect bounds, List<Contour> others, Rect othersBounds) =>
        others.Count == 0 ? contour.Centroid : others.MinBy(o => Distance(contour, bounds, o, othersBounds))!.Centroid;

    // Greedy, nearest pair first, which for glyph-sized contour counts matches the optimal assignment in practice.
    private static List<(Contour? A, Contour? B)> Match(List<Contour> a, Rect boundsA, List<Contour> b, Rect boundsB)
    {
        var candidates = new List<(double Distance, Contour A, Contour B)>();
        foreach (var x in a)
        {
            foreach (var y in b) candidates.Add((Distance(x, boundsA, y, boundsB), x, y));
        }
        candidates.Sort((x, y) => x.Distance.CompareTo(y.Distance));
        var pairs = new List<(Contour?, Contour?)>();
        var paired = new HashSet<Contour>();
        foreach (var (_, x, y) in candidates)
        {
            if (paired.Contains(x) || paired.Contains(y)) continue;
            pairs.Add((x, y));
            paired.Add(x);
            paired.Add(y);
        }
        pairs.AddRange(a.Where(x => !paired.Contains(x)).Select(x => ((Contour?)x, (Contour?)null)));
        pairs.AddRange(b.Where(y => !paired.Contains(y)).Select(y => ((Contour?)null, (Contour?)y)));
        return pairs;
    }

    private static Point[] Sample(Contour? contour, Point anchor, int count)
    {
        if (contour != null) return contour.Sample(count);
        var samples = new Point[count];
        Array.Fill(samples, anchor);
        return samples;
    }

    private static Point[] Align(Point[] from, Point[] to)
    {
        var n = to.Length;
        var (best, bestShift, bestStep) = (double.MaxValue, 0, 1);
        foreach (var step in (ReadOnlySpan<int>)[1, -1])
        {
            for (var shift = 0; shift < n; shift++)
            {
                var travel = 0d;
                for (var i = 0; i < n && travel < best; i++) travel += ((Vector)(from[i] - to[(shift + step * i + n) % n])).SquaredLength;
                if (travel < best) (best, bestShift, bestStep) = (travel, shift, step);
            }
        }
        var aligned = new Point[n];
        for (var i = 0; i < n; i++) aligned[i] = to[(bestShift + bestStep * i + n) % n];
        return aligned;
    }

    private static void Append(StringBuilder data, Point[] points)
    {
        for (var i = 0; i < points.Length; i++) GeometryCodec.AppendPoint(data, i == 0 ? 'M' : 'L', points[i]);
        data.Append('Z');
    }
}
