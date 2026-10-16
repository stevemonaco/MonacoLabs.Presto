using System.Collections.Generic;

namespace PrestoGallery.ViewModels;

/// <summary>A named group of gallery pages, shown as one root in the nav.</summary>
public sealed record GallerySection(string Name, IReadOnlyList<GalleryPage> Pages);
