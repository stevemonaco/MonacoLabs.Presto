using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using PrestoGallery.ViewModels;
using static PrestoGallery.Tests.NavScene;

namespace PrestoGallery.Tests;

/// <summary>The gallery nav's motion: hover fades, sections that fold on an eased height, and the rows' initial rise.</summary>
public sealed class NavMotionTests
{
    [AvaloniaFact]
    public void CollapsingASectionShrinksItsBodyToZeroAndTheNextHeaderEndsBelowIt() => With((window, gallery, nav) =>
    {
        TestFrames.Tick(60);
        var (section, next) = (Container(nav, gallery.VisibleSections[0]), Container(nav, gallery.VisibleSections[1]));
        var heights = new List<double> { Body(section) };

        gallery.VisibleSections[0].IsExpanded = false;
        for (var i = 0; i < 40; i++)
        {
            TestFrames.Tick();
            heights.Add(Body(section));
        }

        Assert.True(heights[0] > 100);
        Assert.Contains(heights, height => height > 0 && height < heights[0]);
        for (var i = 1; i < heights.Count; i++) Assert.True(heights[i] <= heights[i - 1] + .01, $"Frame {i} grew from {heights[i - 1]} to {heights[i]}.");
        Assert.Equal(0, heights[^1], 0.5);
        var header = Part<Border>(section, "PART_LayoutRoot");
        Assert.Equal(SceneHarness.Slot(header, window).Bottom, SceneHarness.Slot(Part<Border>(next, "PART_LayoutRoot"), window).Top, 2.5);
    });

    [AvaloniaFact]
    public void CollapsingFadesThePagesInPlaceAndThenHidesThem() => With((_, gallery, nav) =>
    {
        TestFrames.Tick(60);
        var section = Container(nav, gallery.VisibleSections[0]);
        var presenter = Part<ItemsPresenter>(section, "PART_ItemsPresenter");
        var full = Body(section);

        gallery.VisibleSections[0].IsExpanded = false;
        TestFrames.Tick(3);
        Assert.True(presenter.IsVisible);
        Assert.InRange(presenter.Opacity, .01, .99);
        Assert.Equal(full, Body(section), 0.5);

        TestFrames.Tick(40);
        Assert.False(presenter.IsVisible);
        Assert.Equal(0, Body(section), 0.5);
    });

    [AvaloniaFact]
    public void ExpandingRestoresTheBodyAndStaggersItsPagesBackIn() => With((_, gallery, nav) =>
    {
        TestFrames.Tick(60);
        var section = Container(nav, gallery.VisibleSections[0]);
        var full = Body(section);
        var pages = section.GetLogicalChildren().OfType<TreeViewItem>().ToList();
        gallery.VisibleSections[0].IsExpanded = false;
        TestFrames.Tick(40);

        gallery.VisibleSections[0].IsExpanded = true;
        TestFrames.Tick(4);
        Assert.InRange(Body(section), 1, full - 1);
        Assert.InRange(pages[0].Opacity, .01, .99);
        Assert.True(pages[0].Opacity > pages[^1].Opacity, $"the first page was at {pages[0].Opacity:0.00} and the last at {pages[^1].Opacity:0.00}");
        TestFrames.Tick(40);

        Assert.Equal(full, Body(section), 0.5);
        Assert.All(pages, page => Assert.Equal(1, page.Opacity));
    });

    [AvaloniaFact]
    public void ExpandingASectionReplaysNothingInTheOthers() => With((_, gallery, nav) =>
    {
        TestFrames.Tick(60);
        gallery.VisibleSections[0].IsExpanded = false;
        TestFrames.Tick(40);
        var others = gallery.VisibleSections.Skip(1).SelectMany(section => Container(nav, section).GetLogicalChildren().OfType<TreeViewItem>()).ToList();

        gallery.VisibleSections[0].IsExpanded = true;
        for (var i = 0; i < 20; i++)
        {
            TestFrames.Tick();
            Assert.All(others, page => Assert.Equal(1, page.Opacity));
        }
    });

    [AvaloniaFact]
    public void TheChevronLandsOnItsRotation() => With((_, gallery, nav) =>
    {
        var chevron = Part<Avalonia.Controls.Shapes.Path>(Container(nav, gallery.VisibleSections[0]), "Chevron");
        Assert.Equal(90, Rotation(chevron), 0.5);
        gallery.VisibleSections[0].IsExpanded = false;
        TestFrames.Tick(4);
        Assert.InRange(Rotation(chevron), 1, 89);
        TestFrames.Tick(60);
        Assert.Equal(0, Rotation(chevron), 0.5);

        gallery.VisibleSections[0].IsExpanded = true;
        TestFrames.Tick(60);
        Assert.Equal(90, Rotation(chevron), 0.5);
    });

    [AvaloniaFact]
    public void ClickingASectionHeaderCollapsesItAndKeepsTheOpenPage() => With((window, gallery, nav) =>
    {
        TestFrames.Tick(60);
        var header = Part<Border>(Container(nav, gallery.VisibleSections[0]), "PART_LayoutRoot");
        SceneHarness.Click(window, header);
        Assert.False(gallery.VisibleSections[0].IsExpanded);
        Assert.Equal(gallery.Sections[0].Pages[0], gallery.SelectedPage);
    });

    [AvaloniaFact]
    public void ClickingASectionHeaderWithTheOpenPageOffscreenCollapsesItWithoutScrolling() => With((window, gallery, nav) =>
    {
        nav.SelectedItem = gallery.Sections[^1].Pages[^1];
        TestFrames.Tick(60);
        var scroller = nav.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroller.Offset = default;
        TestFrames.Tick();

        SceneHarness.Click(window, Part<Border>(Container(nav, gallery.VisibleSections[0]), "PART_LayoutRoot"));
        Assert.False(gallery.VisibleSections[0].IsExpanded);
        Assert.Equal(0, scroller.Offset.Y);
        Assert.Equal(gallery.Sections[^1].Pages[^1], gallery.SelectedPage);
    });

    [AvaloniaFact]
    public void EveryRowRisesInWithinTheCascadeCap() => With((_, _, nav) =>
    {
        var headers = nav.GetVisualDescendants().OfType<Grid>().Where(grid => grid.Name == "PART_Header").ToList();
        Assert.True(headers.Count > 60);
        Assert.Contains(headers, header => header.Opacity < 1);

        // Shown one frame in; the last row starts by 300 ms.
        TestFrames.Tick(19);
        Assert.All(headers, header => Assert.True(header.Opacity > 0));
        TestFrames.Tick(30);
        Assert.All(headers, header => Assert.Equal(1, header.Opacity));
    });

    [AvaloniaFact]
    public void HoveringAPageFadesItsFillInAndNudgesItsTitle() => With((window, gallery, nav) =>
    {
        TestFrames.Tick(60);
        var page = Container(nav, gallery.VisibleSections[0].Pages[2]);
        var fill = Part<Border>(page, "HoverFill");
        var title = page.GetLogicalDescendants().OfType<TextBlock>().First();
        window.MouseMove(SceneHarness.Center(Part<Border>(page, "PART_LayoutRoot"), window));
        TestFrames.Tick(2);
        Assert.InRange(fill.Opacity, .01, .99);
        TestFrames.Tick(10);

        Assert.Equal(1, fill.Opacity);
        Assert.Equal(3, Convert.ToDouble(PrestoAvalonia.Get(title, "X")), 0.01);
    });

    [AvaloniaFact]
    public void UnderReducedMotionCollapseAndExpandLandWithinAFrame() => With((_, gallery, nav) =>
    {
        TestFrames.Tick(60);
        var section = Container(nav, gallery.VisibleSections[0]);
        var chevron = Part<Avalonia.Controls.Shapes.Path>(section, "Chevron");
        var pages = section.GetLogicalChildren().OfType<TreeViewItem>().ToList();
        var full = Body(section);

        gallery.VisibleSections[0].IsExpanded = false;
        TestFrames.Tick(2);
        Assert.Equal(0, Body(section), 0.5);
        Assert.Equal(0, Rotation(chevron), 0.5);
        Assert.False(Part<ItemsPresenter>(section, "PART_ItemsPresenter").IsVisible);

        gallery.VisibleSections[0].IsExpanded = true;
        TestFrames.Tick(2);
        Assert.Equal(full, Body(section), 0.5);
        Assert.Equal(90, Rotation(chevron), 0.5);
        Assert.All(pages, page => Assert.Equal(1, page.Opacity));
    }, reduced: true);

    // The section below its header, which the accordion folds.
    private static double Body(TreeViewItem section)
    {
        var header = Part<Border>(section, "PART_LayoutRoot");
        return section.Bounds.Height - header.Bounds.Bottom - header.Margin.Bottom;
    }

    private static double Rotation(Avalonia.Controls.Shapes.Path chevron) => double.Parse(Convert.ToString(PrestoAvalonia.Get(chevron, "Rotate"), CultureInfo.InvariantCulture)!.Replace("deg", ""), CultureInfo.InvariantCulture);
}
