using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;
using PrestoGallery.ViewModels;
using static PrestoGallery.Tests.NavScene;

namespace PrestoGallery.Tests;

/// <summary>Sweeps the pointer down the gallery's nav a pixel row at a time and checks what each row draws as hovered.</summary>
public sealed class NavHoverTests
{
    [AvaloniaFact]
    public void ExactlyThePageUnderThePointerIsHoveredAtEveryRow() => With((window, _, nav) =>
    {
        var items = nav.GetVisualDescendants().OfType<TreeViewItem>().ToList();
        var pages = items.Where(item => item.DataContext is GalleryPage).ToList();
        var sections = items.Where(item => item.DataContext is NavSection).ToList();
        var viewport = SceneHarness.Slot(nav.GetVisualDescendants().OfType<ScrollViewer>().First(), window);
        var x = viewport.Center.X;

        for (var y = (int)viewport.Top; y < (int)viewport.Bottom; y++)
        {
            var point = new Point(x, y + 0.5);
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();

            var under = pages.SingleOrDefault(page => SceneHarness.Slot(page, window).Contains(point));
            var lit = pages.Where(ShowsHover).ToList();
            // The selected page shows only the selection pill beneath it, never the hover.
            if (under is null || under.IsSelected) Assert.Empty(lit);
            else Assert.Equal([under], lit);
            if (under is not null) Assert.DoesNotContain(sections, ShowsHover);
        }
    });

    // A page's fill fades toward its hover state, so the state is read rather than the mid-fade opacity; a header switches its background.
    private static bool ShowsHover(TreeViewItem item)
    {
        var root = Part<Border>(item, "PART_LayoutRoot");
        return Equals(Motion.GetState(root), "hot") || root.Background is ISolidColorBrush { Color: var color } && color == Palette.SurfaceHigh.Color;
    }
}
