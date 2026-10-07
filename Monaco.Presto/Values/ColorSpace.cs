namespace Monaco.Presto.Values;

/// <summary>The CSS Color 4 interpolation spaces <c>srgb</c>, <c>srgb-linear</c>, <c>oklab</c> and <c>oklch</c>.</summary>
public enum ColorSpace
{
    /// <summary>Gamma-encoded sRGB channels, CSS <c>srgb</c>.</summary>
    Srgb,

    /// <summary>Linear-light sRGB channels, CSS <c>srgb-linear</c>.</summary>
    LinearSrgb,

    /// <summary>The perceptual OKLab space: a straight line between the two colors.</summary>
    Oklab,

    /// <summary>OKLab's polar form: lightness, chroma and hue, so a path travels round the hue wheel.</summary>
    Oklch,
}
