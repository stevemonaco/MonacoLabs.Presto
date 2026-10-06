using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using PrestoSample.ViewModels;
using static PrestoSample.Tests.NavScene;

namespace PrestoSample.Tests;

/// <summary>The gallery nav's selection pill, which glides between selections and otherwise follows the selected item at once.</summary>
public sealed class NavPillTests
{
    [AvaloniaFact]
    public void ThePillStartsOnTheInitialSelection() => WithPill((_, nav, pill) =>
    {
        Assert.True(pill.IsEffectivelyVisible);
        AssertOn(pill, nav);
    });

    [AvaloniaFact]
    public void ThePillGlidesToAPageInAnotherSectionAndSettlesOnIt() => WithPill((gallery, nav, pill) =>
    {
        var from = SceneHarness.Slot(pill, nav).Y;
        var target = gallery.Sections[^1].Pages[0];
        nav.SelectedItem = target;
        TestFrames.Tick(4);

        var to = SceneHarness.Slot(Root(nav, target), nav).Y;
        var mid = SceneHarness.Slot(pill, nav).Y;
        Assert.InRange(mid, Math.Min(from, to) + 1, Math.Max(from, to) - 1);

        TestFrames.Tick(60);
        AssertOn(pill, nav);
    });

    [AvaloniaFact]
    public void UnderReducedMotionThePillLandsOnTheFirstFrame() => WithPill((gallery, nav, pill) =>
    {
        nav.SelectedItem = gallery.Sections[^1].Pages[0];
        TestFrames.Tick();
        AssertOn(pill, nav);
    }, reduced: true);

    [AvaloniaFact]
    public void FilteringTheSelectedPageOutHidesThePillAndClearingRestoresIt() => WithPill((gallery, nav, pill) =>
    {
        gallery.SearchText = "Scroll.Speed";
        TestFrames.Tick(30);
        Assert.False(pill.IsEffectivelyVisible);

        gallery.SearchText = "";
        TestFrames.Tick(30);
        Assert.True(pill.IsEffectivelyVisible);
        AssertOn(pill, nav);
    });

    [AvaloniaFact]
    public void CollapsingTheSelectedPagesSectionHidesThePill() => WithPill((gallery, nav, pill) =>
    {
        var section = gallery.VisibleSections.First(section => section.Pages.Contains(gallery.SelectedPage!));
        section.IsExpanded = false;
        TestFrames.Tick(30);
        Assert.False(pill.IsEffectivelyVisible);

        section.IsExpanded = true;
        TestFrames.Tick(30);
        Assert.True(pill.IsEffectivelyVisible);
        AssertOn(pill, nav);
    });

    [AvaloniaFact]
    public void ThePillStaysOnTheSelectionWhileASectionAboveFoldsAndUnfolds() => WithPill((gallery, nav, pill) =>
    {
        nav.SelectedItem = gallery.VisibleSections[2].Pages[0];
        TestFrames.Tick(60);

        foreach (var expanded in new[] { false, true })
        {
            gallery.VisibleSections[0].IsExpanded = expanded;
            for (var i = 0; i < 30; i++)
            {
                TestFrames.Tick();
                AssertOn(pill, nav);
            }
        }
    });

    [AvaloniaFact]
    public void ScrollingTheNavKeepsThePillOnTheSelection() => WithPill((gallery, nav, pill) =>
    {
        nav.SelectedItem = gallery.Sections[^1].Pages[^1];
        TestFrames.Tick(60);
        var scroller = nav.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroller.Offset.Y > 0);

        scroller.Offset = default;
        TestFrames.Tick();
        AssertOn(pill, nav);
    });

    private static void AssertOn(Border pill, TreeView nav)
    {
        var (slot, at) = (SceneHarness.Slot(Root(nav, nav.SelectedItem!), nav), SceneHarness.Slot(pill, nav));
        Assert.Equal(slot.Y, at.Y, 0.5);
        Assert.Equal(slot.Height, at.Height, 0.5);
    }

    private static Border Root(TreeView nav, object item) => Part<Border>(Container(nav, item), "PART_LayoutRoot");

    private static void WithPill(Action<MainViewModel, TreeView, Border> test, bool reduced = false) =>
        With((_, gallery, nav) => test(gallery, nav, Part<Border>(nav, "SelectionPill")), reduced);
}
