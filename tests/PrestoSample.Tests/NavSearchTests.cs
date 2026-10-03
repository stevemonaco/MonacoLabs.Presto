using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using PrestoSample.ViewModels;
using static PrestoSample.Tests.NavScene;

namespace PrestoSample.Tests;

/// <summary>The gallery nav's search: <see cref="MainViewModel.SearchText"/> filtering <see cref="MainViewModel.VisibleSections"/> in place.</summary>
public sealed class NavSearchTests
{
    [AvaloniaFact]
    public void FilteringKeepsCatalogOrderAndDropsSectionsWithNoMatch()
    {
        var gallery = new MainViewModel();
        gallery.SearchText = "drag";

        var expected = gallery.Sections.SelectMany(section => section.Pages).Where(page =>
            page.Title.Contains("drag", StringComparison.OrdinalIgnoreCase) || page.Description.Contains("drag", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expected, gallery.VisibleSections.SelectMany(section => section.Pages));
        Assert.All(gallery.VisibleSections, section => Assert.NotEmpty(section.Pages));
        Assert.Equal(
            gallery.Sections.Select(section => section.Name).Where(name => gallery.VisibleSections.Any(section => section.Name == name)),
            gallery.VisibleSections.Select(section => section.Name));
    }

    [AvaloniaTheory]
    [InlineData("presence.EXIT", "Effects")]
    [InlineData("Scroll.Speed", "Scroll story")]
    public void ATermOnlyInADescriptionFindsItsPage(string term, string title)
    {
        var gallery = new MainViewModel();
        gallery.SearchText = term;

        Assert.Equal([title], gallery.VisibleSections.SelectMany(section => section.Pages).Select(page => page.Title));
    }

    [AvaloniaFact]
    public void ASectionNameMatchKeepsAllItsPages()
    {
        var gallery = new MainViewModel();
        gallery.SearchText = "builder";

        var builder = Assert.Single(gallery.VisibleSections, section => section.Name == "Builder");
        Assert.Equal(gallery.Sections.Single(section => section.Name == "Builder").Pages, builder.Pages);
    }

    [AvaloniaFact]
    public void FilteringRemovesAndInsertsInTheSameCollections()
    {
        var gallery = new MainViewModel();
        var sections = gallery.VisibleSections;
        var reference = sections[0];
        var pages = reference.Pages;

        gallery.SearchText = "Timer";
        gallery.SearchText = "  ";

        Assert.Same(sections, gallery.VisibleSections);
        Assert.Same(reference, gallery.VisibleSections[0]);
        Assert.Same(pages, reference.Pages);
        Assert.Equal(gallery.Sections.Select(section => section.Pages.Count), gallery.VisibleSections.Select(section => section.Pages.Count));
    }

    [AvaloniaFact]
    public void ACollapsedSectionExpandsWhileSearchedAndCollapsesAgainOnClear()
    {
        var gallery = new MainViewModel();
        var builder = gallery.VisibleSections.Single(section => section.Name == "Builder");
        builder.IsExpanded = false;

        gallery.SearchText = "Recipes";
        Assert.True(builder.IsExpanded);

        gallery.SearchText = "";
        Assert.False(builder.IsExpanded);
        Assert.All(gallery.VisibleSections.Where(section => section != builder), section => Assert.True(section.IsExpanded));
    }

    [AvaloniaFact]
    public void TheTreeShowsTheFilteredSectionsAndKeepsTheOpenPage() => With((_, gallery, nav) =>
    {
        var timer = gallery.SelectedPage;

        gallery.SearchText = "Recipe";
        TestFrames.Tick();
        AssertTreeMatches(nav, gallery);
        Assert.Same(timer, gallery.SelectedPage);
        Assert.Null(nav.SelectedItem);

        gallery.SearchText = "";
        TestFrames.Tick();
        AssertTreeMatches(nav, gallery);
        Assert.Same(timer, gallery.SelectedPage);
        Assert.Same(timer, nav.SelectedItem);

        var reference = gallery.VisibleSections[0];
        reference.IsExpanded = false;
        TestFrames.Tick();
        Assert.False(Container(nav, reference).IsExpanded);
    });

    [AvaloniaFact]
    public void CtrlFFocusesTheSearchBoxAndSelectsItsText() => With((window, gallery, _) =>
    {
        gallery.SearchText = "drag";
        var search = SearchBox(window);
        Assert.False(search.IsFocused);

        window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, null);

        Assert.True(search.IsFocused);
        Assert.Equal("drag", search.SelectedText);
    });

    [AvaloniaFact]
    public void TypingFiltersTheTreeAndEscapeClearsIt() => With((window, gallery, nav) =>
    {
        window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, null);
        window.KeyTextInput("drag");
        TestFrames.Tick();
        Assert.Equal("drag", gallery.SearchText);
        Assert.True(gallery.VisibleSections.Sum(section => section.Pages.Count) < gallery.Sections.Sum(section => section.Pages.Count));
        AssertTreeMatches(nav, gallery);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        TestFrames.Tick();
        Assert.Equal("", gallery.SearchText);
        Assert.Equal("", SearchBox(window).Text);
        AssertTreeMatches(nav, gallery);
        Assert.Equal(gallery.Sections.Select(section => section.Pages), gallery.VisibleSections.Select(section => section.Pages));
    });

    [AvaloniaFact]
    public void EnterOpensTheFirstMatch() => With((window, gallery, nav) =>
    {
        var page = gallery.CurrentPage;
        window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, null);
        window.KeyTextInput("morph");
        TestFrames.Tick();

        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        TestFrames.Tick();

        Assert.Same(gallery.VisibleSections[0].Pages[0], gallery.SelectedPage);
        Assert.NotSame(page, gallery.CurrentPage);
        Assert.Same(gallery.SelectedPage, nav.SelectedItem);
        Assert.Contains("morph", gallery.SelectedPage!.Title + gallery.SelectedPage.Description, StringComparison.OrdinalIgnoreCase);
    });

    [AvaloniaFact]
    public void NoSamplesMatchShowsOnlyWhenNothingMatches() => With((window, gallery, _) =>
    {
        var caption = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "No samples match");
        Assert.False(caption.IsEffectivelyVisible);

        gallery.SearchText = "qwertyuiop";
        TestFrames.Tick();
        Assert.True(caption.IsEffectivelyVisible);

        gallery.SearchText = "timer";
        TestFrames.Tick();
        Assert.False(caption.IsEffectivelyVisible);
    });

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void FilteringAndClearingLeaveNoGhostsOrFadedRows(bool reduced) => With((window, gallery, nav) =>
    {
        gallery.SearchText = "Scroll.Speed";
        TestFrames.Tick();
        Assert.NotEmpty(Ghosts(window));
        AssertSettled(window, nav);

        gallery.SearchText = "";
        AssertSettled(window, nav);
        AssertTreeMatches(nav, gallery);
    }, reduced);

    private static void AssertSettled(Window window, TreeView nav)
    {
        TestFrames.Tick(60);
        Assert.Empty(Ghosts(window));
        Assert.All(nav.GetVisualDescendants().OfType<TreeViewItem>().Where(item => item.IsEffectivelyVisible), item =>
        {
            Assert.Equal(1, item.Opacity);
            Assert.All(item.GetVisualDescendants().OfType<TextBlock>(), text => Assert.Equal(1, text.Opacity));
        });
    }

    private static IEnumerable<Canvas> Ghosts(Visual window) => OverlayLayer.GetOverlayLayer(window)!.Children.OfType<Canvas>();

    private static TextBox SearchBox(Window window) => window.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "SearchBox");


    private static void AssertTreeMatches(TreeView nav, MainViewModel gallery)
    {
        Assert.Equal(gallery.VisibleSections, Realized(nav));
        for (var i = 0; i < gallery.VisibleSections.Count; i++)
            Assert.Equal(gallery.VisibleSections[i].Pages, Realized((TreeViewItem)nav.ContainerFromIndex(i)!));
    }

    private static IEnumerable<object?> Realized(ItemsControl items) =>
        items.GetRealizedContainers().OrderBy(items.IndexFromContainer).Select(container => container.DataContext);

}
