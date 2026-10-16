namespace Tempo.ViewModels;

/// <summary>One day of the heatmap.</summary>
/// <param name="Level">The shade, 0 for no focus up to 4 for two hours or more.</param>
/// <param name="Distance">How many cells away today's cell is, for the reveal outward from it.</param>
public sealed record HeatmapCell(DateTime Date, int Minutes, int Level, double Distance);
