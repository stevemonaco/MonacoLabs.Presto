using Avalonia;
using MonacoLabs.Presto.Transforms;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Targets;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Compositor;

/// <summary>
/// The transform and opacity a compiled animation's mirror writes to instead of the control (COMPOSITOR-PLAN §4.3 step 1),
/// seeded from where the control stands so a relative or inherited value starts where the UI thread would have started it.
/// </summary>
internal sealed class CompositorScratch : IMemberTarget
{
    private static readonly Dictionary<string, IPropertyAccessor> s_members = new()
    {
        [nameof(X)] = ObjectTargetAdapter.MemberAccessor.Of<CompositorScratch, double>(scratch => scratch.X, (scratch, value) => scratch.X = value),
        [nameof(Y)] = ObjectTargetAdapter.MemberAccessor.Of<CompositorScratch, double>(scratch => scratch.Y, (scratch, value) => scratch.Y = value),
        [nameof(Rotate)] = new AngleAccessor(),
        [nameof(Scale)] = ObjectTargetAdapter.MemberAccessor.Of<CompositorScratch, double>(scratch => scratch.Scale, (scratch, value) => scratch.Scale = value),
        [nameof(ScaleX)] = ObjectTargetAdapter.MemberAccessor.Of<CompositorScratch, double>(scratch => scratch.ScaleX, (scratch, value) => scratch.ScaleX = value),
        [nameof(ScaleY)] = ObjectTargetAdapter.MemberAccessor.Of<CompositorScratch, double>(scratch => scratch.ScaleY, (scratch, value) => scratch.ScaleY = value),
        [nameof(Opacity)] = ObjectTargetAdapter.MemberAccessor.Of<CompositorScratch, double>(scratch => scratch.Opacity, (scratch, value) => scratch.Opacity = value),
    };

    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>Always in degrees, whatever unit the value carried, since a plain double accessor would write <c>"1turn"</c> as the number 1.</summary>
    public double Rotate { get; set; }

    public double Scale { get; set; }

    public double ScaleX { get; set; }

    public double ScaleY { get; set; }

    public double Opacity { get; set; }

    internal (double X, double Y, double Rotate, double Scale, double ScaleX, double ScaleY, double Opacity) Key =>
        (X, Y, Rotate, Scale, ScaleX, ScaleY, Opacity);

    IReadOnlyDictionary<string, IPropertyAccessor> IMemberTarget.Members => s_members;

    /// <remarks>A property another compositor animation drives starts from where that animation's mirror stands, as upstream reads a running WAAPI animation's computed style.</remarks>
    public static CompositorScratch Seed(Visual visual)
    {
        var scratch = new CompositorScratch
        {
            X = RenderTransformComposer.Get(visual, TransformShorthand.X),
            Y = RenderTransformComposer.Get(visual, TransformShorthand.Y),
            Rotate = RenderTransformComposer.Get(visual, TransformShorthand.Rotate),
            Scale = RenderTransformComposer.Get(visual, TransformShorthand.Scale),
            ScaleX = RenderTransformComposer.Get(visual, TransformShorthand.ScaleX),
            ScaleY = RenderTransformComposer.Get(visual, TransformShorthand.ScaleY),
            Opacity = visual.Opacity,
        };
        if (Held(visual, CompositorTrackKind.Translation) is { } translation) (scratch.X, scratch.Y) = (translation.X, translation.Y);
        if (Held(visual, CompositorTrackKind.Scale) is { } scale) (scratch.Scale, scratch.ScaleX, scratch.ScaleY) = (scale.Scale, scale.ScaleX, scale.ScaleY);
        if (Held(visual, CompositorTrackKind.RotationAngle) is { } rotation) scratch.Rotate = rotation.Rotate;
        if (Held(visual, CompositorTrackKind.Opacity) is { } opacity) scratch.Opacity = opacity.Opacity;
        return scratch;
    }

    private static CompositorScratch? Held(Visual visual, CompositorTrackKind kind) =>
        (CompositorRegistry.Holder(visual, kind) as CompositorAnimation)?.ScratchOf(visual);

    private sealed class AngleAccessor : IPropertyAccessor
    {
        public IValueCodec? Codec => null;

        // Read back with the unit so a "1turn" end value converts instead of being taken as 360 turns.
        public object GetValue(object target) => JsNumber.Format(((CompositorScratch)target).Rotate) + "deg";

        public void SetValue(object target, double number, Tween tween) =>
            ((CompositorScratch)target).Rotate = TransformShorthands.ToDegrees(number, tween._unit);

        public void Revert(object target, object? originalValue) =>
            ((CompositorScratch)target).Rotate =
                originalValue is string degrees && ValueParser.TryParseUnit(degrees, out var number, out _) ? number : 0;
    }
}
