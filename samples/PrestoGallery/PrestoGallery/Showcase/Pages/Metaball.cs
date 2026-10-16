using Avalonia;

namespace PrestoGallery.Showcase.Pages;

/// <summary>The two-circle metaball neck: tangent points on each circle joined by bezier curves whose handles follow the tangents.</summary>
public static class Metaball
{
    private const double Spread = .5;
    private const double HandleSize = 2.4;

    /// <summary>
    /// The neck from circle 1 to circle 2 as one closed contour: <c>p1, h1, h3, p3</c> is a cubic to circle 2, a line
    /// runs to <c>p4</c>, and <c>p4, h4, h2, p2</c> is a cubic back to circle 1. Null when the circles are more than
    /// <paramref name="reach"/> apart or one contains the other.
    /// </summary>
    public static Point[]? Neck(Point c1, double r1, Point c2, double r2, double reach)
    {
        var d = Point.Distance(c1, c2);
        if (d > r1 + r2 + reach || d <= Math.Abs(r1 - r2)) return null;

        var (u1, u2) = d < r1 + r2
            ? (Math.Acos(((r1 * r1) + (d * d) - (r2 * r2)) / (2 * r1 * d)), Math.Acos(((r2 * r2) + (d * d) - (r1 * r1)) / (2 * r2 * d)))
            : (0, 0);
        // Thinning the spread to nothing at full reach makes the neck pinch off instead of vanishing at full width.
        var spread = Spread * Math.Clamp(1 - ((d - r1 - r2) / reach), 0, 1);
        var between = Math.Atan2(c2.Y - c1.Y, c2.X - c1.X);
        var maxSpread = Math.Acos((r1 - r2) / d);

        var angle1 = between + u1 + ((maxSpread - u1) * spread);
        var angle2 = between - u1 - ((maxSpread - u1) * spread);
        var angle3 = between + Math.PI - u2 - ((Math.PI - u2 - maxSpread) * spread);
        var angle4 = between - Math.PI + u2 + ((Math.PI - u2 - maxSpread) * spread);
        var p1 = At(c1, r1, angle1);
        var p2 = At(c1, r1, angle2);
        var p3 = At(c2, r2, angle3);
        var p4 = At(c2, r2, angle4);

        var handle = Math.Min(spread * HandleSize, Point.Distance(p1, p3) / (r1 + r2)) * Math.Min(1, d * 2 / (r1 + r2));
        return
        [
            p1, At(p1, r1 * handle, angle1 - (Math.PI / 2)), At(p3, r2 * handle, angle3 + (Math.PI / 2)), p3,
            p4, At(p4, r2 * handle, angle4 - (Math.PI / 2)), At(p2, r1 * handle, angle2 + (Math.PI / 2)), p2,
        ];
    }

    private static Point At(Point origin, double length, double angle) =>
        new(origin.X + (length * Math.Cos(angle)), origin.Y + (length * Math.Sin(angle)));
}
