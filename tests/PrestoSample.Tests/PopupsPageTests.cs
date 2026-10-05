using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class PopupsPageTests
{
    private static int Frames => (int)Math.Ceiling(Recipes.Tokens.Short / 16) + 2;

    [AvaloniaTheory]
    [InlineData("MenuButton")]
    [InlineData("DetailsButton")]
    public void AFlyoutOpensFromItsStartValuesAndStaysUntilItsCloseHasPlayed(string button)
    {
        var (window, page) = Loaded();
        var anchor = page.GetControl<Button>(button);
        var flyout = (PopupFlyoutBase)anchor.Flyout!;

        flyout.ShowAt(anchor);
        var presenter = flyout.Popup.Child!;

        Assert.Equal(0, presenter.Opacity);
        TestFrames.Tick(Frames);
        Assert.Equal(1, presenter.Opacity);

        flyout.Hide();
        TestFrames.Tick();
        Assert.True(flyout.IsOpen);
        TestFrames.Tick(Frames);
        Assert.False(flyout.IsOpen);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheContextMenuAndTheDropDownOpenFromTheirStartValues()
    {
        var (window, page) = Loaded();
        var target = page.GetControl<Border>("ContextTarget");
        var picker = page.GetControl<ComboBox>("Picker");

        target.ContextMenu!.Open(target);
        Assert.Equal(0, target.ContextMenu.Opacity);
        TestFrames.Tick(Frames);
        target.ContextMenu.Close();
        TestFrames.Tick(Frames);

        picker.IsDropDownOpen = true;
        var dropDown = picker.GetVisualDescendants().OfType<Popup>().Single().Child!;
        Assert.Equal(0, dropDown.Opacity);
        TestFrames.Tick(Frames);
        Assert.Equal(1, dropDown.Opacity);

        picker.IsDropDownOpen = false;
        TestFrames.Tick();
        Assert.True(dropDown.IsAttachedToVisualTree());
        TestFrames.Tick(Frames);
        Assert.False(dropDown.IsAttachedToVisualTree());
        SceneHarness.Close(window);
    }

    private static (Window Window, PopupsPage Page) Loaded()
    {
        var page = new PopupsPage();
        return (SceneHarness.Show(page, 1280, 900), page);
    }
}
