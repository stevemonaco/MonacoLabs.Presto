namespace PrestoGallery.Showcase.Pages;

/// <summary>A destination as both views bind it; <see cref="Id"/> builds their connected keys.</summary>
public sealed record Destination(int Id, string City, string Country, double Hue, string Blurb);
