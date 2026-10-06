using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Controls;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary><see cref="Pager"/> and <see cref="PagerIndicator"/> (Phase 64a): four 200-wide slides in a 200x200 window, dragged by synthetic touch.</summary>
public sealed class PagerTests : CompositorTestBase
{
    [AvaloniaTheory]
    [InlineData(-120, 1)]
    [InlineData(-80, 0)]
    public void ADragReleasedPastHalfASlidePages(double by, int slide)
    {
        var (window, pager) = Stage();

        new Touch(window).Drag(Frames, new Point(150, 100), new Vector(by, 0));
        Frames.Advance(3000, 20);

        Assert.Equal(slide, pager.SelectedIndex);
        Assert.Equal(slide, pager.Position.Value, 3);
    }

    [AvaloniaFact]
    public void AFlingPages()
    {
        var (window, pager) = Stage();

        new Touch(window).Drag(Frames, new Point(150, 100), new Vector(-40, 0), steps: 2, hold: false);
        Frames.Advance(3000, 20);

        Assert.True(pager.SelectedIndex >= 1, $"the fling stayed on {pager.SelectedIndex}");
        Assert.Equal(pager.SelectedIndex, pager.Position.Value, 3);
    }

    [AvaloniaFact]
    public void TheBoundsFollowAResize()
    {
        var (window, pager) = Stage();
        pager.GoTo(3);
        Frames.Advance(3000, 20);

        window.Width = 300;
        Tick();
        Assert.Equal(-900, StripX(pager), 1);
        new Touch(window).Drag(Frames, new Point(250, 100), new Vector(-150, 0));
        Frames.Advance(3000, 20);

        Assert.Equal(3, pager.SelectedIndex);
        Assert.Equal(-900, StripX(pager), 1);
    }

    [AvaloniaFact]
    public void KeysPage()
    {
        var (_, pager) = Stage();

        Press(pager, Key.Right);
        Assert.Equal(1, pager.SelectedIndex);
        Press(pager, Key.End);
        Assert.Equal(3, pager.SelectedIndex);
        Press(pager, Key.Right);
        Assert.Equal(3, pager.SelectedIndex);
        Press(pager, Key.Left);
        Assert.Equal(2, pager.SelectedIndex);
        Frames.Advance(3000, 20);
        Assert.Equal(2, pager.Position.Value, 3);
        Press(pager, Key.Home);
        Assert.Equal(0, pager.SelectedIndex);
    }

    [AvaloniaFact]
    public void GoToSpringsAndSettlesOnTheIndex()
    {
        var (_, pager) = Stage();

        pager.GoTo(2);
        Frames.Advance(100, 20);

        Assert.Equal(2, pager.SelectedIndex);
        Assert.InRange(pager.Position.Value, .1, 1.9);
        Frames.Advance(3000, 20);
        Assert.Equal(2, pager.Position.Value, 3);
    }

    [AvaloniaFact]
    public void LoopingWrapsOnToTheFirstSlideAndBackToTheLast()
    {
        var (window, pager) = Stage();
        pager.Loop = true;

        for (var i = 0; i < 4; i++)
        {
            Press(pager, Key.Right);
            Frames.Advance(3000, 20);
        }
        Tick();

        Assert.Equal(0, pager.SelectedIndex);
        Assert.Equal(4, pager.Position.Value, 3);
        Assert.Equal(800, pager.Slides[0].Bounds.X);

        pager.GoTo(3);
        Frames.Advance(3000, 20);
        Assert.Equal(3, pager.Position.Value, 3);
        pager.GoTo(0);
        Frames.Advance(3000, 20);
        new Touch(window).Drag(Frames, new Point(50, 100), new Vector(120, 0));
        Frames.Advance(3000, 20);

        Assert.Equal(3, pager.SelectedIndex);
    }

    [AvaloniaTheory]
    [InlineData("hover")]
    [InlineData("keyboard focus")]
    public void AutoplayAdvancesAndHolds(string hold)
    {
        var (window, pager) = Stage();
        pager.AutoplayInterval = 1000;

        Frames.Advance(500, 20);
        Assert.Equal(.5, pager.AutoplayProgress.Value, 1);
        Frames.Advance(600, 20);
        Assert.Equal(1, pager.SelectedIndex);

        if (hold == "hover") window.MouseMove(Centre(pager, window));
        else pager.Focus(NavigationMethod.Tab);
        Frames.Advance(3000, 20);

        Assert.Equal(1, pager.SelectedIndex);
    }

    [AvaloniaFact]
    public void AutoplayRewindsFromTheLastSlideWithoutLooping()
    {
        var (_, pager) = Stage();
        pager.GoTo(3);
        pager.AutoplayInterval = 1000;

        Frames.Advance(1100, 20);

        Assert.Equal(0, pager.SelectedIndex);
    }

    [AvaloniaFact]
    public void TheParallaxValueTracksTheOffset()
    {
        var (window, pager) = Stage();
        pager.Loop = true;
        var (first, second, last) = (pager.OffsetOf(0), pager.OffsetOf(1), pager.OffsetOf(3));
        Assert.Equal(-1, last.Value, 3);

        var touch = new Touch(window);
        touch.Down(1, new Point(150, 100));
        for (var i = 1; i <= 5; i++)
        {
            Frames.Advance(20);
            touch.Move(1, new Point(150 - (10 * i), 100));
        }
        Frames.Advance(20);

        Assert.Equal(-.25, first.Value, 3);
        Assert.Equal(.75, second.Value, 3);
        Assert.Equal(-1.25, last.Value, 3);
        touch.Up(1, new Point(100, 100));
    }

    [AvaloniaFact]
    public void TheDotsWidenTheSelectedOne()
    {
        var (_, pager) = Stage();
        var dots = Indicator(pager, PagerIndicatorMode.Dots);
        Assert.Equal([24, 8, 8, 8], dots.GetVisualChildren().Select(dot => ((Control)dot).Width));

        pager.GoTo(2);
        Frames.Advance(3000, 20);

        Assert.Equal([8, 8, 24, 8], dots.GetVisualChildren().Select(dot => ((Control)dot).Width));
    }

    [AvaloniaTheory]
    [InlineData(PagerIndicatorMode.Dots, 52, 2)]
    [InlineData(PagerIndicatorMode.Dots, 35, 1)]
    [InlineData(PagerIndicatorMode.Progress, 120, 2)]
    public void TappingTheIndicatorSlidesToThatSlide(PagerIndicatorMode mode, double x, int slide)
    {
        var (window, pager) = Stage();
        var indicator = Indicator(pager, mode);
        var at = new Point(x, indicator.Bounds.Center.Y);

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Frames.Advance(100, 20);

        Assert.Equal(slide, pager.SelectedIndex);
        Assert.InRange(pager.Position.Value, .1, slide - .1);
        Frames.Advance(3000, 20);
        Assert.Equal(slide, pager.Position.Value, 3);
    }

    [AvaloniaFact]
    public void ThePointerOverADotIsOverTheIndicatorSoItGetsTheHandCursor()
    {
        var (window, pager) = Stage();
        var indicator = Indicator(pager, PagerIndicatorMode.Dots);

        Assert.Same(indicator, window.InputHitTest(new Point(52, indicator.Bounds.Center.Y)));
        Assert.Same(indicator, window.InputHitTest(new Point(44, indicator.Bounds.Center.Y)));
    }

    [AvaloniaFact]
    public void ATapJustAboveADotStillSelectsIt()
    {
        var (window, pager) = Stage();
        var indicator = Indicator(pager, PagerIndicatorMode.Dots);
        indicator.VerticalAlignment = VerticalAlignment.Center;
        Tick();
        var at = new Point(52, indicator.Bounds.Top - 6);

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);

        Assert.Equal(2, pager.SelectedIndex);
    }

    [AvaloniaFact]
    public void TheProgressBarFillsWithThePosition()
    {
        var (_, pager) = Stage();
        var bar = Indicator(pager, PagerIndicatorMode.Progress);

        pager.GoTo(1);
        Frames.Advance(3000, 20);
        Tick();

        Assert.Equal(100, bar.GetVisualChildren().Cast<Control>().Last().Bounds.Width, 1);
    }

    [AvaloniaFact]
    public void UnderReducedMotionPagingLandsAtOnceAndAutoplayStillPages()
    {
        var (window, pager) = Stage();
        var dots = Indicator(pager, PagerIndicatorMode.Dots);
        Presto.Defaults.ReducedMotion = true;

        pager.GoTo(2);
        Assert.Equal(2, pager.Position.Value, 3);
        Tick();
        Assert.Equal(24, ((Control)dots.GetVisualChildren().ElementAt(2)).Width);

        new Touch(window).Drag(Frames, new Point(150, 100), new Vector(120, 0));
        Assert.Equal(1, pager.SelectedIndex);
        Assert.Equal(1, pager.Position.Value, 3);

        pager.AutoplayInterval = 1000;
        Frames.Advance(1100, 20);
        Assert.Equal(2, pager.SelectedIndex);
        Assert.Equal(2, pager.Position.Value, 3);
        Assert.Equal(0, pager.AutoplayProgress.Value);
    }

    private static double StripX(Pager pager) => Convert.ToDouble(PrestoAvalonia.Get(pager.GetVisualChildren().Single(), "X"));

    private static void Press(Pager pager, Key key) =>
        pager.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = pager });

    private static PagerIndicator Indicator(Pager pager, PagerIndicatorMode mode)
    {
        var indicator = new PagerIndicator { Pager = pager, Mode = mode, Width = 200, VerticalAlignment = VerticalAlignment.Bottom };
        ((Panel)pager.Parent!).Children.Add(indicator);
        Tick();
        return indicator;
    }

    private static (Window Window, Pager Pager) Stage()
    {
        var pager = new Pager();
        foreach (var color in new[] { Colors.Red, Colors.Green, Colors.Blue, Colors.Yellow })
        {
            pager.Slides.Add(new Border { Background = new SolidColorBrush(color) });
        }
        var window = new Window { Content = new Panel { Children = { pager } }, Width = 200, Height = 200 };
        window.Show();
        Tick();
        return (window, pager);
    }
}
