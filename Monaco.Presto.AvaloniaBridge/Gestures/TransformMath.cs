using Avalonia;
using Monaco.Presto.Core;

namespace Monaco.Presto.AvaloniaBridge.Gestures;

/// <summary>
/// Pure geometry for <c>Transformable</c> (GESTURES-PLAN decisions 3–5). An element at layout offset <c>L</c> with
/// render-transform origin <c>o</c> maps content point <c>q</c> to <c>L + o + t + s·Rot(θ)·(q − o)</c> in parent space,
/// with θ in degrees clockwise as <c>RotateTransform</c>.
/// </summary>
internal static class TransformMath
{
    /// <summary>The translation that keeps the content point under <paramref name="focal"/> fixed while scale and rotation change.</summary>
    public static Vector FocalTranslation(Point layoutOffset, Point origin, Vector translation, double scale, double rotation,
        Point focal, double newScale, double newRotation)
    {
        var c = focal - layoutOffset - origin;
        return c - Rotate(c - translation, newRotation - rotation) * (newScale / scale);
    }

    /// <summary>Damps a scale beyond [<paramref name="min"/>, <paramref name="max"/>] by <paramref name="friction"/>, in log-scale space.</summary>
    public static double RubberBand(double scale, double min, double max, double friction) =>
        scale > max ? max * Math.Pow(scale / max, friction)
        : scale < min ? min * Math.Pow(scale / min, friction)
        : scale;

    /// <summary>Keeps <paramref name="friction"/> of a translation's excess beyond a range from <see cref="PanRange"/>.</summary>
    public static Vector RubberBand(Vector translation, Rect range, double friction) =>
        new(Cushion(translation.X, range.Left, range.Right, friction), Cushion(translation.Y, range.Top, range.Bottom, friction));

    /// <summary>
    /// The allowed translations, as a rect of (x, y), for an element of <paramref name="size"/> transformed by
    /// <paramref name="scale"/> and <paramref name="rotation"/> about <paramref name="origin"/>, using its axis-aligned box.
    /// </summary>
    /// <remarks>Under <see cref="PanBounds.Inside"/>, an axis where the content is larger than the container falls back to covering it.</remarks>
    public static Rect PanRange(PanBounds bounds, Rect container, Point layoutOffset, Size size, Point origin, double scale, double rotation)
    {
        if (bounds == PanBounds.Free)
            return new Rect(-Constants.MaxValue, -Constants.MaxValue, 2 * Constants.MaxValue, 2 * Constants.MaxValue);

        var radians = rotation * Math.PI / 180;
        var cos = Math.Cos(radians) * scale;
        var sin = Math.Sin(radians) * scale;
        double x0 = -origin.X, x1 = size.Width - origin.X, y0 = -origin.Y, y1 = size.Height - origin.Y;
        var left = Math.Min(cos * x0, cos * x1) + Math.Min(-sin * y0, -sin * y1);
        var right = Math.Max(cos * x0, cos * x1) + Math.Max(-sin * y0, -sin * y1);
        var top = Math.Min(sin * x0, sin * x1) + Math.Min(cos * y0, cos * y1);
        var bottom = Math.Max(sin * x0, sin * x1) + Math.Max(cos * y0, cos * y1);

        var baseX = layoutOffset.X + origin.X;
        var baseY = layoutOffset.Y + origin.Y;
        Axis(bounds, container.Left - baseX - left, container.Right - baseX - right, out var minX, out var maxX);
        Axis(bounds, container.Top - baseY - top, container.Bottom - baseY - bottom, out var minY, out var maxY);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>Clamps <paramref name="translation"/> into a range from <see cref="PanRange"/>.</summary>
    public static Vector Clamp(Vector translation, Rect range) =>
        new(Math.Clamp(translation.X, range.Left, range.Right), Math.Clamp(translation.Y, range.Top, range.Bottom));

    // alignStart puts the content's near edge on the container's; alignEnd its far edge on the container's.
    private static void Axis(PanBounds bounds, double alignStart, double alignEnd, out double min, out double max)
    {
        if (bounds == PanBounds.Contain && alignStart < alignEnd)
        {
            min = max = (alignStart + alignEnd) / 2;
            return;
        }
        min = Math.Min(alignStart, alignEnd);
        max = Math.Max(alignStart, alignEnd);
    }

    internal static double Cushion(double value, double min, double max, double friction) =>
        value > max ? max + (value - max) * friction : value < min ? min + (value - min) * friction : value;

    private static Vector Rotate(Vector v, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new Vector(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }
}
