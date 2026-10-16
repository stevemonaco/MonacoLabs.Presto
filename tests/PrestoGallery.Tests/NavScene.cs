using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using PrestoGallery.ViewModels;
using PrestoGallery.Views;

namespace PrestoGallery.Tests;

/// <summary>Shows the gallery's <see cref="MainView"/> for the nav tests and finds the nav's containers and template parts.</summary>
internal static class NavScene
{
    public static readonly IReadOnlyList<GalleryPage> Pages = [.. MainViewModel.Catalog.SelectMany(section => section.Pages)];

    public static GalleryPage Page(string title) => Pages.First(page => page.Title == title);

    public static void With(Action<Window, MainViewModel, TreeView> test, bool reduced = false, double width = 1400, double height = 1000)
    {
        var was = Presto.Defaults.ReducedMotion;
        Presto.Defaults.ReducedMotion = reduced;
        try
        {
            var gallery = new MainViewModel();
            var window = SceneHarness.Show(new MainView { DataContext = gallery }, width, height);
            try
            {
                test(window, gallery, window.GetLogicalDescendants().OfType<TreeView>().Single(tree => tree.Name == "Nav"));
            }
            finally
            {
                SceneHarness.Close(window);
            }
        }
        finally
        {
            Presto.Defaults.ReducedMotion = was;
        }
    }

    public static TreeViewItem Container(TreeView nav, object item) => (TreeViewItem)nav.TreeContainerFromItem(item)!;

    public static T Part<T>(Visual visual, string name) where T : Control =>
        visual.GetVisualDescendants().OfType<T>().First(part => part.Name == name);
}
