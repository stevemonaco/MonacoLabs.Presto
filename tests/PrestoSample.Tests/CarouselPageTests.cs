using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
    public void UnderReducedMotionNothingAutoplaysButTheNextArrowStillSettlesOneSlideOn()
    {
        using var _ = ReducedMotion.On();
        var page = new CarouselPage();
        var window = SceneHarness.Show(page, 1100, 600);
        var width = page.GetControl<Border>("Viewport").Bounds.Width;
        var dots = page.GetControl<StackPanel>("Dots");

        TestFrames.Tick(330);

        Assert.Equal(0, StripX(page));
        Assert.Equal(24, dots.Children[0].Width);

        SceneHarness.Click(window, page.GetControl<Button>("Next"));
        TestFrames.Tick(330);

        Assert.Equal(-width, StripX(page), 1);
        Assert.Equal(24, dots.Children[1].Width);
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

    [AvaloniaFact]
    public void AHardFlickCoastsPastSeveralSlidesAndLandsOnOne()
    {
        var page = new CarouselPage();
        var window = SceneHarness.Show(page, 1100, 600);
        var width = page.GetControl<Border>("Viewport").Bounds.Width;
        var grab = SceneHarness.Centre(page.GetControl<Border>("Viewport"), window);

        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 4; i++)
        {
            window.MouseMove(grab - new Vector(150 * i, 0));
            TestFrames.Tick();
        }
        window.MouseUp(grab - new Vector(600, 0), MouseButton.Left);
        TestFrames.Tick(200);

        var slide = -StripX(page) / width;
        Assert.Equal(Math.Round(slide), slide, 3);
        Assert.InRange(slide, 2, 4);
        Assert.Equal(24, page.GetControl<StackPanel>("Dots").Children[(int)Math.Round(slide)].Width);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void DraggingTheStripMovesEachParallaxLayerAtItsOwnRate()
    {
        var page = new CarouselPage();
        var window = SceneHarness.Show(page, 1100, 600);
        var grab = SceneHarness.Centre(page.GetControl<Border>("Viewport"), window);

        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 10; i++)
        {
            window.MouseMove(grab - new Vector(10 * i, 0));
            TestFrames.Tick();
        }
        TestFrames.Tick(2);

        var x = StripX(page);
        Assert.InRange(x, -110, -50);
        var slide = (Panel)page.GetControl<StackPanel>("Strip").Children[0];
        var content = (Grid)slide.Children[1];
        Assert.Equal(x * -.5, X(slide.Children[0]), 1);
        Assert.Equal(x * -.2, X(content.Children[1]), 1);
        Assert.Equal(x * .35, X(content.Children[0]), 1);
        window.MouseUp(grab - new Vector(100, 0), MouseButton.Left);
        SceneHarness.Close(window);
    }

    private static double X(Control control) => Convert.ToDouble(PrestoAvalonia.Get(control, "X"));

    private static double StripX(CarouselPage page) =>
        Convert.ToDouble(PrestoAvalonia.Get(page.GetControl<StackPanel>("Strip"), "X"));
}
