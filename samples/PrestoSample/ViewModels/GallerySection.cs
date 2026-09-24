using System.Collections.Generic;

namespace PrestoSample.ViewModels;

/// <summary>A named group of gallery pages, shown as one expanded root in the nav.</summary>
public sealed record GallerySection(string Name, IReadOnlyList<GalleryPage> Pages);
