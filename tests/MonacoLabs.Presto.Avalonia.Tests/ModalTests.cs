using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Controls;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Modal"/> (Phase 64c): a 100x100 dialog in a 200x200 window, over a backdrop with a 40x40 opener at its top left.
/// </summary>
public sealed class ModalTests : CompositorTestBase
{
    [AvaloniaFact]
    public void OpeningShowsTheScrimBlursTheBackdropAndRevealsFromTheOriginsCenter()
    {
        var (_, modal, opener) = Stage();

        modal.Open(opener);
        Tick();
        Frames.Advance(100, 20);

        Assert.True(Scrim(modal).IsEffectivelyVisible);
        Assert.InRange(Scrim(modal).Opacity, .05, .95);
        var circle = Assert.IsType<EllipseGeometry>(Panel(modal).Clip);
        Assert.Equal(new Point(20, 20), circle.Rect.Center);
        var early = circle.Rect.Width;
        Frames.Advance(200, 20);
        Assert.True(circle.Rect.Width > early, $"the circle stayed {early} wide");

        Frames.Advance(3000, 20);
        Assert.Equal(1, Scrim(modal).Opacity, 3);
        Assert.True(modal.Backdrop!.Effect is BlurEffect { Radius: > 5 }, $"the backdrop showed {modal.Backdrop.Effect}");
    }

    [AvaloniaTheory]
    [InlineData("escape")]
    [InlineData("back")]
    public void EscapeOrBackClosesIt(string how)
    {
        var (window, modal, _) = Opened();

        window.RaiseEvent(how == "escape"
            ? new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = window }
            : new RoutedEventArgs(TopLevel.BackRequestedEvent));
        Frames.Advance(3000, 20);

        Assert.False(modal.IsOpen);
        Assert.False(modal.Content!.IsEffectivelyVisible);
        Assert.Null(modal.Backdrop!.Effect);
    }

    [AvaloniaFact]
    public void AnOutsideClickClosesItAndAnInsideClickDoesNot()
    {
        var (window, modal, _) = Opened();
        var closed = 0;
        modal.Closed += (_, _) => closed++;

        Press(window, new Point(100, 100));
        Assert.True(modal.IsOpen);

        Press(window, new Point(190, 190));
        Assert.False(modal.IsOpen);
        Assert.Equal(1, closed);
    }

    [AvaloniaFact]
    public void FocusMovesIntoItAndReturnsToTheOpener()
    {
        var (window, modal, opener) = Stage();
        opener.Focus();

        modal.Open(opener);
        var focused = (Visual)window.FocusManager!.GetFocusedElement()!;
        Assert.True(modal.IsVisualAncestorOf(focused), $"{focused} had focus while the modal was open");

        modal.Close();
        Assert.Same(opener, window.FocusManager.GetFocusedElement());
    }

    [AvaloniaFact]
    public void TheClosedModalNeitherHitTestsNorShows()
    {
        var (window, modal, _) = Opened();
        var backdrop = modal.Backdrop!;

        modal.Close();
        Tick();
        Assert.True(Scrim(modal).IsVisible, "the scrim did not fade out");
        Assert.Same(backdrop, window.InputHitTest(new Point(190, 190)));

        Frames.Advance(3000, 20);
        Assert.False(Scrim(modal).IsVisible);
        Assert.False(Panel(modal).IsVisible);
        Assert.Same(backdrop, window.InputHitTest(new Point(100, 100)));
    }

    [AvaloniaFact]
    public void ReopeningMidCloseGrowsBackFromWhereTheCircleStands()
    {
        var (_, modal, opener) = Opened();
        modal.Close();
        Frames.Advance(Recipes.Tokens.Medium / 2, 20);
        var shrinking = Radius(modal);
        var full = Math.Sqrt((180 * 180) + (180 * 180));
        Assert.InRange(shrinking, 1, full - 1);

        modal.Open(opener);
        // A held hide sees the show on its next update, so one more frame of the exit plays first.
        for (var i = 0; i < 5; i++)
        {
            Frames.Advance(20);
            Assert.InRange(Radius(modal), shrinking / 2, full);
        }
        Assert.True(Radius(modal) > shrinking, $"the circle stood at {Radius(modal)} after reopening at {shrinking}");

        Frames.Advance(3000, 20);
        Assert.True(modal.IsOpen);
        Assert.True(Panel(modal).IsEffectivelyVisible);
        Assert.Equal(1, Scrim(modal).Opacity, 3);
        Assert.Null(Panel(modal).Clip);
    }

    [AvaloniaFact]
    public void OpeningAgainWhileOpenTakesTheNewOriginForTheClose()
    {
        var (_, modal, _) = Opened();

        modal.Open(new Point(150, 160));
        modal.Close();
        Frames.Advance(Recipes.Tokens.Medium / 2, 20);

        Assert.Equal(new Point(150, 160), ((EllipseGeometry)Panel(modal).Clip!).Rect.Center);
    }

    [AvaloniaFact]
    public void ABackdropReplacedWhileOpenUnblursAndTheNewOneBlurs()
    {
        var (window, modal, _) = Opened();
        var old = modal.Backdrop!;
        var replacement = new Border();
        ((Panel)window.Content!).Children.Insert(0, replacement);

        modal.Backdrop = replacement;
        Frames.Advance(3000, 20);

        Assert.Null(old.Effect);
        Assert.True(replacement.Effect is BlurEffect { Radius: > 5 }, $"the new backdrop showed {replacement.Effect}");
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheContentShowsAndHidesAtOnceAndTheScrimFades()
    {
        var (_, modal, opener) = Stage();
        Presto.Defaults.ReducedMotion = true;

        modal.Open(opener);
        Tick();
        Frames.Advance(40, 20);
        Assert.True(Panel(modal).IsEffectivelyVisible);
        Assert.Null(Panel(modal).Clip);
        Assert.InRange(Scrim(modal).Opacity, .1, .9);

        Frames.Advance(3000, 20);
        modal.Close();
        Tick();
        Assert.False(Panel(modal).IsVisible);
        Assert.True(Scrim(modal).IsVisible, "the scrim did not fade out");
    }

    [AvaloniaFact]
    public void AnIsOpenSetByAnIsOriginButtonsCommandRevealsFromTheButton()
    {
        var (window, modal, opener) = Stage();
        Modal.SetIsOrigin(opener, true);
        opener.Command = new TestCommand(_ => modal.IsOpen = true);

        Press(window, new Point(20, 20));
        Tick();
        Frames.Advance(100, 20);

        Assert.True(modal.IsOpen);
        Assert.Equal(new Point(20, 20), Assert.IsType<EllipseGeometry>(Panel(modal).Clip).Rect.Center);
    }

    [AvaloniaFact]
    public void AnOriginLessOpenInALaterJobShowsAtOnce()
    {
        var (window, modal, opener) = Stage();
        Modal.SetIsOrigin(opener, true);

        Press(window, new Point(20, 20));
        Dispatcher.UIThread.RunJobs();
        modal.Open();
        Tick();
        Frames.Advance(100, 20);

        Assert.True(Panel(modal).IsEffectivelyVisible);
        Assert.Null(Panel(modal).Clip);
    }

    [AvaloniaFact]
    public void AnIsOriginElementsTapIsTheOrigin()
    {
        var (window, modal, _) = Stage();
        var tile = new Border { Width = 40, Height = 40, Background = Brushes.Blue, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
        Modal.SetIsOrigin(tile, true);
        tile.Tapped += (_, _) => modal.Open();
        ((Panel)window.Content!).Children.Insert(1, tile);
        Tick();

        Press(window, new Point(180, 180));
        Tick();
        Frames.Advance(100, 20);

        Assert.Equal(new Point(180, 180), Assert.IsType<EllipseGeometry>(Panel(modal).Clip).Rect.Center);
    }

    [AvaloniaFact]
    public void OpeningFromAnElementUnderAnotherVisualRootMapsThroughTheScreen()
    {
        var (window, modal, _) = Stage();
        var inside = new Button { Width = 30, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var other = new Window { Content = inside, Width = 100, Height = 100 };
        window.Position = new PixelPoint(100, 50);
        other.Show();
        other.Position = new PixelPoint(130, 120);
        Tick();
        Assert.Null(inside.TranslatePoint(new Point(15, 10), modal));

        modal.Open(inside);
        Tick();
        Frames.Advance(100, 20);

        Assert.Equal(new Point(45, 80), Assert.IsType<EllipseGeometry>(Panel(modal).Clip).Rect.Center);
    }

    private static Border Scrim(Modal modal) => modal.GetVisualChildren().OfType<Border>().First();

    private static double Radius(Modal modal) => ((EllipseGeometry)Panel(modal).Clip!).Rect.Width / 2;

    private static Decorator Panel(Modal modal) => (Decorator)modal.Content!.GetVisualParent()!;

    private static (Window Window, Modal Modal, Button Opener) Stage()
    {
        var opener = new Button { Width = 40, Height = 40, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var backdrop = new Border { Background = Brushes.White, Child = opener };
        var modal = new Modal { Backdrop = backdrop, Content = new Border { Width = 100, Height = 100, Background = Brushes.Red } };
        var window = new Window { Content = new Panel { Children = { backdrop, modal } }, Width = 200, Height = 200 };
        window.Show();
        Tick();
        return (window, modal, opener);
    }

    private (Window Window, Modal Modal, Button Opener) Opened()
    {
        var staged = Stage();
        staged.Modal.Open(staged.Opener);
        Frames.Advance(3000, 20);
        Tick();
        return staged;
    }
}
