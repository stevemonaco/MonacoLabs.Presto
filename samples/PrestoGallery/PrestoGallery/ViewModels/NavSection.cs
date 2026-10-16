using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PrestoGallery.ViewModels;

/// <summary>A section as the nav shows it: the pages matching the search, and whether it is expanded.</summary>
public sealed partial class NavSection : ObservableObject
{
    private readonly GallerySection _section;
    private bool _searching;
    private bool _ownExpanded = true;

    public NavSection(GallerySection section)
    {
        _section = section;
        Pages = [.. section.Pages];
    }

    public string Name => _section.Name;

    /// <summary>The section's pages that match the search, in catalog order.</summary>
    public ObservableCollection<GalleryPage> Pages { get; }

    /// <summary>Forced on while a search is active; otherwise the user's own choice.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    partial void OnIsExpandedChanged(bool value)
    {
        if (!_searching) _ownExpanded = value;
    }

    // A section-name match shows all its pages.
    internal bool Filter(string query)
    {
        var all = query.Length == 0 || Name.Contains(query, StringComparison.OrdinalIgnoreCase);
        Sync(Pages, _section.Pages, page => all
            || page.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || page.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
        _searching = query.Length > 0;
        IsExpanded = _searching || _ownExpanded;
        return Pages.Count > 0;
    }

    internal static void Sync<T>(ObservableCollection<T> visible, IEnumerable<T> catalog, Func<T, bool> show)
    {
        var index = 0;
        foreach (var item in catalog)
        {
            var shown = index < visible.Count && EqualityComparer<T>.Default.Equals(visible[index], item);
            if (!show(item))
            {
                if (shown) visible.RemoveAt(index);
                continue;
            }
            if (!shown) visible.Insert(index, item);
            index++;
        }
    }
}
