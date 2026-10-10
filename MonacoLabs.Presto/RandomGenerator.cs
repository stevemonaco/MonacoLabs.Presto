namespace MonacoLabs.Presto;

/// <summary>Returns a random number between <paramref name="min"/> and <paramref name="max"/> inclusive, rounded to <paramref name="decimals"/> places.</summary>
public delegate double RandomGenerator(double min = 0, double max = 1, int decimals = 0);
