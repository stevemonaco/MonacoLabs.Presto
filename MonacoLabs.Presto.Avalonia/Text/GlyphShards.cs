using Avalonia;
using Avalonia.Media;

namespace MonacoLabs.Presto.Text;

/// <summary>Cuts an outline, such as <see cref="PrestoAvalonia.GlyphOutline"/>'s, into shards that tile it, as <see cref="Recipes.Shatter"/> breaks text.</summary>
public static class GlyphShards
{
    /// <summary>
    /// Cuts <paramref name="outline"/> into about <paramref name="count"/> shards: its flattened figures are triangulated, holes
    /// bridged into their outer figure, and neighboring triangles merged, smallest first, each into a neighbor drawn from
    /// <paramref name="seed"/>, so the same seed gives the same shards.
    /// </summary>
    /// <returns>
    /// The shards in the outline's coordinates, each one or more closed figures with the nonzero fill rule; their areas sum
    /// to the outline's. A shard turns about its <see cref="Geometry.Bounds"/> center as <see cref="Recipes.Shatter"/> spins it.
    /// </returns>
    /// <remarks>Separate parts of an outline, such as the dot of an <c>i</c>, never merge, so an outline of many parts can give more than <paramref name="count"/>.</remarks>
    /// <exception cref="ArgumentException">Avalonia.Skia does not draw <paramref name="outline"/>.</exception>
    public static IReadOnlyList<Geometry> Of(Geometry outline, int count, int seed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        var contours = GlyphContours.From(outline) ?? throw new ArgumentException("The geometry is not drawn by Avalonia.Skia.", nameof(outline));
        var points = new List<Point>();
        var triangles = new List<(int A, int B, int C)>();
        foreach (var outer in contours.Contours.Where(c => !c.IsHole))
        {
            var ring = Ring(outer, points, positive: true);
            var holes = contours.Contours.Where(c => c.IsHole && c.Parent == outer).OrderByDescending(c => c.Bounds.Right).Select(c => Ring(c, points, positive: false)).ToList();
            while (holes.Count > 0)
            {
                var hole = holes[0];
                holes.RemoveAt(0);
                Bridge(ring, hole, holes, points);
            }
            Clip(ring, points, triangles);
        }
        return Merge(triangles, points, count, seed).ConvertAll(region => Boundary(region, triangles, points));
    }

    // Twice the signed area of (o, a, b): positive when o → a → b turns the way a positive-area figure runs.
    private static double Cross(Point o, Point a, Point b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

    // Drops the points flattening put along straight edges, which only add slivers, and orients the ring.
    private static List<int> Ring(Contour contour, List<Point> points, bool positive)
    {
        var source = contour.Points;
        var ring = new List<int>(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            var (before, at, after) = (source[(i + source.Length - 1) % source.Length], source[i], source[(i + 1) % source.Length]);
            if (Math.Abs(Cross(before, at, after)) < 1e-6 && Vector.Dot(at - before, after - at) > 0) continue;
            ring.Add(points.Count);
            points.Add(at);
        }
        if (contour.SignedArea > 0 != positive) ring.Reverse();
        return ring;
    }

    // Joins the hole to the ring along the shortest segment from its rightmost point that crosses no edge, walking the hole and back.
    private static void Bridge(List<int> ring, List<int> hole, List<List<int>> others, List<Point> points)
    {
        var m = 0;
        for (var i = 1; i < hole.Count; i++)
        {
            if (points[hole[i]].X > points[hole[m]].X) m = i;
        }
        var from = points[hole[m]];
        var order = Enumerable.Range(0, ring.Count).OrderBy(k => ((Vector)(points[ring[k]] - from)).SquaredLength).ToList();
        var at = order.FirstOrDefault(k => Opens(ring, k, from, points) && !Crosses(ring[k], hole[m], points, [ring, hole, .. others]), order[0]);
        var bridge = new List<int>(hole.Count + 2);
        for (var i = 0; i <= hole.Count; i++) bridge.Add(hole[(m + i) % hole.Count]);
        bridge.Add(ring[at]);
        ring.InsertRange(at + 1, bridge);
    }

    // Whether the segment from the ring's k-th point towards target leaves it into the figure, between its two edges.
    private static bool Opens(List<int> ring, int k, Point target, List<Point> points)
    {
        var (before, at, after) = (points[ring[(k + ring.Count - 1) % ring.Count]], points[ring[k]], points[ring[(k + 1) % ring.Count]]);
        var (toAfter, toTarget) = (Cross(at, after, target) >= 0, Cross(at, target, before) >= 0);
        return Cross(before, at, after) >= 0 ? toAfter && toTarget : toAfter || toTarget;
    }

    private static bool Crosses(int a, int b, List<Point> points, List<int>[] rings)
    {
        var (p, q) = (points[a], points[b]);
        foreach (var ring in rings)
        {
            for (var i = 0; i < ring.Count; i++)
            {
                var (c, d) = (ring[i], ring[(i + 1) % ring.Count]);
                if (c == a || c == b || d == a || d == b) continue;
                var (r, s) = (points[c], points[d]);
                if (Cross(p, q, r) * Cross(p, q, s) < 0 && Cross(r, s, p) * Cross(r, s, q) < 0) return true;
            }
        }
        return false;
    }

    // Ear clipping; after a full lap without an ear (a degenerate ring) it clips the next vertex anyway, so it always ends.
    private static void Clip(List<int> ring, List<Point> points, List<(int, int, int)> triangles)
    {
        var n = ring.Count;
        if (n < 3) return;
        var prev = new int[n];
        var next = new int[n];
        for (var i = 0; i < n; i++) (prev[i], next[i]) = ((i + n - 1) % n, (i + 1) % n);
        var (at, left, misses) = (0, n, 0);
        while (left > 3)
        {
            var (a, c) = (prev[at], next[at]);
            if (misses < left && !IsEar(a, at, c))
            {
                at = c;
                misses++;
                continue;
            }
            triangles.Add((ring[a], ring[at], ring[c]));
            (next[a], prev[c]) = (c, a);
            (at, left, misses) = (c, left - 1, 0);
        }
        triangles.Add((ring[prev[at]], ring[at], ring[next[at]]));

        bool IsEar(int a, int b, int c)
        {
            var (pa, pb, pc) = (points[ring[a]], points[ring[b]], points[ring[c]]);
            if (Cross(pa, pb, pc) < 0) return false;
            for (var p = next[c]; p != a; p = next[p])
            {
                var pp = points[ring[p]];
                if (pp == pa || pp == pb || pp == pc || Cross(points[ring[prev[p]]], pp, points[ring[next[p]]]) >= 0) continue;
                if (Cross(pa, pb, pp) >= 0 && Cross(pb, pc, pp) >= 0 && Cross(pc, pa, pp) >= 0) return false;
            }
            return true;
        }
    }

    // Each triangle starts as a region; the smallest region with a neighbor merges into a seeded pick of its neighbors.
    private static List<List<int>> Merge(List<(int A, int B, int C)> triangles, List<Point> points, int count, int seed)
    {
        var members = new List<int>?[triangles.Count];
        var neighbors = new HashSet<int>[triangles.Count];
        var areas = new double[triangles.Count];
        var edges = new Dictionary<(int, int), int>();
        for (var t = 0; t < triangles.Count; t++)
        {
            var (a, b, c) = triangles[t];
            members[t] = [t];
            neighbors[t] = [];
            areas[t] = Cross(points[a], points[b], points[c]);
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
            {
                var key = u < v ? (u, v) : (v, u);
                if (edges.TryAdd(key, t)) continue;
                neighbors[t].Add(edges[key]);
                neighbors[edges[key]].Add(t);
            }
        }

        var random = Utils.CreateSeededRandom(seed);
        for (var live = triangles.Count; live > count; live--)
        {
            var smallest = -1;
            for (var r = 0; r < members.Length; r++)
            {
                if (members[r] != null && neighbors[r].Count > 0 && (smallest < 0 || areas[r] < areas[smallest])) smallest = r;
            }
            if (smallest < 0) break;
            var options = neighbors[smallest].Order().ToArray();
            var into = options[(int)random(0, options.Length - 1)];
            members[into]!.AddRange(members[smallest]!);
            areas[into] += areas[smallest];
            foreach (var r in neighbors[smallest])
            {
                neighbors[r].Remove(smallest);
                if (r == into) continue;
                neighbors[r].Add(into);
                neighbors[into].Add(r);
            }
            members[smallest] = null;
        }
        return members.OfType<List<int>>().ToList();
    }

    // A region's outline is its triangles' edges whose reverse no other of its triangles has, chained into loops.
    private static Geometry Boundary(List<int> region, List<(int A, int B, int C)> triangles, List<Point> points)
    {
        var edges = new HashSet<(int, int)>();
        foreach (var t in region)
        {
            var (a, b, c) = triangles[t];
            edges.UnionWith([(a, b), (b, c), (c, a)]);
        }
        var outgoing = new Dictionary<int, Queue<int>>();
        foreach (var (u, v) in edges)
        {
            if (edges.Contains((v, u))) continue;
            if (!outgoing.TryGetValue(u, out var targets)) outgoing[u] = targets = new();
            targets.Enqueue(v);
        }

        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        foreach (var start in outgoing.Keys)
        {
            while (outgoing[start].Count > 0)
            {
                context.BeginFigure(points[start], true);
                var at = start;
                while (outgoing.TryGetValue(at, out var targets) && targets.TryDequeue(out at) && at != start) context.LineTo(points[at]);
                context.EndFigure(true);
            }
        }
        return geometry;
    }
}
