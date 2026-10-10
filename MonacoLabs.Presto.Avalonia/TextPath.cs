using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using MonacoLabs.Presto.Text;
using MonacoLabs.Presto.Transforms;

namespace MonacoLabs.Presto;

/// <summary>
/// Lays a <see cref="TextBlock"/>'s characters along a <see cref="Shape"/>'s contour, each centered on it and rotated to its
/// tangent: <c>presto:TextPath.Path="{Binding #Curve}" presto:TextPath.Offset="0.1"</c>.
/// </summary>
/// <remarks>
/// The block is split into characters while <see cref="PathProperty"/> is set. Each character's placement is its followed
/// <c>"X"</c>, <c>"Y"</c> and <c>"Rotate"</c> base, so an animation of those shorthands layers over it and reverts back to it.
/// The text follows the shape's live geometry and position, re-placed at most once per engine frame; characters past the
/// ends of an open contour are hidden, and a closed contour wraps.
/// </remarks>
public static class TextPath
{
    /// <summary>The shape whose contour the text follows; clearing it puts the text back.</summary>
    public static readonly AttachedProperty<Shape?> PathProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, Shape?>("Path", typeof(TextPath));

    /// <summary>Where along the contour the text is anchored, as a fraction 0–1 of its length.</summary>
    public static readonly AttachedProperty<double> OffsetProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, double>("Offset", typeof(TextPath));

    /// <summary>Which point of the text sits at <see cref="OffsetProperty"/>.</summary>
    public static readonly AttachedProperty<TextPathAlign> AlignProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, TextPathAlign>("Align", typeof(TextPath));

    /// <summary>How far, in DIPs, the characters sit off the contour, towards their tops.</summary>
    public static readonly AttachedProperty<double> ShiftProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, double>("Shift", typeof(TextPath));

    private static readonly AttachedProperty<Placer?> PlacerProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, Placer?>("Placer", typeof(TextPath));

    static TextPath()
    {
        PathProperty.Changed.AddClassHandler<TextBlock>(HandlePathChanged);
        OffsetProperty.Changed.AddClassHandler<TextBlock>(Request);
        AlignProperty.Changed.AddClassHandler<TextBlock>(Request);
        ShiftProperty.Changed.AddClassHandler<TextBlock>(Request);
    }

    /// <summary>Gets the shape <paramref name="block"/>'s text follows.</summary>
    public static Shape? GetPath(TextBlock block) => block.GetValue(PathProperty);

    /// <summary>Sets the shape <paramref name="block"/>'s text follows.</summary>
    public static void SetPath(TextBlock block, Shape? value) => block.SetValue(PathProperty, value);

    /// <summary>Gets where along the contour <paramref name="block"/>'s text is anchored.</summary>
    public static double GetOffset(TextBlock block) => block.GetValue(OffsetProperty);

    /// <summary>Sets where along the contour <paramref name="block"/>'s text is anchored.</summary>
    public static void SetOffset(TextBlock block, double value) => block.SetValue(OffsetProperty, value);

    /// <summary>Gets which point of <paramref name="block"/>'s text sits at the offset.</summary>
    public static TextPathAlign GetAlign(TextBlock block) => block.GetValue(AlignProperty);

    /// <summary>Sets which point of <paramref name="block"/>'s text sits at the offset.</summary>
    public static void SetAlign(TextBlock block, TextPathAlign value) => block.SetValue(AlignProperty, value);

    /// <summary>Gets how far <paramref name="block"/>'s characters sit off the contour.</summary>
    public static double GetShift(TextBlock block) => block.GetValue(ShiftProperty);

    /// <summary>Sets how far <paramref name="block"/>'s characters sit off the contour.</summary>
    public static void SetShift(TextBlock block, double value) => block.SetValue(ShiftProperty, value);

    private static void HandlePathChanged(TextBlock block, AvaloniaPropertyChangedEventArgs e)
    {
        block.GetValue(PlacerProperty)?.Release();
        if (e.NewValue is Shape shape) block.SetValue(PlacerProperty, new Placer(block, shape));
        else block.ClearValue(PlacerProperty);
    }

    private static void Request(TextBlock block, AvaloniaPropertyChangedEventArgs e) => block.GetValue(PlacerProperty)?.Request();

    private sealed class Placer
    {
        private const double ClosedTolerance = 0.01;

        private readonly TextBlock _block;
        private readonly Shape _shape;
        private readonly TextSplitter _split;
        // A sink flushes once at the end of an engine frame, after the animations that moved the shape this frame.
        private readonly MotionValue _requests = new(0);
        private readonly Action<MotionValue> _place;
        private Rect[] _slots = [];

        public Placer(TextBlock block, Shape shape)
        {
            _block = block;
            _shape = shape;
            _place = _ => Place();
            _requests.Changed += _place;
            shape.PropertyChanged += HandleChanged;
            block.LayoutUpdated += HandleChanged;
            _split = PrestoAvalonia.SplitText(block, new() { Words = null, Chars = new(), IncludeSpaces = true });
            _split.AddEffect(_ => { Place(); return (Action?)null; });
        }

        public void Request() => _requests.Set(_requests.Value + 1);

        public void Release()
        {
            _requests.Changed -= _place;
            _shape.PropertyChanged -= HandleChanged;
            _block.LayoutUpdated -= HandleChanged;
            _split.Revert();
        }

        private void HandleChanged(object? sender, EventArgs e) => Request();

        private void Place()
        {
            var chars = _split.Chars;
            if (chars.Count == 0 || _shape.RenderedGeometry is not { } geometry || _shape.TransformToVisual(_block) is not { } matrix) return;
            var length = geometry.ContourLength;
            if (!(length > 0)) return;
            geometry.TryGetPointAtDistance(0, out var first);
            geometry.TryGetPointAtDistance(length, out var last);
            var closed = Point.Distance(first, last) < ClosedTolerance;

            if (_slots.Length != chars.Count) _slots = new Rect[chars.Count];
            var total = 0.0;
            for (var i = 0; i < chars.Count; i++) total += (_slots[i] = _split.UncheckedSlotOf(chars[i])).Width;
            var advance = GetOffset(_block) * length - GetAlign(_block) switch
            {
                TextPathAlign.End => total,
                TextPathAlign.Center => total / 2,
                _ => 0,
            };
            var shift = GetShift(_block);

            for (var i = 0; i < chars.Count; i++)
            {
                var piece = chars[i];
                var slot = _slots[i];
                var at = advance + slot.Width / 2;
                advance += slot.Width;
                if (closed) at = (at % length + length) % length;
                else if (at < 0 || at > length)
                {
                    piece.Opacity = 0;
                    continue;
                }
                piece.ClearValue(Visual.OpacityProperty);
                if (!geometry.TryGetPointAndTangentAtDistance(at, out var point, out var tangent)) continue;

                point = point.Transform(matrix);
                var tx = tangent.X * matrix.M11 + tangent.Y * matrix.M21;
                var ty = tangent.X * matrix.M12 + tangent.Y * matrix.M22;
                var norm = Math.Sqrt(tx * tx + ty * ty);
                if (norm == 0) continue;
                (tx, ty) = (tx / norm, ty / norm);

                // The slot's center lands on the point once rotated about the render-transform origin and translated.
                var origin = piece.RenderTransformOrigin.ToPixels(slot.Size);
                var (armX, armY) = (slot.Width / 2 - origin.X, slot.Height / 2 - origin.Y);
                var x = point.X + ty * shift - (slot.X + origin.X + armX * tx - armY * ty);
                var y = point.Y - tx * shift - (slot.Y + origin.Y + armX * ty + armY * tx);
                RenderTransformComposer.Follow(piece, TransformShorthand.X, x);
                RenderTransformComposer.Follow(piece, TransformShorthand.Y, y);
                RenderTransformComposer.Follow(piece, TransformShorthand.Rotate, Math.Atan2(ty, tx) * 180 / Math.PI);
            }
        }
    }
}
