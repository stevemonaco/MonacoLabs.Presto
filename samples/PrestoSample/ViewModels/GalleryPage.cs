using System;
using Avalonia.Controls;

namespace PrestoSample.ViewModels;

/// <summary>One entry in the gallery: the nav title, the blurb shown above the page, and the factory that builds it.</summary>
public sealed record GalleryPage(string Title, string Description, Func<Control> Create);
