using Avalonia;
using Monaco.Presto.AvaloniaBridge.Transforms;
using Monaco.Presto.Core;
using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Compositor;

/// <summary>
/// The transform and opacity a compiled animation's mirror writes to instead of the control (COMPOSITOR-PLAN §4.3 step 1),
/// seeded from where the control stands so a relative or inherited value starts where the UI thread would have started it.
/// </summary>
internal sealed class CompositorScratch
{
    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>Always in degrees, whatever unit the value carried, which is <see cref="Adapter"/>'s reason to exist.</summary>
    public double Rotate { get; set; }

    public double Scale { get; set; }

    public double ScaleX { get; set; }

    public double ScaleY { get; set; }

    public double Opacity { get; set; }

    internal (double X, double Y, double Rotate, double Scale, double ScaleX, double ScaleY, double Opacity) Key =>
        (X, Y, Rotate, Scale, ScaleX, ScaleY, Opacity);

    public static CompositorScratch Seed(Visual visual) => new()
    {
        X = RenderTransformComposer.Get(visual, TransformShorthand.X),
        Y = RenderTransformComposer.Get(visual, TransformShorthand.Y),
        Rotate = RenderTransformComposer.Get(visual, TransformShorthand.Rotate),
        Scale = RenderTransformComposer.Get(visual, TransformShorthand.Scale),
        ScaleX = RenderTransformComposer.Get(visual, TransformShorthand.ScaleX),
        ScaleY = RenderTransformComposer.Get(visual, TransformShorthand.ScaleY),
        Opacity = visual.Opacity,
    };

    /// <summary>
    /// Resolves the scratch's angle the way the control's own would resolve: the reflection adapter sees a plain double and
    /// would write <c>"1turn"</c> as the number 1, where the compositor has to compile the same 360 degrees the UI thread does.
    /// </summary>
    internal sealed class Adapter : ITargetAdapter
    {
        private static readonly IPropertyAccessor s_angle = new AngleAccessor();

        public IPropertyAccessor? Resolve(object target, string property) =>
            target is CompositorScratch && property == "Rotate" ? s_angle : null;

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
}
