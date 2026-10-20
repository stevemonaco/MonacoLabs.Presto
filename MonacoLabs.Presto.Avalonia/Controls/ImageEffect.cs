namespace MonacoLabs.Presto.Controls;

/// <summary>
/// What <see cref="ImageEffectDrawOperation"/> applies to an image, in this order: a color matrix, a ripple, a pixelation,
/// a seeded noise dissolve, then a linear wipe mask.
/// </summary>
internal readonly struct ImageEffect(in ColorMatrix matrix, double ripple, double rippleScale, double pixelate,
    double dissolve, double dissolveScale, double dissolveSoftness, int seed)
{
    public readonly ColorMatrix Matrix = matrix;
    public double Ripple { get; init; } = ripple;
    public readonly double RippleScale = rippleScale;
    public double Pixelate { get; init; } = pixelate;
    public double Dissolve { get; init; } = dissolve;
    public readonly double DissolveScale = dissolveScale;
    public readonly double DissolveSoftness = dissolveSoftness;
    public readonly int Seed = seed;

    /// <summary>How far the wipe has shown the image, from 0 (hidden) to 1 (no mask), with the angle and feather of <c>MaskSweep</c>.</summary>
    public double Wipe { get; init; } = 1;
    public double WipeAngle { get; init; }
    public double WipeFeather { get; init; }

    public bool IsIdentity => Matrix.IsIdentity && Ripple == 0 && Pixelate <= 1 && Dissolve <= 0 && Wipe >= 1;

    public bool Equals(in ImageEffect other) =>
        Matrix.AsSpan().SequenceEqual(other.Matrix.AsSpan()) && Ripple == other.Ripple && RippleScale == other.RippleScale
        && Pixelate == other.Pixelate && Dissolve == other.Dissolve && DissolveScale == other.DissolveScale
        && DissolveSoftness == other.DissolveSoftness && Seed == other.Seed
        && Wipe == other.Wipe && WipeAngle == other.WipeAngle && WipeFeather == other.WipeFeather;
}
