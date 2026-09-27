using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class BloomNavPageTests
{
    [AvaloniaFact]
    public void ABloomSwapsInTheTabsContentAndHueThenClearsItself()
    {
        var page = new BloomNavPage();
        var window = SceneHarness.Show(page, 1100, 600);

        SceneHarness.Click(window, Tab(page, 2));
        TestFrames.Tick(90);

        Assert.Empty(page.GetControl<Canvas>("Overlay").Children);
        Assert.Equal(Palette.Violet.Color, ((ISolidColorBrush)page.GetControl<Panel>("Stage").Background!).Color);
        Assert.Contains(Datasets.Albums[0].Artist, Texts(page));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void RapidClicksStackBloomsAndSettleOnTheLastTab()
    {
        var page = new BloomNavPage();
        var window = SceneHarness.Show(page, 1100, 600);

        for (var i = 1; i < 4; i++)
        {
            SceneHarness.Click(window, Tab(page, i));
            TestFrames.Tick();
        }
        TestFrames.Tick(90);

        Assert.Empty(page.GetControl<Canvas>("Overlay").Children);
        Assert.Equal(Palette.Amber.Color, ((ISolidColorBrush)page.GetControl<Panel>("Stage").Background!).Color);
        Assert.Contains(Datasets.Metrics[0].Label, string.Join('\n', Texts(page)));
        SceneHarness.Close(window);
    }

    private static Button Tab(BloomNavPage page, int index) => (Button)page.GetControl<StackPanel>("Tabs").Children[index];

    private static string[] Texts(BloomNavPage page) =>
        [.. page.GetControl<Border>("Screen").GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text ?? "")];
}
