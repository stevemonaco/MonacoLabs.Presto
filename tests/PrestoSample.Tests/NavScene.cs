using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Monaco.Presto;
using PrestoSample.ViewModels;
using PrestoSample.Views;

namespace PrestoSample.Tests;

/// <summary>Shows the gallery's <see cref="MainView"/> for the nav tests and finds the nav's containers and template parts.</summary>
internal static class NavScene
{
    public static void With(Action<Window, MainViewModel, TreeView> test, bool reduced = false)
    {
        var was = Presto.Defaults.ReducedMotion;
        Presto.Defaults.ReducedMotion = reduced;
        try
        {
            var gallery = new MainViewModel();
            var window = SceneHarness.Show(new MainView { DataContext = gallery }, 1400, 1000);
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
