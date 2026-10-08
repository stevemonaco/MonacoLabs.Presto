using System;
using Avalonia.Controls;

namespace PrestoSample.ViewModels;

/// <summary>One entry in the gallery: the nav title, the blurb shown above the page, and the factory that builds it.</summary>
public sealed record GalleryPage(string Title, string Description, Func<Control> Create)
{
    /// <summary>The name of the section holding this page, shown as the header's eyebrow.</summary>
    public string Section { get; init; } = "";

    /// <summary>The title of the Builder page that redoes this anime.js mirror, which ignores reduced motion as anime.js does.</summary>
    public string? Twin { get; init; }
}
