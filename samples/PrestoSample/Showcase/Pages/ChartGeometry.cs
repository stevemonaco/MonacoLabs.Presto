using Avalonia;
using Avalonia.Media;

namespace PrestoSample.Showcase.Pages;

/// <summary>
/// The shapes a chart point morphs between. Every closed contour starts at the middle of its top (or outer) edge and runs
/// clockwise, because <c>MorphTo</c> pairs points by distance along the contour: matching starts and windings carry each
/// edge onto its counterpart instead of twisting the shape through itself.
/// </summary>
public static class ChartGeometry
{
    /// <summary>A bar with rounded top corners, centred on <paramref name="centreX"/> and standing on <paramref name="bottom"/>.</summary>
    public static Geometry Bar(double centreX, double top, double bottom, double width, double radius)
    {
        var left = centreX - (width / 2);
        var right = centreX + (width / 2);
        var r = Math.Min(radius, Math.Min(width / 2, bottom - top));
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(centreX, top), true);
            context.LineTo(new Point(right - r, top));
            context.ArcTo(new Point(right, top + r), new Size(r, r), 0, false, SweepDirection.Clockwise);
            context.LineTo(new Point(right, bottom));
            context.LineTo(new Point(left, bottom));
            context.LineTo(new Point(left, top + r));
            context.ArcTo(new Point(left + r, top), new Size(r, r), 0, false, SweepDirection.Clockwise);
            context.LineTo(new Point(centreX, top));
            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>An annular wedge; angles are in degrees, clockwise from twelve o'clock.</summary>
    public static Geometry Wedge(Point centre, double inner, double outer, double start, double sweep)
    {
        var mid = start + (sweep / 2);
        var end = start + sweep;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(At(centre, outer, mid), true);
            context.ArcTo(At(centre, outer, end), new Size(outer, outer), 0, false, SweepDirection.Clockwise);
            context.LineTo(At(centre, inner, end));
            context.ArcTo(At(centre, inner, start), new Size(inner, inner), 0, sweep > 180, SweepDirection.CounterClockwise);
            context.LineTo(At(centre, outer, start));
            context.ArcTo(At(centre, outer, mid), new Size(outer, outer), 0, false, SweepDirection.Clockwise);
            context.EndFigure(true);
        }
        return geometry;
    }

    public static Geometry Circle(Point centre, double radius)
    {
        var top = new Point(centre.X, centre.Y - radius);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(top, true);
            context.ArcTo(new Point(centre.X, centre.Y + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            context.ArcTo(top, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>The open polyline through <paramref name="points"/>, for a stroke.</summary>
    public static Geometry Line(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], false);
            for (var i = 1; i < points.Count; i++) context.LineTo(points[i]);
            context.EndFigure(false);
        }
        return geometry;
    }

    /// <summary>Each value's start angle and sweep, in degrees clockwise from twelve o'clock, splitting the full turn in proportion.</summary>
    public static (double Start, double Sweep)[] Sweeps(IReadOnlyList<double> values)
    {
        var total = values.Sum();
        var sweeps = new (double Start, double Sweep)[values.Count];
        var start = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            var sweep = values[i] / total * 360;
            sweeps[i] = (start, sweep);
            start += sweep;
        }
        return sweeps;
    }

    private static Point At(Point centre, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(centre.X + (radius * Math.Sin(radians)), centre.Y - (radius * Math.Cos(radians)));
    }
}
