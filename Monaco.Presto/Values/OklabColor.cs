namespace Monaco.Presto.Values;

/// <summary>An OKLab color: lightness 0-1, the green-red <see cref="A"/> and blue-yellow <see cref="B"/> axes, and alpha 0-1.</summary>
public readonly record struct OklabColor(double L, double A, double B, double Alpha = 1);
