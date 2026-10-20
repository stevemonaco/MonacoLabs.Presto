using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using PrestoGallery.ViewModels;
using static PrestoGallery.Tests.NavScene;

namespace PrestoGallery.Tests;

/// <summary>The gallery on a phone: every page fits the width, scrolls under its title, and the shell turns compact in landscape.</summary>
public sealed class PhoneLayoutTests
{
    private static readonly IReadOnlyList<GalleryPage> Pages = [.. new MainViewModel().Sections.SelectMany(section => section.Pages)];

    public static TheoryData<string> Titles => [.. Pages.Select(page => page.Title)];

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetLogicalDescendants().OfType<T>().Single(control => control.Name == name);

    private static void Open(Window window, MainViewModel gallery, string title)
    {
        gallery.OpenFromNav(Pages.First(page => page.Title == title));
        Assert.True(SceneHarness.Pump(() => !gallery.IsCompact || !Named<Border>(window, "Home").IsVisible).Result);
        for (var i = 0; i < 20; i++) TestFrames.Tick();
    }

    [AvaloniaTheory]
    [MemberData(nameof(Titles))]
    public void EveryPageFitsAPhoneWidth(string title) => With((window, gallery, _) =>
    {
        TestFrames.Install();
        Open(window, gallery, title);
        var scroller = Named<ScrollViewer>(window, "PageScroller");
        var page = (Visual)Named<TransitioningContentControl>(window, "PageHost").Content!;
        var first = page.GetVisualChildren().Select(child => Overflowing(child, scroller)).FirstOrDefault(found => found != null);
        Assert.True(first == null, $"{title}: {Describe(first)} runs {Over(first, scroller):0} DIP past the right edge, in {Ancestry(first)}");
    }, width: 360, height: 740);

    // The outermost visual laid out past the edge, clipped or not: a stage that clips its content still hides it. Render
    // transforms are left out, so a mid-flight animation does not count, and so are scaling boxes, horizontal scrollers, marquee
    // strips and anything marked to bleed.
    private static Visual? Overflowing(Visual visual, ScrollViewer scroller)
    {
        if (!visual.IsVisible || visual.GetType().Name is "Marquee" or "Pager" || visual is StyledElement { Classes: var classes } && classes.Contains("bleed"))
            return null;
        if (Over(visual, scroller) >= 1) return visual;
        if (visual is Viewbox) return null;
        if (visual is ScrollContentPresenter { TemplatedParent: ScrollViewer { HorizontalScrollBarVisibility: not ScrollBarVisibility.Disabled } })
            return null;
        return visual.GetVisualChildren().Select(child => Overflowing(child, scroller)).FirstOrDefault(found => found != null);
    }

    private static double Over(Visual? visual, ScrollViewer scroller)
    {
        if (visual == null) return 0;
        // A negative margin is a deliberate bleed past the parent.
        var right = visual.Bounds.Width + Math.Min(0, (visual as Layoutable)?.Margin.Right ?? 0);
        for (var at = visual; at != null && at != scroller; at = at.GetVisualParent()) right += at.Bounds.X;
        return right - scroller.Bounds.Width;
    }

    private static string Describe(Visual? visual) => $"{visual?.GetType().Name} {(visual as Control)?.Name}".TrimEnd();

    private static string Ancestry(Visual? visual) =>
        string.Join(" < ", visual?.GetVisualAncestors().Take(4).Select(Describe) ?? []);

    [AvaloniaFact]
    public void TheTitleScrollsAwayWithALongPage() => With((window, gallery, _) =>
    {
        TestFrames.Install();
        Open(window, gallery, "Springs");
        var scroller = Named<ScrollViewer>(window, "PageScroller");
        var header = Named<Grid>(window, "Header");
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height * 2);

        scroller.Offset = new Vector(0, 400);
        TestFrames.Tick();

        Assert.True(header.TranslatePoint(new Point(0, header.Bounds.Height), scroller)!.Value.Y < 0);
    }, width: 360, height: 740);

    [AvaloniaFact]
    public void AShortPageStillFillsTheViewport() => With((window, gallery, _) =>
    {
        TestFrames.Install();
        Open(window, gallery, "Timer");
        var scroller = Named<ScrollViewer>(window, "PageScroller");
        var host = Named<TransitioningContentControl>(window, "PageHost");

        Assert.Equal(scroller.Viewport.Height, host.TranslatePoint(new Point(0, host.Bounds.Height), scroller)!.Value.Y + 24, 1);
    }, width: 1280, height: 1400);

    [AvaloniaFact]
    public void AViewportPageFillsThePhoneScreenOnceItsTitleScrollsAway() => With((window, gallery, _) =>
    {
        TestFrames.Install();
        Open(window, gallery, "Chat log");
        var scroller = Named<ScrollViewer>(window, "PageScroller");
        var host = Named<TransitioningContentControl>(window, "PageHost");
        var margin = Named<Grid>(window, "PageColumn").Margin;

        Assert.Equal(scroller.Viewport.Height - margin.Top - margin.Bottom, host.Bounds.Height, 1);
        Assert.Equal(scroller.Viewport.Height + Named<Grid>(window, "Header").Bounds.Height + 16, scroller.Extent.Height, 1);
    }, width: 360, height: 740);

    [AvaloniaFact]
    public void AViewportPageFitsUnderTheTitleWhenWide() => With((window, gallery, _) =>
    {
        TestFrames.Install();
        Open(window, gallery, "Chat log");
        var scroller = Named<ScrollViewer>(window, "PageScroller");
        var host = Named<TransitioningContentControl>(window, "PageHost");

        Assert.Equal(scroller.Viewport.Height, scroller.Extent.Height, 1);
        Assert.Equal(scroller.Viewport.Height - 24, host.TranslatePoint(new Point(0, host.Bounds.Height), scroller)!.Value.Y, 1);
    }, width: 1400, height: 1000);

    [AvaloniaTheory]
    [InlineData(860, 400, true)]
    [InlineData(1024, 768, false)]
    [InlineData(400, 850, true)]
    public void ALandscapePhoneGetsTheCompactShell(double width, double height, bool compact) => With((_, gallery, _) =>
        Assert.Equal(compact, gallery.IsCompact), width: width, height: height);

    [AvaloniaFact]
    public void TheBackButtonIsAFullTouchTarget() => With((window, gallery, _) =>
    {
        TestFrames.Install();
        Open(window, gallery, "Timer");
        var back = Named<Button>(window, "Back");

        Assert.True(back.IsEffectivelyVisible);
        Assert.True(back.Bounds is { Width: >= 48, Height: >= 48 });
    }, width: 360, height: 740);
}
