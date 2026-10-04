namespace Monaco.Presto.AvaloniaBridge.Lottie;

/// <summary>A playable frame range of a <see cref="LottieComposition"/>; <c>Name</c> is null for an unnamed range.</summary>
public readonly record struct LottieSegment(string? Name, double From, double To);
