using System.Text;
using Avalonia;
using Avalonia.Media;
using Monaco.Presto.Core;
using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Values;

/// <summary>
/// Decomposes a <see cref="Geometry"/> into the coordinates of a polyline sampled along its contour, and rebuilds that
/// polyline from the tween's lerped channels.
/// </summary>
/// <remarks>
/// A geometry that reaches the codec on its own is sampled at <see cref="DefaultPrecision"/>, so two ends of different
/// contour lengths decompose into different numbers of points and the shorter one is padded; pass both through
/// <see cref="PrestoAvalonia.MorphTo"/> to have them sampled against one another instead.
/// </remarks>
internal sealed class GeometryCodec : IValueCodec
{
    internal const double DefaultPrecision = .33;

    public bool TryDecode(object value, DecomposedValue into)
    {
        if (value is not Geometry geometry) return false;
        ValueParser.Decompose(Sample(geometry, PointCount(geometry.ContourLength, DefaultPrecision)), into);
        return true;
    }

    public object Encode(double number, Tween tween)
    {
        var numbers = tween._numbers;
        var strings = tween._strings;
        // A polyline is left open so a stroke follows the contour; MorphTo's contour pairs end in Z and start in M.
        var closed = strings.Length > 0 && strings[^1].Contains('Z');
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        if (closed) context.SetFillRule(FillRule.NonZero);
        context.BeginFigure(new Point(numbers[0], numbers[1]), true);
        for (var i = 2; i + 1 < numbers.Length; i += 2)
        {
            var point = new Point(numbers[i], numbers[i + 1]);
            if (i < strings.Length && strings[i].Contains('M'))
            {
                context.EndFigure(closed);
                context.BeginFigure(point, true);
            }
            else
            {
                context.LineTo(point);
            }
        }
        context.EndFigure(closed);
        return geometry;
    }

    /// <summary>Upstream's <c>maxPoints</c>: <paramref name="precision"/> points per unit of contour, and never fewer than the two a line needs.</summary>
    internal static int PointCount(double length, double precision) => Math.Max((int)Math.Ceiling(length * precision), 2);

    /// <summary>The path data of <paramref name="points"/> coordinates spread evenly along <paramref name="geometry"/>'s contour.</summary>
    internal static string Sample(Geometry geometry, int points)
    {
        var length = geometry.ContourLength;
        var data = new StringBuilder(points * 12);
        for (var i = 0; i < points; i++)
        {
            geometry.TryGetPointAtDistance(length * i / (points - 1), out var point);
            AppendPoint(data, i == 0 ? 'M' : 'L', point);
        }
        return data.ToString();
    }

    /// <summary>Appends <paramref name="command"/> and <paramref name="point"/> rounded to 3 decimals.</summary>
    internal static void AppendPoint(StringBuilder data, char command, Point point)
    {
        data.Append(command);
        data.Append(JsNumber.Format(Helpers.Round(point.X, 3)));
        data.Append(',');
        data.Append(JsNumber.Format(Helpers.Round(point.Y, 3)));
    }
}
