using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Monaco.Presto.AvaloniaBridge;
using CarouselPage = PrestoSample.Showcase.Pages.CarouselPage;

namespace PrestoSample.Tests;

public sealed class CarouselPageTests
{
    [AvaloniaFact]
    public void ThePageRunsAndRevertsItsScopeOnDetach()
    {
        var page = new CarouselPage();
        var reverted = false;
        page.Scope.Add(_ => () => reverted = true);
        var window = SceneHarness.Show(page, 1100, 600);
        TestFrames.Tick(60);

        SceneHarness.Close(window);

        Assert.True(reverted);
    }

    [AvaloniaFact]
    public void TheNextArrowSettlesTheStripOneSlideOnAndMarksItsDot()
    {
        var page = new CarouselPage();
        var window = SceneHarness.Show(page, 1100, 600);
        var width = page.GetControl<Border>("Viewport").Bounds.Width;

        SceneHarness.Click(window, page.GetControl<Button>("Next"));
        TestFrames.Tick(90);

        Assert.Equal(-width, StripX(page), 1);
        Assert.Equal([8, 24, 8, 8, 8], page.GetControl<StackPanel>("Dots").Children.Select(dot => dot.Width));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AutoplayAdvancesEveryFourSeconds()
    {
        var page = new CarouselPage();
        var window = SceneHarness.Show(page, 1100, 600);
        var width = page.GetControl<Border>("Viewport").Bounds.Width;

        TestFrames.Tick(330);

        Assert.Equal(-width, StripX(page), 1);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AutoplayHoldsWhileThePointerIsOverTheStrip()
    {
        var page = new CarouselPage();
        var window = SceneHarness.Show(page, 1100, 600);
        window.MouseMove(SceneHarness.Centre(page.GetControl<Border>("Viewport"), window));

        TestFrames.Tick(300);

        Assert.Equal(0, StripX(page));
        SceneHarness.Close(window);
    }

    private static double StripX(CarouselPage page) =>
        Convert.ToDouble(PrestoAvalonia.Get(page.GetControl<StackPanel>("Strip"), "X"));
}
