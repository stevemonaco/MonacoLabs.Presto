using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class BloomNavPageTests
{
    [AvaloniaFact]
    public void ABloomSwapsInTheTabsContentAndHueThenClearsItsClip()
    {
        var page = new BloomNavPage();
        var window = SceneHarness.Show(page, 1100, 600);

        SceneHarness.Click(window, Tab(page, 2));
        TestFrames.Tick(10);
        var presenter = page.GetControl<TransitioningContentControl>("Stage").GetVisualDescendants().OfType<ContentPresenter>().Single(p => p.Content == Shown(page));
        var bloom = Assert.IsType<EllipseGeometry>(presenter.Clip).Rect.Center;
        var clicked = SceneHarness.Centre(Tab(page, 2), presenter);
        Assert.True(Math.Abs(bloom.X - clicked.X) < 1 && Math.Abs(bloom.Y - clicked.Y) < 1, $"the bloom grew from {bloom}, not the click at {clicked}");
        TestFrames.Tick(80);

        AssertSettled(page);
        Assert.Equal(Palette.Violet.Color, ((ISolidColorBrush)Shown(page).Background!).Color);
        Assert.Contains(Datasets.Albums[0].Artist, Texts(page));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void RapidClicksSettleOnTheLastTabWithNoBloomLeft()
    {
        var page = new BloomNavPage();
        var window = SceneHarness.Show(page, 1100, 600);

        for (var i = 1; i < 4; i++)
        {
            SceneHarness.Click(window, Tab(page, i));
            TestFrames.Tick();
        }
        TestFrames.Tick(90);

        AssertSettled(page);
        Assert.Equal(Palette.Amber.Color, ((ISolidColorBrush)Shown(page).Background!).Color);
        Assert.Contains(Datasets.Metrics[0].Label, string.Join('\n', Texts(page)));
        SceneHarness.Close(window);
    }

    private static Control Tab(BloomNavPage page, int index) => page.GetControl<TabStrip>("Tabs").ContainerFromIndex(index)!;

    private static Border Shown(BloomNavPage page) => (Border)page.GetControl<TransitioningContentControl>("Stage").Content!;

    private static void AssertSettled(BloomNavPage page)
    {
        var presenters = page.GetControl<TransitioningContentControl>("Stage").GetVisualDescendants().OfType<ContentPresenter>().ToList();
        Assert.Same(Shown(page), Assert.Single(presenters, presenter => presenter.IsVisible).Content);
        Assert.All(presenters, presenter => Assert.Null(presenter.Clip));
    }

    private static string[] Texts(BloomNavPage page) =>
        [.. Shown(page).GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text ?? "")];
}
