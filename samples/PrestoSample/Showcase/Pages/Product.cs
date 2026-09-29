namespace PrestoSample.Showcase.Pages;

/// <summary>A product as the grid, the quick view and the cart show it; <see cref="Hue"/> and <see cref="Index"/> draw its <see cref="Scenery"/>.</summary>
public sealed record Product(string Name, string Category, string Price, double Hue, int Index);
