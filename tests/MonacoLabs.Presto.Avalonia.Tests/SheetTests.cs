using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Controls;

namespace MonacoLabs.Presto.Tests;

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
    public void ACrosswiseFirstMoveDropsThePressAndTheSheetStays()
    {
        var (window, sheet) = Opened();
        var touch = new Touch(window);

        touch.Press(1, new Point(100, 150));
        Frames.Advance(20);
        touch.Move(1, new Point(110, 148));
        for (var i = 1; i <= 3; i++)
        {
            Frames.Advance(20);
            touch.Move(1, new Point(110, 148 - (20 * i)));
        }
        touch.Up(1, new Point(110, 88));
        Frames.Advance(3000, 20);

        Assert.Equal(1, sheet.DetentIndex);
        Assert.Equal(100, sheet.Position.Value, 1);
    }

    [AvaloniaFact]
    public void AShorterTimeConstantSettlesSooner()
    {
        var (_, snappy) = Stage(new Border { Background = Brushes.Red });
        var (_, standard) = Stage(new Border { Background = Brushes.Red });
        snappy.TimeConstant = 150;

        snappy.IsOpen = true;
        standard.IsOpen = true;
        Tick();
        Frames.Advance(450, 20);

        Assert.InRange(snappy.Position.Value, 95, 100);
        Assert.InRange(standard.Position.Value, 50, 80);
        Frames.Advance(3000, 20);
        Assert.Equal(100, snappy.Position.Value, 1);
        Assert.Equal(100, standard.Position.Value, 1);
    }

    [AvaloniaFact]
    public void TheDefaultTimeConstantIs350() => Assert.Equal(350, new Sheet().TimeConstant);

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
    public void AnOpenSheetGetsBackAndEscapeBeforeAnEarlierTopLevelHandler()
    {
        var window = new Window { Width = 200, Height = 200 };
        var appSaw = 0;
        window.AddHandler(TopLevel.BackRequestedEvent, (_, e) => appSaw++);
        window.AddHandler(InputElement.KeyDownEvent, (_, e) => appSaw++);
        var sheet = new Sheet { Detents = SheetDetents.Parse("0,0.5"), Content = new Border() };
        window.Content = sheet;
        window.Show();
        sheet.IsOpen = true;

        window.RaiseEvent(new RoutedEventArgs(TopLevel.BackRequestedEvent));
        Assert.False(sheet.IsOpen);
        sheet.IsOpen = true;
        window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = window });
        Assert.False(sheet.IsOpen);
        Assert.Equal(0, appSaw);

        window.RaiseEvent(new RoutedEventArgs(TopLevel.BackRequestedEvent));
        Assert.Equal(1, appSaw);
    }

    [AvaloniaFact]
    public void EscapeClosesTheMostRecentlyOpenedOverlayFirst()
    {
        var sheet = new Sheet { Detents = SheetDetents.Parse("0,0.5"), Content = new Border() };
        var modal = new Modal { Content = new Border() };
        var window = new Window { Content = new Panel { Children = { sheet, modal } }, Width = 200, Height = 200 };
        window.Show();
        void Escape() => window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = window });

        modal.IsOpen = true;
        sheet.IsOpen = true;
        Escape();
        Assert.False(sheet.IsOpen);
        Assert.True(modal.IsOpen);

        sheet.IsOpen = true;
        modal.IsOpen = false;
        modal.IsOpen = true;
        Escape();
        Assert.False(modal.IsOpen);
        Assert.True(sheet.IsOpen);
    }

    [AvaloniaFact]
    public void OpeningASheetWithASingleDetentLeavesItClosed()
    {
        var (window, sheet) = Stage(new Border { Background = Brushes.Red });
        sheet.Detents = SheetDetents.Parse("0");

        sheet.IsOpen = true;
        Tick();

        Assert.False(sheet.IsOpen);
        Assert.Equal(0, sheet.DetentIndex);
        Assert.False(Scrim(sheet).IsHitTestVisible);
        Assert.NotSame(Scrim(sheet), window.InputHitTest(new Point(100, 40)));
    }

    [AvaloniaFact]
    public void TheDetentIndexIsCoercedIntoTheDeclaredDetents()
    {
        var sheet = new Sheet { Content = new Border() };

        sheet.DetentIndex = 5;
        Assert.Equal(1, sheet.DetentIndex);
        Assert.True(sheet.IsOpen);

        sheet.Detents = SheetDetents.Parse("0,0.5,1");
        sheet.DetentIndex = 2;
        sheet.Detents = SheetDetents.Parse("0,0.5");
        Assert.Equal(1, sheet.DetentIndex);
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
    public void TheEdgePseudoClassFollowsTheEdge()
    {
        var (window, sheet) = Stage(new Border());
        string[] edges = [":left", ":top", ":right", ":bottom"];
        Assert.Equal([":bottom"], edges.Where(sheet.Classes.Contains));

        sheet.Edge = Edge.Right;
        Assert.Equal([":right"], edges.Where(sheet.Classes.Contains));

        window.Styles.Add(new Style(x => x.OfType<Sheet>().Class("side")) { Setters = { new Setter(Sheet.EdgeProperty, Edge.Top) } });
        sheet.ClearValue(Sheet.EdgeProperty);
        sheet.Classes.Add("side");
        Tick();
        Assert.Equal([":top"], edges.Where(sheet.Classes.Contains));
    }

    [AvaloniaFact]
    public void AStyleOnTheBottomEdgeReachesTheContent()
    {
        var grab = new Border { Classes = { "grab" } };
        var (window, sheet) = Stage(new StackPanel { Children = { grab } });
        window.Styles.Add(new Style(x => x.OfType<Sheet>().Class(":bottom").Descendant().OfType<Border>().Class("grab"))
        {
            Setters = { new Setter(Border.BackgroundProperty, Brushes.Green) },
        });
        Tick();
        Assert.Same(Brushes.Green, grab.Background);

        sheet.Edge = Edge.Right;
        Assert.Null(grab.Background);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnOpenSheetFlippedToTheBottomByAStyleStaysOpenOnItsNewEdgeWithoutReplayingEntrances(bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var entered = 0;
        var row = new Border { Height = 20, Background = Brushes.Blue };
        Presence.SetEnter(row, Presto.Build().Opacity(from: 0).Duration(100).OnBegin(_ => entered++).WhenReduced(r => r.Opacity(from: 0)));
        var sheet = new Sheet { Detents = SheetDetents.Parse("0,0.5"), Content = new StackPanel { Children = { row } } };
        var host = new Panel { Children = { sheet } };
        host.Styles.Add(new Style(x => x.OfType<Sheet>()) { Setters = { new Setter(Sheet.EdgeProperty, Edge.Right) } });
        host.Styles.Add(new Style(x => x.OfType<Panel>().Class("compact").Child().OfType<Sheet>()) { Setters = { new Setter(Sheet.EdgeProperty, Edge.Bottom) } });
        var window = new Window { Content = host, Width = 300, Height = 200 };
        window.Show();
        Tick();
        sheet.IsOpen = true;
        Tick();
        Frames.Advance(3000, 20);
        Assert.Equal((150d, 1), (Math.Round(sheet.Position.Value, 1), entered));

        host.Classes.Add("compact");
        window.UpdateLayout();

        Assert.Equal(Edge.Bottom, sheet.Edge);
        Assert.True(sheet.IsOpen);
        Assert.Equal(100, sheet.Position.Value, 1);
        Assert.True(row.IsEffectivelyVisible);
        Tick();
        Frames.Advance(3000, 20);
        Assert.Equal(1, entered);
        Assert.Equal(100, sheet.Position.Value, 1);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnOpenSheetWhoseDetentsShrinkLandsOpenOnTheCoercedDetentAtOnce(bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var (window, sheet) = Stage(new Border { Background = Brushes.Red });
        sheet.DetentIndex = 2;
        Frames.Advance(3000, 20);

        sheet.Detents = SheetDetents.Parse("0,0.5");
        window.UpdateLayout();

        Assert.Equal((1, true), (sheet.DetentIndex, sheet.IsOpen));
        Assert.Equal(100, sheet.Position.Value, 1);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ChangingTheEdgeOrDetentsMidDragDropsTheDragAndALaterDragWorks(bool reduced, bool detents)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var (window, sheet) = Opened();
        var touch = new Touch(window);
        var pointer = touch.Press(1, new Point(100, 150));
        for (var i = 1; i <= 3; i++)
        {
            Frames.Advance(20);
            touch.Move(1, new Point(100, 150 - (10 * i)));
        }
        Assert.Same(sheet, pointer.Captured);

        if (detents) sheet.Detents = SheetDetents.Parse("0,0.5");
        else sheet.Edge = Edge.Right;
        window.UpdateLayout();

        Assert.Null(pointer.Captured);
        Assert.Equal(100, sheet.Position.Value, 1);
        touch.Move(1, new Point(100, 100));
        touch.Up(1, new Point(100, 100));
        Assert.Equal((1, 100d), (sheet.DetentIndex, Math.Round(sheet.Position.Value, 1)));

        if (detents) touch.Drag(Frames, new Point(100, 150), new Vector(0, 80));
        else touch.Drag(Frames, new Point(150, 100), new Vector(-70, 0));
        Frames.Advance(3000, 20);
        var landed = detents ? 0 : 2;
        Assert.Equal(landed, sheet.DetentIndex);
        Assert.Equal(100 * landed, sheet.Position.Value, 1);
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
