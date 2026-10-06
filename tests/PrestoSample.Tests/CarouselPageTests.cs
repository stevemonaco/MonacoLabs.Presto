using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Controls;
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

        SceneHarness.Click(window, page.GetControl<Button>("Next"));
        TestFrames.Tick(90);

        Assert.Equal(1, Position(page), 3);
        Assert.Equal([8, 24, 8, 8, 8], Dots(page).Select(dot => dot.Width));
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(4)]
    [InlineData(1)]
    [InlineData(2)]
    public void ClickingADotSlidesToIt(int dot)
    {
        var page = new CarouselPage();
        var window = SceneHarness.Show(page, 1100, 600);
        var at = SceneHarness.Centre(Dots(page)[dot], window);

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        TestFrames.Tick(90);

        Assert.Equal(dot, page.GetControl<Pager>("Pager").SelectedIndex);
        Assert.Equal(dot, ((Position(page) % 5) + 5) % 5, 3);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AutoplayAdvancesEveryFourSeconds()
    {
        var page = new CarouselPage();
        var window = SceneHarness.Show(page, 1100, 600);

        TestFrames.Tick(330);

        Assert.Equal(1, Position(page), 3);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionAutoplayAndTheNextArrowJumpASlideOn()
    {
        using var _ = ReducedMotion.On();
        var page = new CarouselPage();
        var window = SceneHarness.Show(page, 1100, 600);

        TestFrames.Tick(240);
        Assert.Equal(0, Position(page));
        TestFrames.Tick(20);

        Assert.Equal(1, Position(page));
        Assert.Equal(24, Dots(page)[1].Width);

        SceneHarness.Click(window, page.GetControl<Button>("Next"));

        Assert.Equal(2, Position(page));
        Assert.Equal(24, Dots(page)[2].Width);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AutoplayHoldsWhileThePointerIsOverTheStrip()
    {
        var page = new CarouselPage();
        var window = SceneHarness.Show(page, 1100, 600);
        window.MouseMove(SceneHarness.Centre(page.GetControl<Border>("Viewport"), window));

        TestFrames.Tick(300);

        Assert.Equal(0, Position(page));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AHardFlickCoastsPastSeveralSlidesAndLandsOnOne()
    {
        var page = new CarouselPage();
        var window = SceneHarness.Show(page, 1100, 600);
        var grab = SceneHarness.Centre(page.GetControl<Border>("Viewport"), window);

        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 4; i++)
        {
            window.MouseMove(grab - new Vector(150 * i, 0));
            TestFrames.Tick();
        }
        window.MouseUp(grab - new Vector(600, 0), MouseButton.Left);
        TestFrames.Tick(200);

        var slide = Position(page);
        Assert.Equal(Math.Round(slide), slide, 3);
        Assert.True(slide >= 2, $"the flick stopped at {slide}");
        Assert.Equal(24, Dots(page)[(int)Math.Round(slide) % 5].Width);
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

        var x = -Position(page) * page.GetControl<Pager>("Pager").Bounds.Width;
        Assert.InRange(x, -110, -50);
        var slide = (Panel)page.GetControl<Pager>("Pager").Slides[0];
        var content = (Grid)slide.Children[1];
        Assert.Equal(x * -.5, X(slide.Children[0]), 1);
        Assert.Equal(x * -.2, X(content.Children[1]), 1);
        Assert.Equal(x * .35, X(content.Children[0]), 1);
        window.MouseUp(grab - new Vector(100, 0), MouseButton.Left);
        SceneHarness.Close(window);
    }

    private static double X(Control control) => Convert.ToDouble(PrestoAvalonia.Get(control, "X"));

    private static double Position(CarouselPage page) => page.GetControl<Pager>("Pager").Position.Value;

    private static Control[] Dots(CarouselPage page) =>
        page.GetControl<PagerIndicator>("Dots").GetVisualChildren().Cast<Control>().ToArray();
}
