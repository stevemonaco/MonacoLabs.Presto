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
}

internal static class TransformShorthands
{
    public static TransformShorthand? Parse(string name) => name switch
    {
        "X" => TransformShorthand.X,
        "Y" => TransformShorthand.Y,
        "Rotate" => TransformShorthand.Rotate,
        "Scale" => TransformShorthand.Scale,
        "ScaleX" => TransformShorthand.ScaleX,
        "ScaleY" => TransformShorthand.ScaleY,
        "SkewX" => TransformShorthand.SkewX,
        "SkewY" => TransformShorthand.SkewY,
        _ => null,
    };
}
