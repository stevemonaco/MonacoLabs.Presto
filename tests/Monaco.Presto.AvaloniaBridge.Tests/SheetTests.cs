using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Controls;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// <see cref="Sheet"/> (Phase 63c): a bottom sheet in a 200x200 window with detents at 0, 100 and 200, dragged by
/// synthetic touch.
/// </summary>
public sealed class SheetTests : CompositorTestBase
{
    [AvaloniaFact]
    public void OpeningShowsTheContentAndPlaysTheEntrancesInside()
    {
        var row = new Border { Height = 20, Background = Brushes.Blue };
        Presence.SetEnter(row, Presto.Build().Opacity(from: 0).Duration(100).Ease("linear"));
        var (_, sheet) = Stage(new StackPanel { Children = { row } });
        Assert.False(row.IsEffectivelyVisible);

        sheet.IsOpen = true;
        Tick();
        Frames.Advance(50);

        Assert.True(row.IsEffectivelyVisible);
        Assert.InRange(row.Opacity, .3, .7);
        Frames.Advance(3000, 20);
        Assert.Equal(1, sheet.DetentIndex);
        Assert.Equal(100, sheet.Position.Value, 1);
    }

    [AvaloniaTheory]
    [InlineData(-30, 1)]
    [InlineData(-70, 2)]
    [InlineData(80, 0)]
    public void ADragReleasedBetweenDetentsSettlesOnTheNearest(double by, int detent)
    {
        var (window, sheet) = Opened();

        new Touch(window).Drag(Frames, new Point(100, 150), new Vector(0, by));
        Frames.Advance(3000, 20);

        Assert.Equal(detent, sheet.DetentIndex);
        Assert.Equal(100 * detent, sheet.Position.Value, 1);
        Assert.Equal(detent > 0, sheet.IsOpen);
    }

    [AvaloniaFact]
    public void AFlingSkipsToTheNextDetent()
    {
        var (window, sheet) = Opened();

        new Touch(window).Drag(Frames, new Point(100, 150), new Vector(0, -16), steps: 2, hold: false);
        Frames.Advance(3000, 20);

        Assert.Equal(2, sheet.DetentIndex);
        Assert.Equal(200, sheet.Position.Value, 1);
    }

    [AvaloniaFact]
    public void TheScrimOpacityTracksThePosition()
    {
        var (window, sheet) = Opened();
        Assert.Equal(.5, Scrim(sheet).Opacity, 3);

        var touch = new Touch(window);
        touch.Down(1, new Point(100, 150));
        for (var i = 1; i <= 5; i++)
        {
            Frames.Advance(20);
            touch.Move(1, new Point(100, 150 - (10 * i)));
        }
        Frames.Advance(20);

        Assert.Equal(150, sheet.Position.Value, 1);
        Assert.Equal(.75, Scrim(sheet).Opacity, 3);
        touch.Up(1, new Point(100, 100));
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(100)]
    public void AnInnerScrollerHandsTheDragOffAtItsTopAndScrollsOtherwise(double offset)
    {
        var scroller = new ScrollViewer { Content = new Border { Height = 1000, Background = Brushes.Blue } };
        var (window, sheet) = Stage(scroller);
        sheet.DetentIndex = 2;
        Frames.Advance(3000, 20);
        scroller.Offset = new Vector(0, offset);
        Tick();

        new Touch(window).Drag(Frames, new Point(100, 60), new Vector(0, 60));
        Frames.Advance(3000, 20);

        if (offset == 0)
        {
            Assert.Equal(1, sheet.DetentIndex);
            Assert.Equal(0, scroller.Offset.Y);
        }
        else
        {
            Assert.Equal(200, sheet.Position.Value, 1);
            Assert.True(scroller.Offset.Y < offset, $"the list stayed at {scroller.Offset.Y}");
        }
    }

    [AvaloniaTheory]
    [InlineData("escape")]
    [InlineData("back")]
    [InlineData("scrim")]
    public void EscapeBackOrAScrimTapClosesIt(string how)
    {
        var (window, sheet) = Opened();

        switch (how)
        {
            case "escape":
                window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = window });
                break;
            case "back":
                window.RaiseEvent(new RoutedEventArgs(TopLevel.BackRequestedEvent));
                break;
            default:
                Press(window, new Point(100, 40));
                break;
        }
        Frames.Advance(3000, 20);

        Assert.False(sheet.IsOpen);
        Assert.Equal(0, sheet.Position.Value, 1);
        Assert.False(sheet.Content!.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void ItFillsTheOverlayLayer()
    {
        var window = new Window { Width = 200, Height = 200 };
        window.Show();
        var sheet = new Sheet { Detents = SheetDetents.Parse("0,0.5"), Content = new Border { Background = Brushes.Red } };
        OverlayLayer.GetOverlayLayer(window)!.Children.Add(sheet);
        sheet.IsOpen = true;
        Tick();
        Frames.Advance(3000, 20);

        Assert.Equal(new Size(200, 200), sheet.Bounds.Size);
        Assert.Equal(100, sheet.Content!.TranslatePoint(default, window)!.Value.Y, 1);
    }

    [AvaloniaFact]
    public void UnderReducedMotionItLandsAtOnceAndTheScrimFades()
    {
        var (window, sheet) = Stage(new Border { Background = Brushes.Red });
        Presto.Defaults.ReducedMotion = true;

        sheet.IsOpen = true;
        Assert.Equal(100, sheet.Position.Value, 1);
        Frames.Advance(100, 20);
        Assert.InRange(Scrim(sheet).Opacity, .1, .4);
        Frames.Advance(Recipes.Tokens.Short);
        Assert.Equal(.5, Scrim(sheet).Opacity, 3);

        new Touch(window).Drag(Frames, new Point(100, 150), new Vector(0, -70));
        Assert.Equal(200, sheet.Position.Value, 1);
    }

    private static Border Scrim(Sheet sheet) => sheet.GetVisualChildren().OfType<Border>().First();

    private static (Window Window, Sheet Sheet) Stage(Control content)
    {
        var sheet = new Sheet { Detents = SheetDetents.Parse("0,0.5,1"), Content = content };
        var window = new Window { Content = new Panel { Children = { new Border { Background = Brushes.White }, sheet } }, Width = 200, Height = 200 };
        window.Show();
        Tick();
        return (window, sheet);
    }

    private (Window Window, Sheet Sheet) Opened()
    {
        var staged = Stage(new Border { Background = Brushes.Red });
        staged.Sheet.IsOpen = true;
        Frames.Advance(3000, 20);
        Tick();
        return staged;
    }
}
