namespace MonacoLabs.Presto.Lottie;

/// <summary>A playable frame range of a <see cref="LottieComposition"/>; <c>Name</c> is null for a range not taken from a marker, and empty for a marker without a <c>cm</c>.</summary>
public readonly record struct LottieSegment(string? Name, double From, double To);
