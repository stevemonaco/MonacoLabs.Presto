using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using PrestoGallery.Showcase.Pages;

namespace PrestoGallery.Tests;

public sealed class AdaptiveSheetPageTests
{
    private const int Phone = 0;
    private const int Tablet = 1;
    private const int Frames = 60;

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheDetailsSheetIsARightDrawerFromTabletWidthUpAndABottomSheetOnAPhone(bool reduced)
    {
        using var _ = reduced ? ReducedMotion.On() : null;
        var (window, page) = Loaded();
        var details = page.GetControl<Sheet>("DetailsSheet");

        AssertForm(details, Edge.Right, 20);
        Resize(page, Tablet);
        AssertForm(details, Edge.Right, 20);
        Resize(page, Phone);
        AssertForm(details, Edge.Bottom, 0);
        Assert.Equal(3, details.Detents.Count);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpenDetailsStayOpenOnTheBottomEdgeWhenTheStageNarrowsToAPhone(bool reduced)
    {
        using var _ = reduced ? ReducedMotion.On() : null;
        var (window, page) = Loaded();
        var details = page.GetControl<Sheet>("DetailsSheet");

        SceneHarness.Click(window, page.GetControl<ItemsControl>("Notes").ContainerFromIndex(1)!);
        TestFrames.Tick(Frames);
        Assert.True(details.IsOpen);
        Assert.Equal(Showcase.Datasets.Places[1], page.GetControl<DockPanel>("Details").DataContext);

        Resize(page, Phone);

        Assert.True(details.IsOpen);
        Assert.Equal(Edge.Bottom, details.Edge);
        Assert.True(details.DetentIndex > 0);
        Assert.True(details.Position.Value > 0);
        Assert.Contains("IsOpen True", page.GetControl<TextBlock>("DetailsReadout").Text);
        Assert.Contains("Edge Bottom", page.GetControl<TextBlock>("DetailsReadout").Text);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SettingsOpenInAFlyoutAtTheGearWhenWideAndAsABottomSheetOnAPhone(bool reduced)
    {
        using var _ = reduced ? ReducedMotion.On() : null;
        var (window, page) = Loaded();
        var settings = page.GetControl<Sheet>("SettingsSheet");
        var gear = page.GetControl<Button>("Gear");

        SceneHarness.Click(window, gear);
        TestFrames.Tick(Frames);
        Assert.True(settings.IsOpen);
        Assert.NotNull(settings.Content!.FindAncestorOfType<FlyoutPresenter>());
        Assert.Same(gear, settings.Flyout!.Target);

        settings.IsOpen = false;
        TestFrames.Tick(Frames);
        Resize(page, Phone);
        SceneHarness.Click(window, gear);
        TestFrames.Tick(Frames);

        Assert.True(settings.IsOpen);
        Assert.Equal(SheetPresentation.Edge, settings.Presentation);
        Assert.Null(settings.Content.FindAncestorOfType<FlyoutPresenter>());
        Assert.Equal(Edge.Bottom, settings.Edge);
        Assert.True(settings.Position.Value > 0);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData("DetailsSheet", "click", false)]
    [InlineData("DetailsSheet", "escape", false)]
    [InlineData("SettingsSheet", "click", false)]
    [InlineData("SettingsSheet", "escape", false)]
    [InlineData("DetailsSheet", "click", true)]
    [InlineData("SettingsSheet", "escape", true)]
    public void ALightDismissClosesEitherSheet(string name, string path, bool reduced)
    {
        using var _ = reduced ? ReducedMotion.On() : null;
        var (window, page) = Loaded();
        var sheet = page.GetControl<Sheet>(name);
        sheet.IsOpen = true;
        TestFrames.Tick(Frames);

        if (path == "click")
        {
            var outside = page.GetControl<Border>("Stage").TranslatePoint(new Point(40, 300), window)!.Value;
            window.MouseDown(outside, MouseButton.Left);
            window.MouseUp(outside, MouseButton.Left);
        }
        else
        {
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        }
        TestFrames.Tick(Frames);

        Assert.False(sheet.IsOpen);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheGrabHandleShowsOnlyOnTheBottomEdge()
    {
        var (window, page) = Loaded();
        var details = page.GetControl<Sheet>("DetailsSheet");
        var grab = details.Content!.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("grab"));

        Assert.False(grab.IsVisible);
        Resize(page, Phone);
        Assert.True(grab.IsVisible);
        SceneHarness.Close(window);
    }

    private static void AssertForm(Sheet sheet, Edge edge, double edgeSwipe)
    {
        Assert.Equal(edge, sheet.Edge);
        Assert.Equal(edgeSwipe, sheet.EdgeSwipe);
    }

    private static void Resize(AdaptiveSheetPage page, int width)
    {
        page.GetControl<TabStrip>("Widths").SelectedIndex = width;
        TestFrames.Tick(2);
    }

    private static (Window Window, AdaptiveSheetPage Page) Loaded()
    {
        var page = new AdaptiveSheetPage();
        return (SceneHarness.Show(page, 1280, 900, frames: 2), page);
    }
}
