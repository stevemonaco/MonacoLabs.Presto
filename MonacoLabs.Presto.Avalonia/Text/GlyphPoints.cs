using Avalonia;
using Avalonia.Media;

namespace MonacoLabs.Presto.Text;

/// <summary>Samples points along or inside an outline, such as <see cref="PrestoAvalonia.GlyphOutline"/>'s, for particle text.</summary>
public static class GlyphPoints
{
    /// <summary>
    /// Samples <paramref name="outline"/>: with <see cref="GlyphSampling.Edge"/>, points evenly spaced about
    /// <paramref name="spacing"/> apart along each of its flattened figures; with <see cref="GlyphSampling.Fill"/>, one point
    /// per cell of a <paramref name="spacing"/> grid over its bounds, jittered by up to a quarter of a cell from
    /// <paramref name="seed"/>, kept where it is inside the outline by the even-odd rule, so holes stay empty.
    /// </summary>
    /// <returns>The points in the outline's coordinates, edge points first.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spacing"/> is not positive.</exception>
    /// <exception cref="ArgumentException">Avalonia.Skia does not draw <paramref name="outline"/>.</exception>
    public static IReadOnlyList<Point> Sample(Geometry outline, double spacing, GlyphSampling mode, int seed = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spacing);
        var contours = GlyphContours.From(outline) ?? throw new ArgumentException("The geometry is not drawn by Avalonia.Skia.", nameof(outline));
        var points = new List<Point>();
        if (mode.HasFlag(GlyphSampling.Edge))
        {
            foreach (var contour in contours.Contours) points.AddRange(contour.Sample(Math.Max(1, (int)Math.Round(contour.Length / spacing))));
        }
        if (mode.HasFlag(GlyphSampling.Fill) && contours.Contours.Length > 0) AddFill(contours.Contours, spacing, seed, points);
        return points;
    }

    private static void AddFill(Contour[] contours, double spacing, int seed, List<Point> points)
    {
        var bounds = contours[0].Bounds;
        foreach (var contour in contours) bounds = bounds.Union(contour.Bounds);
        var random = Utils.CreateSeededRandom(seed);
        var band = new List<(Point A, Point B)>();
        for (var y = bounds.Top + spacing / 2; y < bounds.Bottom; y += spacing)
        {
            // A jittered point stays within a quarter cell of its row, so only the edges crossing that band can flip it.
            var (low, high) = (y - spacing / 4, y + spacing / 4);
            band.Clear();
            foreach (var contour in contours)
            {
                if (contour.Bounds.Bottom < low || contour.Bounds.Top > high) continue;
                var ring = contour.Points;
                for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
                {
                    if (Math.Max(ring[i].Y, ring[j].Y) >= low && Math.Min(ring[i].Y, ring[j].Y) <= high) band.Add((ring[i], ring[j]));
                }
            }
            for (var x = bounds.Left + spacing / 2; x < bounds.Right; x += spacing)
            {
                var point = new Point(x + (random(0, 1, 9) - .5) * spacing / 2, y + (random(0, 1, 9) - .5) * spacing / 2);
                var inside = false;
                foreach (var (a, b) in band)
                {
                    if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
                }
                if (inside) points.Add(point);
            }
        }
    }
}
