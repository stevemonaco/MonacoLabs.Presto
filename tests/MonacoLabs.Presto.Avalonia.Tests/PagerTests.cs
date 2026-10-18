using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Controls;

namespace MonacoLabs.Presto.Tests;

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

    [AvaloniaFact]
    public void ASelectedIndexPastTheSlidesIsClampedAndTheStripRestsOnTheLast()
    {
        var (_, pager) = Stage();

        pager.SelectedIndex = 7;
        Frames.Advance(3000, 20);

        Assert.Equal(3, pager.SelectedIndex);
        Assert.Equal(-600, StripX(pager), 1);
    }

    [AvaloniaFact]
    public void ALoopingSelectedIndexWrapsAndIsReCoercedWhenLoopChanges()
    {
        var (_, pager) = Stage();

        pager.SelectedIndex = -2;
        Assert.Equal(0, pager.SelectedIndex);
        pager.Loop = true;
        Assert.Equal(2, pager.SelectedIndex);
    }

    [AvaloniaFact]
    public void RemovingSlidesPastTheIndexReCoercesIt()
    {
        var (_, pager) = Stage();
        pager.GoTo(3);
        Frames.Advance(3000, 20);

        pager.Slides.RemoveAt(3);
        Frames.Advance(3000, 20);

        Assert.Equal(2, pager.SelectedIndex);
        Assert.Equal(2, pager.Position.Value, 3);
    }

    [AvaloniaFact]
    public void ASelectedIndexSetBeforeTheSlidesAreAddedSurvives()
    {
        var pager = new Pager { SelectedIndex = 2 };
        Assert.Equal(2, pager.SelectedIndex);

        foreach (var color in new[] { Colors.Red, Colors.Green, Colors.Blue, Colors.Yellow })
        {
            pager.Slides.Add(new Border { Background = new SolidColorBrush(color) });
        }

        Assert.Equal(2, pager.SelectedIndex);
    }

    [AvaloniaFact]
    public void ATwoWayBindingSourceGetsTheCoercedIndex()
    {
        var (_, pager) = Stage();
        var source = new Selection();
        pager.Bind(Pager.SelectedIndexProperty, new Binding(nameof(Selection.Index)) { Source = source });

        source.Index = 9;

        Assert.Equal(3, pager.SelectedIndex);
        Assert.Equal(3, source.Index);
    }

    [AvaloniaTheory]
    [InlineData("hover")]
    [InlineData("keyboard focus")]
    [InlineData("drag")]
    public void AutoplayAdvancesAndHolds(string hold)
    {
        var (window, pager) = Stage();
        pager.AutoplayInterval = 1000;

        Frames.Advance(500, 20);
        Assert.Equal(.5, pager.AutoplayProgress.Value, 1);
        Frames.Advance(600, 20);
        Assert.Equal(1, pager.SelectedIndex);

        if (hold == "hover") window.MouseMove(Center(pager, window));
        else if (hold == "drag") new Touch(window).Press(1, new Point(100, 100));
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

    [AvaloniaFact]
    public void AnInfiniteMeasureTakesTheLargestSlideAndAFiniteOneTheAvailableSize()
    {
        var pager = new Pager { Slides = { new Border { Width = 120, Height = 80 }, new Border { Width = 60, Height = 150 } } };

        pager.Measure(Size.Infinity);
        Assert.Equal(new Size(120, 150), pager.DesiredSize);

        pager.Measure(new Size(300, double.PositiveInfinity));
        Assert.Equal(new Size(300, 150), pager.DesiredSize);

        pager.Measure(new Size(300, 200));
        Assert.Equal(new Size(300, 200), pager.DesiredSize);
    }

    [AvaloniaFact]
    public void TurningLoopOnAndOffAtRuntimeReArrangesTheStripAndItsBounds()
    {
        var (window, pager) = Stage();

        pager.Loop = true;
        Tick();
        Assert.Equal(-200, pager.Slides[3].Bounds.X);
        new Touch(window).Drag(Frames, new Point(50, 100), new Vector(120, 0));
        Frames.Advance(3000, 20);
        Assert.Equal(3, pager.SelectedIndex);

        pager.GoTo(0);
        Frames.Advance(3000, 20);
        pager.Loop = false;
        Tick();
        Assert.Equal(600, pager.Slides[3].Bounds.X);
        new Touch(window).Drag(Frames, new Point(50, 100), new Vector(120, 0));
        Frames.Advance(3000, 20);
        Assert.Equal(0, pager.SelectedIndex);
        Assert.Equal(0, StripX(pager), 1);
    }

    [AvaloniaFact]
    public void GrabbingTheStripPausesTheTravel()
    {
        var (window, pager) = Stage();
        pager.GoTo(2);
        Frames.Advance(100, 20);
        var grabbedAt = pager.Position.Value;
        Assert.InRange(grabbedAt, .1, 1.9);

        new Touch(window).Press(1, new Point(100, 100));
        Frames.Advance(3000, 20);

        Assert.Equal(grabbedAt, pager.Position.Value, 6);
    }

    [AvaloniaFact]
    public void GoToWithNoSlidesDoesNothing()
    {
        var pager = new Pager();
        Show(pager);

        pager.GoTo(3);

        Assert.Equal(0, pager.SelectedIndex);
    }

    [AvaloniaTheory]
    [InlineData("detached")]
    [InlineData("zero wide")]
    public void ASelectionWhileDetachedOrZeroWideWaitsForTheNextPlacement(string how)
    {
        var (_, pager) = Stage();
        var panel = (Panel)pager.Parent!;
        if (how == "detached") panel.Children.Remove(pager);
        else pager.Width = 0;
        Tick();

        pager.SelectedIndex = 2;
        Frames.Advance(3000, 20);
        if (how == "zero wide") Assert.Equal(0, StripX(pager), 1);

        if (how == "detached") panel.Children.Add(pager);
        else pager.Width = double.NaN;
        Tick();

        Assert.Equal(-400, StripX(pager), 1);
    }

    [AvaloniaTheory]
    [InlineData("go to")]
    [InlineData("key")]
    [InlineData("indicator tap")]
    [InlineData("selected index")]
    [InlineData("drag")]
    public void PagingByHandRestartsTheAutoplayInterval(string how)
    {
        var (window, pager) = Stage();
        var indicator = Indicator(pager, PagerIndicatorMode.Progress);
        pager.AutoplayInterval = 1000;
        Frames.Advance(600, 20);
        Assert.InRange(pager.AutoplayProgress.Value, .5, .7);

        switch (how)
        {
            case "go to":
                pager.GoTo(2);
                break;
            case "key":
                Press(pager, Key.End);
                break;
            case "indicator tap":
                var at = new Point(120, indicator.Bounds.Center.Y);
                window.MouseDown(at, MouseButton.Left);
                window.MouseUp(at, MouseButton.Left);
                break;
            case "selected index":
                pager.SelectedIndex = 2;
                break;
            default:
                new Touch(window).Drag(Frames, new Point(150, 100), new Vector(-120, 0));
                break;
        }
        Assert.Equal(0, pager.AutoplayProgress.Value);

        Frames.Advance(600, 20);

        Assert.InRange(pager.AutoplayProgress.Value, .5, .7);
    }

    [AvaloniaTheory]
    [InlineData(NavigationMethod.Pointer)]
    [InlineData(NavigationMethod.Unspecified)]
    public void FocusByPointerOrCodeDoesNotHoldAutoplay(NavigationMethod method)
    {
        var (_, pager) = Stage();
        pager.AutoplayInterval = 1000;

        Assert.True(pager.Focus(method));
        Frames.Advance(1100, 20);

        Assert.Equal(1, pager.SelectedIndex);
    }

    [AvaloniaFact]
    public void DetachingPausesTheTravelCancelsAutoplayAndRevertsTheDraggable()
    {
        var (_, pager) = Stage();
        pager.AutoplayInterval = 1000;
        pager.GoTo(2);
        Frames.Advance(100, 20);
        var progress = pager.AutoplayProgress.Value;
        var strip = (Control)pager.GetVisualChildren().Single();

        ((Panel)pager.Parent!).Children.Remove(pager);
        Frames.Advance(3000, 20);

        for (var timer = Presto.Engine._head; timer != null; timer = timer._next) Assert.True(timer.Paused || timer.Canceled);
        Assert.Equal(progress, pager.AutoplayProgress.Value);
        Assert.Equal(2, pager.SelectedIndex);
        PrestoAvalonia.CreateDraggable(strip).Revert();
    }

    [AvaloniaFact]
    public void ABindingWithNoModeWritesPagingBackToTheSource()
    {
        var (_, pager) = Stage();
        var source = new Selection();
        pager.Bind(Pager.SelectedIndexProperty, new Binding(nameof(Selection.Index)) { Source = source });

        pager.GoTo(2);

        Assert.Equal(2, source.Index);
    }

    [AvaloniaFact]
    public void APagerDefaultsToFocusableClippedAndNotAutoplaying()
    {
        var (_, pager) = Stage();

        Assert.True(pager.Focusable);
        Assert.True(pager.ClipToBounds);
        Assert.Equal(0, pager.AutoplayInterval);
        Frames.Advance(5000, 20);
        Assert.Equal(0, pager.SelectedIndex);
        Assert.Equal(0, pager.AutoplayProgress.Value);
    }

    [AvaloniaFact]
    public void AnIndicatorDefaultsToWhiteDotsWithAHandCursorAndNoPager()
    {
        var indicator = new PagerIndicator();
        Show(indicator);

        Assert.Null(indicator.Pager);
        Assert.Empty(indicator.GetVisualChildren());
        Assert.Equal(PagerIndicatorMode.Dots, indicator.Mode);
        Assert.Equal(Colors.White, ((ISolidColorBrush)indicator.Fill).Color);
        Assert.Equal(1, indicator.Fill.Opacity);
        Assert.Equal(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF), ((ISolidColorBrush)indicator.Track).Color);
        Assert.Equal(nameof(StandardCursorType.Hand), indicator.Cursor!.ToString());
    }

    [AvaloniaFact]
    public void AProgressBarIsFourHighAndAsWideAsOfferedOrTwentyFourPerSlide()
    {
        var (_, pager) = Stage();
        var bar = new PagerIndicator { Pager = pager, Mode = PagerIndicatorMode.Progress };
        ((Panel)pager.Parent!).Children.Add(bar);
        Tick();

        bar.Measure(Size.Infinity);
        Assert.Equal(new Size(96, 4), bar.DesiredSize);

        bar.Measure(new Size(150, 100));
        Assert.Equal(new Size(150, 4), bar.DesiredSize);
    }

    [AvaloniaFact]
    public void ChangingTheLookOrTheSlidesRebuildsTheIndicatorAtRest()
    {
        var (_, pager) = Stage();
        var indicator = Indicator(pager, PagerIndicatorMode.Dots);
        pager.GoTo(2);
        Frames.Advance(40, 20);
        IBrush[] Backgrounds() => indicator.GetVisualChildren().Select(dot => ((Border)dot).Background!).ToArray();

        indicator.Fill = Brushes.Red;
        Assert.Equal([8, 8, 24, 8], indicator.GetVisualChildren().Select(dot => ((Control)dot).Width));
        Assert.Same(Brushes.Red, Backgrounds()[2]);

        indicator.Track = Brushes.Blue;
        Assert.Equal(new IBrush[] { Brushes.Blue, Brushes.Blue, Brushes.Red, Brushes.Blue }, Backgrounds());

        pager.Slides.Add(new Border());
        Assert.Equal([8, 8, 24, 8, 8], indicator.GetVisualChildren().Select(dot => ((Control)dot).Width));

        indicator.Mode = PagerIndicatorMode.Progress;
        Assert.Equal(new IBrush[] { Brushes.Blue, Brushes.Red }, Backgrounds());
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TappingAnIndicatorWithNoPagerOrNoSlidesDoesNothing(bool emptyPager)
    {
        var pager = new Pager();
        var indicator = new PagerIndicator { Width = 200, VerticalAlignment = VerticalAlignment.Bottom, Pager = emptyPager ? pager : null };
        var window = new Window { Content = new Panel { Children = { pager, indicator } }, Width = 200, Height = 200 };
        window.Show();
        Tick();
        bool? handled = null;
        window.AddHandler(InputElement.TappedEvent, (_, e) => handled = e.Handled, handledEventsToo: true);
        var at = new Point(100, indicator.Bounds.Center.Y);

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);

        Assert.False(handled);
        Assert.Equal(0, pager.SelectedIndex);
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

    private sealed class Selection : INotifyPropertyChanged
    {
        private int _index;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Index
        {
            get => _index;
            set
            {
                _index = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Index)));
            }
        }
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
