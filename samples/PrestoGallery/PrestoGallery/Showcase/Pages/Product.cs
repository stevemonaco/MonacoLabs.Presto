using Avalonia.Media;

namespace PrestoGallery.Showcase.Pages;

/// <summary>A product as the grid, the quick view and the cart show it; <see cref="Hue"/> and <see cref="Index"/> draw its <see cref="Picture"/>.</summary>
public sealed record Product(string Name, string Category, string Price, double Hue, int Index, bool SoldOut = false)
{
    public DrawingImage Picture { get; } = Scenery.Picture(Hue, Index);
}
