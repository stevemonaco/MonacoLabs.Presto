namespace MonacoLabs.Presto.Values;

/// <summary>An OKLCH color: lightness 0-1, chroma from 0 (about 0.4 at the sRGB edge), hue in degrees and alpha 0-1.</summary>
public readonly record struct OklchColor(double L, double C, double H, double A = 1);
