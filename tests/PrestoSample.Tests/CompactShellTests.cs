using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Controls;
using PrestoSample.Pages;
using PrestoSample.ViewModels;
using PrestoSample.Views;
using static PrestoSample.Tests.NavScene;

namespace PrestoSample.Tests;

/// <summary>The gallery shell's compact layout at phone width: Home is the nav, a page opens over it and back returns.</summary>
public sealed class CompactShellTests
{
    private static void Compact(Action<Window, MainViewModel, TreeView> test, bool reduced = false) =>
        With(test, reduced, width: 400, height: 850);

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetLogicalDescendants().OfType<T>().Single(control => control.Name == name);

    private static bool Rested(Window window) =>
        SceneHarness.Pump(() => Named<Border>(window, "Home").IsVisible != Named<Border>(window, "PageLevel").IsVisible).Result;

    [AvaloniaFact]
    public void HomeShowsTheNavAtFullWidthAndBuildsNoPage() => Compact((window, gallery, nav) =>
    {
        Assert.True(gallery.IsCompact);
        Assert.True(gallery.AtHome);
        Assert.Null(gallery.CurrentPage);
        Assert.Equal(400, window.GetLogicalDescendants().OfType<GalleryNav>().Single().Bounds.Width);
        Assert.True(nav.IsEffectivelyVisible);
        Assert.False(Named<Border>(window, "PageLevel").IsVisible);
        Assert.False(Named<Button>(window, "CollapsePane").IsEffectivelyVisible);
        Assert.InRange(NavScene.Container(nav, Page("Timer")).Bounds.Height, 44, 60);
    });

    [AvaloniaFact]
    public void TappingARowOpensItAndTappingTheOpenPagesRowFromHomeOpensItAgain() => Compact((window, gallery, nav) =>
    {
        SceneHarness.Click(window, NavScene.Container(nav, Page("Callbacks")));
        Assert.False(gallery.AtHome);
        var page = Assert.IsType<CallbacksPage>(gallery.CurrentPage);
        Assert.True(Rested(window));
        Assert.False(Named<Border>(window, "Home").IsVisible);

        SceneHarness.Click(window, Named<Button>(window, "Back"));
        Assert.True(gallery.AtHome);
        Assert.True(Rested(window));

        SceneHarness.Click(window, NavScene.Container(nav, Page("Callbacks")));
        Assert.False(gallery.AtHome);
        Assert.Same(page, gallery.CurrentPage);
        Assert.True(Rested(window));
        Assert.True(Named<Border>(window, "PageLevel").IsVisible);
    });

    [AvaloniaFact]
    public void BackReturnsHomeWithItsSearchAndScrollOffsetKept() => Compact((window, gallery, nav) =>
    {
        gallery.SearchText = "e";
        TestFrames.Tick();
        var scroller = nav.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroller.Offset = new Vector(0, 200);
        TestFrames.Tick();
        var offset = scroller.Offset;
        Assert.True(offset.Y > 0);

        gallery.OpenFromNav(Page("Springs"));
        Assert.True(Rested(window));
        SceneHarness.Click(window, Named<Button>(window, "Back"));
        Assert.True(Rested(window));

        Assert.True(gallery.AtHome);
        Assert.Equal("e", gallery.SearchText);
        Assert.Equal("e", Named<TextBox>(window, "SearchBox").Text);
        Assert.Equal(offset, scroller.Offset);
    });

    [AvaloniaFact]
    public void TheAboutToggleRevealsTheDescriptionAndTwinLink() => Compact((window, gallery, _) =>
    {
        gallery.OpenFromNav(Page("Eases"));
        TestFrames.Tick();
        var description = window.GetLogicalDescendants().OfType<TextBlock>().Single(block => block.Text == Page("Eases").Description);
        var twin = Named<StackPanel>(window, "TwinLine");
        Assert.False(description.IsEffectivelyVisible);
        Assert.False(twin.IsEffectivelyVisible);

        Named<ToggleButton>(window, "About").IsChecked = true;
        TestFrames.Tick();

        Assert.True(description.IsEffectivelyVisible);
        Assert.True(twin.IsEffectivelyVisible);
    });

    [AvaloniaFact]
    public void UnderReducedMotionHomeToPageCrossfades() => Compact((window, gallery, _) =>
    {
        var tint = Color.FromRgb(255, 0, 123);
        Named<Border>(window, "PageLevel").Background = new ImmutableSolidColorBrush(tint);
        TestFrames.Tick();
        var before = Pixel(window, 4, 4);

        gallery.OpenFromNav(Page("Timer"));
        TestFrames.Tick(3);

        // The page level's margin, at both edges: a slide would show Home on the left and the page on the right.
        var left = Pixel(window, 4, 4);
        var right = Pixel(window, 396, 4);
        Assert.NotEqual(before, left);
        Assert.NotEqual(tint, left);
        Assert.InRange(Math.Abs(left.R - right.R) + Math.Abs(left.G - right.G) + Math.Abs(left.B - right.B), 0, 6);
    }, reduced: true);

    [AvaloniaFact]
    public void BackRequestedPopsAPageAndIsLeftUnhandledAtHome() => Compact((window, gallery, _) =>
    {
        gallery.OpenFromNav(Page("Eases"));
        gallery.OpenCommand.Execute("Playground");
        Assert.True(Rested(window));

        Assert.True(Back(window));
        Assert.Same(Page("Eases"), gallery.SelectedPage);
        Assert.True(gallery.IsBack);
        Assert.False(gallery.AtHome);
        Assert.True(Back(window));
        Assert.True(gallery.AtHome);
        Assert.False(gallery.CanGoBack);
        Assert.True(Rested(window));

        Assert.False(Back(window));
        Assert.True(gallery.AtHome);
        Assert.Same(Page("Eases"), gallery.SelectedPage);
    });

    [AvaloniaFact]
    public void AnOpenSheetTakesTheFirstBackRequestAndThePageTheSecond() => Compact((window, gallery, _) =>
    {
        gallery.OpenFromNav(Page("Gestures"));
        Assert.True(Rested(window));
        var sheet = gallery.CurrentPage!.GetLogicalDescendants().OfType<Sheet>().Single(sheet => sheet.Name == "QueueSheet");
        sheet.IsOpen = true;
        TestFrames.Tick();

        Assert.True(Back(window));
        Assert.False(sheet.IsOpen);
        Assert.False(gallery.AtHome);

        Assert.True(Back(window));
        Assert.True(gallery.AtHome);
    });

    [AvaloniaFact]
    public void ResizingAcrossTheBreakpointKeepsTheOpenPageAndTheNavsState() => Compact((window, gallery, nav) =>
    {
        gallery.OpenFromNav(Page("Springs"));
        gallery.OpenCommand.Execute("Builder basics");
        Assert.True(Rested(window));
        gallery.SearchText = "e";
        var section = gallery.VisibleSections[0];
        section.IsExpanded = false;
        var page = gallery.CurrentPage!;

        Resize(window, 1400);
        Assert.False(gallery.IsCompact);
        Assert.Same(page, gallery.CurrentPage);
        Assert.True(page.IsEffectivelyVisible);
        Assert.Same(Named<Border>(window, "SidebarSlot"), window.GetLogicalDescendants().OfType<GalleryNav>().Single().Parent);
        Assert.True(nav.IsEffectivelyVisible);
        Assert.Equal("e", Named<TextBox>(window, "SearchBox").Text);
        Assert.False(NavScene.Container(nav, section).IsExpanded);

        SceneHarness.Click(window, Named<Button>(window, "CollapsePane"));
        TestFrames.Tick(30);
        Resize(window, 400);
        Assert.True(gallery.IsCompact);
        Assert.False(gallery.AtHome);
        Assert.Same(page, gallery.CurrentPage);
        Assert.True(page.IsEffectivelyVisible);
        Assert.False(Named<Border>(window, "Home").IsVisible);
        Assert.Equal("e", gallery.SearchText);
        Assert.False(NavScene.Container(nav, section).IsExpanded);

        gallery.GoBack();
        Assert.Same(Page("Springs"), gallery.SelectedPage);
        gallery.GoBack();
        Assert.True(gallery.AtHome);
        Assert.True(Rested(window));
        Assert.True(Named<Border>(window, "Home").IsVisible);
        Assert.Equal(400, window.GetLogicalDescendants().OfType<GalleryNav>().Single().Bounds.Width);
    });

    [AvaloniaFact]
    public void GoingWideFromHomeOpensTheFirstPageAfterACompactStartOrThePageHomeWasReachedFrom() => Compact((window, gallery, _) =>
    {
        Resize(window, 1400);
        Assert.False(gallery.AtHome);
        Assert.IsType<TimerPage>(gallery.CurrentPage);
        Assert.True(gallery.CurrentPage!.IsEffectivelyVisible);

        Resize(window, 400);
        gallery.GoBack();
        Assert.True(Rested(window));
        gallery.OpenFromNav(Page("Springs"));
        Assert.True(Rested(window));
        var springs = gallery.CurrentPage;
        gallery.GoBack();
        Assert.True(Rested(window));

        Resize(window, 1400);
        Assert.False(gallery.AtHome);
        Assert.Same(springs, gallery.CurrentPage);
        Assert.True(springs!.IsEffectivelyVisible);
    });

    private static bool Back(Window window)
    {
        var request = new RoutedEventArgs(TopLevel.BackRequestedEvent);
        window.RaiseEvent(request);
        TestFrames.Tick();
        return request.Handled;
    }

    private static void Resize(Window window, double width)
    {
        window.Width = width;
        TestFrames.Tick(3);
    }

    private static Color Pixel(Visual visual, int x, int y)
    {
        var (pixels, stride) = SceneHarness.Capture(visual);
        var i = (y * stride) + (x * 4);
        return Color.FromRgb(pixels[i], pixels[i + 1], pixels[i + 2]);
    }
}
