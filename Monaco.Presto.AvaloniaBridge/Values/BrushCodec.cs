using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Values;

/// <summary>
/// Tweens solid brushes as colors and linear, radial and conic gradients as complex values: a header of discrete fields,
/// the opacity, six geometry numbers and one <c>(offset, r, g, b, a)</c> run per stop. Two ends of different shapes are
/// reconciled first: a solid, or missing, brush takes the gradient's shape, and two gradients are resampled onto the
/// union of their stop offsets.
/// </summary>
/// <remarks>
/// The kind, spread method and units are discrete and come from the end value, so a tween between two kinds of gradient
/// snaps its geometry to the end value's while its stops interpolate. A gradient eased to a solid brush ends on a
/// <see cref="SolidColorBrush"/>. A brush's <see cref="Brush.Transform"/> is not carried.
/// </remarks>
internal sealed class BrushCodec : IReconcilingValueCodec
{
    private const int Kind = 0;
    private const int Spread = 1;
    private const int Units = 2;
    private const int Opacity = 3;
    private const int Geometry = 4;
    private const int Stops = 10;
    private const int StopSize = 5;

    private const int Solid = 0;
    private const int Linear = 1;
    private const int Radial = 2;
    private const int Conic = 3;

    private readonly ConditionalWeakTable<Tween, IBrush> _brushes = new();

    public bool TryDecode(object value, DecomposedValue into)
    {
        switch (value)
        {
            case ISolidColorBrush solid:
                into.Kind = ValueKind.Color;
                into.Numbers = solid.Color.ToPresto().ToChannels();
                return true;
            case IGradientBrush gradient when Decompose(gradient) is { } numbers:
                ValueParser.Decompose(numbers, into);
                return true;
            default:
                return false;
        }
    }

    public bool TryParse(string text, DecomposedValue into) => false;

    public void Reconcile(DecomposedValue from, DecomposedValue to)
    {
        var fromGradient = IsGradient(from);
        var toGradient = IsGradient(to);
        if (fromGradient && toGradient)
        {
            Resample(from, to);
        }
        else if (toGradient)
        {
            Shape(from, to);
        }
        else if (fromGradient)
        {
            Shape(to, from);
            to.Numbers![Kind] = Solid;
        }
    }

    /// <summary>Recolors or restops one brush per tween; the brush being animated from is often immutable or shared, so it is never touched.</summary>
    public object Encode(double number, Tween tween)
    {
        var numbers = tween._numbers;
        if (tween._valueType == ValueKind.Color) return SolidBrush(tween, PrestoColor.FromChannels(numbers).ToAvalonia(), 1);

        var kind = (int)tween._toNumbers[Kind];
        if (kind == Solid)
        {
            if (tween._currentTime >= tween._changeDuration)
            {
                return SolidBrush(tween, PrestoColor.FromChannels(numbers.AsSpan(Stops + 1)).ToAvalonia(), numbers[Opacity]);
            }
            kind = (int)tween._fromNumbers[Kind];
        }
        return GradientBrush(tween, kind);
    }

    private IBrush SolidBrush(Tween tween, Color color, double opacity)
    {
        if (_brushes.TryGetValue(tween, out var existing) && existing is SolidColorBrush brush)
        {
            brush.Color = color;
            brush.Opacity = Math.Clamp(opacity, 0, 1);
            return brush;
        }
        brush = new SolidColorBrush(color, Math.Clamp(opacity, 0, 1));
        _brushes.AddOrUpdate(tween, brush);
        return brush;
    }

    private GradientBrush GradientBrush(Tween tween, int kind)
    {
        var numbers = tween._numbers;
        var units = (int)tween._toNumbers[Units];
        _brushes.TryGetValue(tween, out var existing);
        GradientBrush brush;
        switch (kind)
        {
            case Radial:
                var radial = existing as RadialGradientBrush ?? new RadialGradientBrush();
                radial.Center = Point(numbers, Geometry, units, 0);
                radial.GradientOrigin = Point(numbers, Geometry + 2, units, 1);
                radial.RadiusX = new RelativeScalar(numbers[Geometry + 4], Unit(units, 2));
                radial.RadiusY = new RelativeScalar(numbers[Geometry + 5], Unit(units, 3));
                brush = radial;
                break;
            case Conic:
                var conic = existing as ConicGradientBrush ?? new ConicGradientBrush();
                conic.Center = Point(numbers, Geometry, units, 0);
                conic.Angle = numbers[Geometry + 2];
                brush = conic;
                break;
            default:
                var linear = existing as LinearGradientBrush ?? new LinearGradientBrush();
                linear.StartPoint = Point(numbers, Geometry, units, 0);
                linear.EndPoint = Point(numbers, Geometry + 2, units, 1);
                brush = linear;
                break;
        }
        if (!ReferenceEquals(brush, existing)) _brushes.AddOrUpdate(tween, brush);

        brush.SpreadMethod = (GradientSpreadMethod)(int)tween._toNumbers[Spread];
        brush.Opacity = Math.Clamp(numbers[Opacity], 0, 1);
        var count = (numbers.Length - Stops) / StopSize;
        var stops = brush.GradientStops;
        while (stops.Count > count) stops.RemoveAt(stops.Count - 1);
        while (stops.Count < count) stops.Add(new GradientStop());
        for (var i = 0; i < count; i++)
        {
            var at = Stops + i * StopSize;
            stops[i].Offset = numbers[at];
            stops[i].Color = PrestoColor.FromChannels(numbers.AsSpan(at + 1)).ToAvalonia();
        }
        return brush;
    }

    private static double[]? Decompose(IGradientBrush gradient)
    {
        (int Kind, int Units, double[] Geometry)? header = gradient switch
        {
            ILinearGradientBrush linear => (Kind: Linear, Units: Mask(linear.StartPoint.Unit, 0) | Mask(linear.EndPoint.Unit, 1),
                Geometry: [linear.StartPoint.Point.X, linear.StartPoint.Point.Y, linear.EndPoint.Point.X, linear.EndPoint.Point.Y, 0, 0]),
            IRadialGradientBrush radial => (Kind: Radial,
                Units: Mask(radial.Center.Unit, 0) | Mask(radial.GradientOrigin.Unit, 1) | Mask(radial.RadiusX.Unit, 2) | Mask(radial.RadiusY.Unit, 3),
                Geometry: [radial.Center.Point.X, radial.Center.Point.Y, radial.GradientOrigin.Point.X, radial.GradientOrigin.Point.Y, radial.RadiusX.Scalar, radial.RadiusY.Scalar]),
            IConicGradientBrush conic => (Kind: Conic, Units: Mask(conic.Center.Unit, 0),
                Geometry: [conic.Center.Point.X, conic.Center.Point.Y, conic.Angle, 0, 0, 0]),
            _ => null,
        };
        if (header is not { } shape) return null;

        var stops = gradient.GradientStops.OrderBy(stop => stop.Offset).ToArray();
        var numbers = new double[Stops + stops.Length * StopSize];
        numbers[Kind] = shape.Kind;
        numbers[Spread] = (int)gradient.SpreadMethod;
        numbers[Units] = shape.Units;
        numbers[Opacity] = gradient.Opacity;
        shape.Geometry.CopyTo(numbers, Geometry);
        for (var i = 0; i < stops.Length; i++)
        {
            var at = Stops + i * StopSize;
            numbers[at] = stops[i].Offset;
            stops[i].Color.ToPresto().ToChannels().CopyTo(numbers, at + 1);
        }
        return numbers;
    }

    private static bool IsGradient(DecomposedValue value) => value.Kind == ValueKind.Complex && value.Numbers is { Length: >= Stops };

    /// <summary>Gives <paramref name="value"/>, a color or a missing brush, <paramref name="gradient"/>'s shape in its own color, or a missing brush the gradient's stop colors at alpha 0.</summary>
    private static void Shape(DecomposedValue value, DecomposedValue gradient)
    {
        var numbers = (double[])gradient.Numbers!.Clone();
        var color = value.Kind == ValueKind.Color ? value.Numbers : null;
        for (var at = Stops; at < numbers.Length; at += StopSize)
        {
            if (color != null) color.CopyTo(numbers, at + 1);
            else numbers[at + 4] = 0;
        }
        ValueParser.Decompose(numbers, value);
    }

    private static void Resample(DecomposedValue from, DecomposedValue to)
    {
        var fromNumbers = from.Numbers!;
        var toNumbers = to.Numbers!;
        // Geometry cannot be converted between gradient kinds, so the start takes the end's instead.
        if (fromNumbers[Kind] != toNumbers[Kind]) Array.Copy(toNumbers, Geometry, fromNumbers, Geometry, Stops - Geometry);

        if (fromNumbers.Length == toNumbers.Length && SameOffsets(fromNumbers, toNumbers)) return;

        var offsets = new SortedSet<double>();
        for (var at = Stops; at < fromNumbers.Length; at += StopSize) offsets.Add(fromNumbers[at]);
        for (var at = Stops; at < toNumbers.Length; at += StopSize) offsets.Add(toNumbers[at]);
        ValueParser.Decompose(Restop(fromNumbers, offsets), from);
        ValueParser.Decompose(Restop(toNumbers, offsets), to);
    }

    private static bool SameOffsets(double[] a, double[] b)
    {
        for (var at = Stops; at < a.Length; at += StopSize)
        {
            if (a[at] != b[at]) return false;
        }
        return true;
    }

    /// <summary>The same gradient with a stop at each of <paramref name="offsets"/>, colored as the original is at that offset.</summary>
    private static double[] Restop(double[] numbers, SortedSet<double> offsets)
    {
        var result = new double[Stops + offsets.Count * StopSize];
        Array.Copy(numbers, result, Stops);
        var at = Stops;
        foreach (var offset in offsets)
        {
            result[at] = offset;
            ColorAt(numbers, offset, result.AsSpan(at + 1, 4));
            at += StopSize;
        }
        return result;
    }

    private static void ColorAt(double[] numbers, double offset, Span<double> into)
    {
        var last = numbers.Length - StopSize;
        if (last < Stops)
        {
            into.Clear();
            return;
        }
        if (offset <= numbers[Stops])
        {
            numbers.AsSpan(Stops + 1, 4).CopyTo(into);
            return;
        }
        for (var at = Stops; at < last; at += StopSize)
        {
            var next = at + StopSize;
            if (offset > numbers[next]) continue;
            var span = numbers[next] - numbers[at];
            var progress = span <= 0 ? 1 : (offset - numbers[at]) / span;
            for (var channel = 0; channel < 4; channel++)
            {
                into[channel] = numbers[at + 1 + channel] + (numbers[next + 1 + channel] - numbers[at + 1 + channel]) * progress;
            }
            return;
        }
        numbers.AsSpan(last + 1, 4).CopyTo(into);
    }

    private static int Mask(RelativeUnit unit, int bit) => unit == RelativeUnit.Absolute ? 1 << bit : 0;

    private static RelativeUnit Unit(int units, int bit) => (units & (1 << bit)) != 0 ? RelativeUnit.Absolute : RelativeUnit.Relative;

    private static RelativePoint Point(double[] numbers, int at, int units, int bit) => new(numbers[at], numbers[at + 1], Unit(units, bit));
}
