using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using PrestoGallery.Showcase.Pages;

namespace PrestoGallery.Tests;

public sealed class LiquidPageTests
{
    [AvaloniaFact]
    public void ThePillSettlesOnTheClickedTabsSlot()
    {
        var page = new LiquidPage();
        var window = SceneHarness.Show(page, 712, 440);
        var pill = page.GetControl<Border>("Pill");

        SceneHarness.Click(window, Tab(page, 4));
        TestFrames.Tick(60);

        Assert.Equal(4 * 60 + 6, Canvas.GetLeft(pill), 1);
        Assert.Equal(48, pill.Width, 1);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ThePillStretchesWhileItTravels()
    {
        var page = new LiquidPage();
        var window = SceneHarness.Show(page, 712, 440);
        var pill = page.GetControl<Border>("Pill");
        var rest = pill.Width;

        SceneHarness.Click(window, Tab(page, 4));
        TestFrames.Tick(5);

        Assert.True(pill.Width > rest + 20, $"width {pill.Width} at rest {rest}");
        SceneHarness.Close(window);
    }

    private static Button Tab(LiquidPage page, int index) => (Button)page.GetControl<StackPanel>("Tabs").Children[index];
}
