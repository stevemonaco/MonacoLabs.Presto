namespace Monaco.Presto.AvaloniaBridge.Compositor;

/// <summary>The composition properties Presto compiles animations into (COMPOSITOR-PLAN §2.2), one key frame track each.</summary>
internal enum CompositorTrackKind
{
    Translation,
    Scale,
    RotationAngle,
    Opacity,
}

/// <summary>An animated property name resolved to the composition property it feeds and the channel it drives there.</summary>
internal readonly record struct CompositorProperty(CompositorTrackKind Kind, string Channel)
{
    /// <exception cref="NotSupportedException">The property has no composition counterpart, so it can only be animated on the UI thread.</exception>
    public static CompositorProperty Parse(string property) =>
        Find(property) ?? throw new NotSupportedException(
            $"'{property}' has no composition property. The compositor animates X, Y, Rotate, Scale, ScaleX, ScaleY and Opacity only.");

    /// <summary>The composition property <paramref name="property"/> feeds, or null when it has none.</summary>
    public static CompositorProperty? Find(string property) => property switch
    {
        "X" or "Y" => new(CompositorTrackKind.Translation, property),
        "Scale" or "ScaleX" or "ScaleY" => new(CompositorTrackKind.Scale, property),
        "Rotate" => new(CompositorTrackKind.RotationAngle, property),
        "Opacity" or "Visual.Opacity" => new(CompositorTrackKind.Opacity, "Opacity"),
        _ => null,
    };
}
