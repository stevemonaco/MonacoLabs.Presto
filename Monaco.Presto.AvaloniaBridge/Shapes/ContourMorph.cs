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
            // An outer contour without a partner collapses to, or grows from, the centroid of the contour its group morphs with.
            var anchorA = outerA?.Centroid ?? Nearest(outerB!, boundsB, outersA, boundsA);
            var anchorB = outerB?.Centroid ?? Nearest(outerA!, boundsA, outersB, boundsB);
            Add(outerA, anchorA, outerB, anchorB, hole: false);
            var (holesA, holesB) = (HolesOf(outerA, a), HolesOf(outerB, b));
            foreach (var (holeA, holeB) in Match(holesA, boundsA, holesB, boundsB))
            {
                // A hole collapses into ink: inside a counter or outside the glyph, the shrinking hole would draw as ink.
                var inkA = holeA == null && outerA != null ? InkPoint(outerA, holesA, Map(holeB!.Centroid, boundsB, boundsA)) : anchorA;
                var inkB = holeB == null && outerB != null ? InkPoint(outerB, holesB, Map(holeA!.Centroid, boundsA, boundsB)) : anchorB;
                Add(holeA, inkA, holeB, inkB, hole: true);
            }
        }
        return (fromData.ToString(), toData.ToString(), total);

        void Add(Contour? source, Point sourceAnchor, Contour? destination, Point destinationAnchor, bool hole)
        {
            var count = GeometryCodec.PointCount(Math.Max(source?.Length ?? 0, destination?.Length ?? 0), precision);
            var sourcePoints = Wind(Sample(source, sourceAnchor, count), hole);
            Append(fromData, sourcePoints);
            Append(toData, Align(sourcePoints, Wind(Sample(destination, destinationAnchor, count), hole)));
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
        var (best, bestShift) = (double.MaxValue, 0);
        for (var shift = 0; shift < n; shift++)
        {
            var travel = 0d;
            for (var i = 0; i < n && travel < best; i++) travel += ((Vector)(from[i] - to[(shift + i) % n])).SquaredLength;
            if (travel < best) (best, bestShift) = (travel, shift);
        }
        var aligned = new Point[n];
        for (var i = 0; i < n; i++) aligned[i] = to[(bestShift + i) % n];
        return aligned;
    }

    // Nonzero fill needs one winding per role: outer contours clockwise on screen, holes counter-clockwise.
    private static Point[] Wind(Point[] points, bool hole)
    {
        var area = 0d;
        for (var i = 0; i < points.Length; i++)
        {
            var (a, b) = (points[i], points[(i + 1) % points.Length]);
            area += a.X * b.Y - b.X * a.Y;
        }
        if (area != 0 && area > 0 == hole) Array.Reverse(points);
        return points;
    }

    private static Point Map(Point point, Rect from, Rect to)
    {
        var normalized = Normalize(point, from);
        return new Point(to.X + normalized.X * to.Width, to.Y + normalized.Y * to.Height);
    }

    // The ink point nearest to near among those at least half as deep as the deepest, so a collapsing hole stays in ink.
    private static Point InkPoint(Contour outer, List<Contour> holes, Point near)
    {
        const int Grid = 24;
        var bounds = outer.Bounds;
        var candidates = new List<(Point Point, double Clearance)>();
        for (var row = 0; row < Grid; row++)
        {
            for (var column = 0; column < Grid; column++)
            {
                var point = new Point(bounds.X + (column + .5) * bounds.Width / Grid, bounds.Y + (row + .5) * bounds.Height / Grid);
                if (!outer.Contains(point) || holes.Any(h => h.Contains(point))) continue;
                var clearance = holes.Append(outer).Min(c => c.Points.Min(p => ((Vector)(p - point)).SquaredLength));
                candidates.Add((point, clearance));
            }
        }
        if (candidates.Count == 0) return outer.Centroid;
        var deepest = candidates.Max(c => c.Clearance);
        // Squared clearances: a quarter of the deepest is half its distance.
        return candidates.Where(c => c.Clearance >= deepest / 4).MinBy(c => ((Vector)(c.Point - near)).SquaredLength).Point;
    }

    private static void Append(StringBuilder data, Point[] points)
    {
        for (var i = 0; i < points.Length; i++) GeometryCodec.AppendPoint(data, i == 0 ? 'M' : 'L', points[i]);
        data.Append('Z');
    }
}
