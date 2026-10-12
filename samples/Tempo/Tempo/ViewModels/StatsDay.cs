namespace Tempo.ViewModels;

/// <summary>One bar of the 7-day chart.</summary>
/// <param name="Height">Minutes relative to the week's busiest day, 0..1.</param>
public sealed record StatsDay(string Label, int Minutes, double Height);
