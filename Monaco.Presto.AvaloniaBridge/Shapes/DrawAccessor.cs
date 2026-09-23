using System.Runtime.CompilerServices;
using Avalonia.Collections;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Monaco.Presto.Targets;

namespace Monaco.Presto.AvaloniaBridge.Shapes;

/// <summary>
/// The "Draw" pseudo-property: upstream's drawable <c>draw</c> attribute (<c>"v1 v2"</c>, both 0-1) written as the
/// <see cref="Shape.StrokeDashArray"/> and <see cref="Shape.StrokeDashOffset"/> that reveal that slice of the contour.
/// </summary>
/// <remarks>
/// Upstream normalizes the path to <c>pathLength="1000"</c> and works in those units; Avalonia has no such attribute, so the
/// dashes are measured against <see cref="Geometry.ContourLength"/> and then divided by the stroke thickness, which is the
/// unit Avalonia expresses both dash properties in.
/// </remarks>
internal sealed class DrawAccessor : IPropertyAccessor
{
    private static readonly ConditionalWeakTable<Shape, DrawState> s_states = new();

    public IValueCodec? Codec => null;

    public object GetValue(object target) => State((Shape)target).Values;

    public void SetValue(object target, double number, Tween tween) => Apply((Shape)target, tween._numbers[0], tween._numbers[1]);

    public void Revert(object target, object? originalValue)
    {
        var shape = (Shape)target;
        if (!s_states.TryGetValue(shape, out var state)) return;
        s_states.Remove(shape);
        state.Restore(shape);
    }

    internal static void Apply(Shape shape, double start, double end)
    {
        var state = State(shape);
        state.Values[0] = start;
        state.Values[1] = end;
        if (state.LineCap != PenLineCap.Flat) shape.StrokeLineCap = start == end ? PenLineCap.Flat : state.LineCap;

        var length = shape.RenderedGeometry?.ContourLength ?? 0;
        var thickness = shape.StrokeThickness;
        if (length <= 0 || thickness <= 0) return;

        var offset = -start * length;
        var dash = (end * length) + offset;
        var whole = (start == 0 && end == 1) || (start == 1 && end == 0);
        // The spare gap keeps the pattern longer than the contour, so a partial dash cannot wrap onto itself.
        var gap = length + (whole ? 0 : 10) - dash;
        shape.StrokeDashOffset = offset / thickness;
        // Written in place rather than as a fresh list per frame; AffectsRender listens to the list's own change notification.
        state.Dashes[0] = dash / thickness;
        state.Dashes[1] = gap / thickness;
        if (!ReferenceEquals(shape.StrokeDashArray, state.Dashes)) shape.StrokeDashArray = state.Dashes;
    }

    private static DrawState State(Shape shape) => s_states.GetValue(shape, static s => new DrawState(s));

    private sealed class DrawState(Shape shape)
    {
        private readonly AvaloniaList<double>? _dashes = shape.StrokeDashArray;
        private readonly double _offset = shape.StrokeDashOffset;

        public double[] Values { get; } = [0, 0];

        public AvaloniaList<double> Dashes { get; } = new(0d, 0d);

        public PenLineCap LineCap { get; } = shape.StrokeLineCap;

        public void Restore(Shape shape)
        {
            shape.StrokeDashArray = _dashes;
            shape.StrokeDashOffset = _offset;
            shape.StrokeLineCap = LineCap;
        }
    }
}
