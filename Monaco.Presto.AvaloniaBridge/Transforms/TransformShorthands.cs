using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Transforms;

/// <summary>
/// The transform properties the bridge composes into a render transform: upstream's <c>shortTransforms</c> and
/// <c>validTransforms</c> narrowed to the ones an Avalonia <see cref="Avalonia.Media.Transform"/> can hold.
/// </summary>
internal enum TransformShorthand
{
    X,
    Y,
    Rotate,
    Scale,
    ScaleX,
    ScaleY,
    SkewX,
    SkewY,
    RotateX,
    RotateY,
    Perspective,
}

internal static class TransformShorthands
{
    [ThreadStatic] private static DecomposedValue? s_angle;

    /// <summary>Whether <paramref name="shorthand"/> is turned in degrees rather than moved in pixels or multiplied.</summary>
    public static bool IsAngle(TransformShorthand shorthand) =>
        shorthand is TransformShorthand.Rotate or TransformShorthand.SkewX or TransformShorthand.SkewY or TransformShorthand.RotateX or TransformShorthand.RotateY;

    /// <summary>Converts an angle carrying a unit of its own (<c>"1turn"</c>, <c>".5rad"</c>) into the degrees a transform holds.</summary>
    public static double ToDegrees(double number, string? unit)
    {
        if (unit is null or "deg") return number;
        var angle = s_angle ??= new DecomposedValue();
        angle.Kind = ValueKind.Unit;
        angle.Number = number;
        angle.Unit = unit;
        UnitConverter.Convert(angle, "deg");
        return angle.Number;
    }

    public static TransformShorthand? Parse(string name) => name switch
    {
        Shorthands.X => TransformShorthand.X,
        Shorthands.Y => TransformShorthand.Y,
        Shorthands.Rotate => TransformShorthand.Rotate,
        Shorthands.Scale => TransformShorthand.Scale,
        Shorthands.ScaleX => TransformShorthand.ScaleX,
        Shorthands.ScaleY => TransformShorthand.ScaleY,
        Shorthands.SkewX => TransformShorthand.SkewX,
        Shorthands.SkewY => TransformShorthand.SkewY,
        Shorthands.RotateX => TransformShorthand.RotateX,
        Shorthands.RotateY => TransformShorthand.RotateY,
        Shorthands.Perspective => TransformShorthand.Perspective,
        _ => null,
    };
}
