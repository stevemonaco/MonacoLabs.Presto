namespace Monaco.Presto.Values;

/// <summary>Which way round the hue wheel an <see cref="ColorSpace.Oklch"/> interpolation goes, as CSS Color 4 defines it.</summary>
public enum HueInterpolation
{
    /// <summary>The arc of 180° or less.</summary>
    Shorter,

    /// <summary>The arc of 180° or more.</summary>
    Longer,

    /// <summary>The arc along which the hue increases.</summary>
    Increasing,

    /// <summary>The arc along which the hue decreases.</summary>
    Decreasing,
}
