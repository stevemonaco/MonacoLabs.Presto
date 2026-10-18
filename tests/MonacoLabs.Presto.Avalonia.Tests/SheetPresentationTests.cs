using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Controls;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// A <see cref="Sheet"/> presented anchored: a 0/100/200 bottom sheet in a 200x200 window whose content opens in a flyout
/// at a button in the top left corner.
/// </summary>
public sealed class SheetPresentationTests : CompositorTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpeningAnAnchoredSheetShowsItsContentInAFlyoutAtTheAnchorAndLeavesThePanelHidden(bool ownFlyout)
    {
        var stage = Stage(ownFlyout ? new Flyout() : null);

        stage.Sheet.IsOpen = true;
        Tick();

        var presenter = stage.Content.FindAncestorOfType<FlyoutPresenter>();
        Assert.NotNull(presenter);
        Assert.NotNull(Extent(stage.Window, Colors.Lime));
        if (ownFlyout) Assert.Same(stage.Anchor, stage.Flyout!.Target);
        Assert.False(Panel(stage.Sheet).IsVisible);
        Assert.False(Strip(stage.Sheet).IsVisible);
        Assert.False(Scrim(stage.Sheet).IsHitTestVisible);
        Assert.Equal(1, stage.Sheet.DetentIndex);
        AssertSynced(stage.Sheet);
    }

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("escape")]
    [InlineData("hide")]
    [InlineData("isOpen")]
    public void ClosingTheFlyoutClosesTheSheetAndReturnsTheContentToThePanel(string path)
    {
        var stage = Stage(new Flyout());
        stage.Sheet.IsOpen = true;
        Tick();

        if (path == "isOpen") stage.Sheet.IsOpen = false;
        else Dismiss(stage.Window, stage.Content.FindAncestorOfType<FlyoutPresenter>()!, path, stage.Flyout!.Hide);
        Tick();

        Assert.False(stage.Flyout!.IsOpen);
        Assert.False(stage.Sheet.IsOpen);
        Assert.Same(Panel(stage.Sheet), stage.Content.Parent);
        Assert.False(Panel(stage.Sheet).IsVisible);
        AssertSynced(stage.Sheet);
    }

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("isOpen")]
    public void APopoverCloseOnTheFlyoutPlaysWithTheContentBeforeItReturns(string path)
    {
        var flyout = new Flyout();
        Popover.SetClose(flyout, Recipes.FadeOut(duration: 200, ease: "linear"));
        var stage = Stage(flyout);
        stage.Sheet.IsOpen = true;
        Tick();
        var presenter = stage.Content.FindAncestorOfType<FlyoutPresenter>()!;

        if (path == "isOpen") stage.Sheet.IsOpen = false;
        else Dismiss(stage.Window, presenter, path, flyout.Hide);
        Tick();
        Frames.Advance(100, 16);

        Assert.True(flyout.IsOpen);
        Assert.InRange(presenter.Opacity, .3, .7);
        Assert.Same(presenter, stage.Content.FindAncestorOfType<FlyoutPresenter>());
        Assert.NotNull(Extent(stage.Window, Colors.Lime));

        RunUntil(() => !flyout.IsOpen);

        Assert.False(stage.Sheet.IsOpen);
        Assert.Same(Panel(stage.Sheet), stage.Content.Parent);
        AssertSynced(stage.Sheet);
    }

    [AvaloniaFact]
    public void ReopenedWhileItsFlyoutClosesItShowsTheFlyoutAgainOnceClosed()
    {
        var flyout = new Flyout();
        Popover.SetClose(flyout, Recipes.FadeOut(duration: 200, ease: "linear"));
        var stage = Stage(flyout);
        var closed = 0;
        flyout.Closed += (_, _) => closed++;
        stage.Sheet.IsOpen = true;
        Tick();
        stage.Sheet.IsOpen = false;
        Frames.Advance(100, 16);

        stage.Sheet.IsOpen = true;
        RunUntil(() => closed == 1);

        Assert.True(flyout.IsOpen);
        Assert.True(stage.Sheet.IsOpen);
        Assert.NotNull(stage.Content.FindAncestorOfType<FlyoutPresenter>());
        Assert.False(Panel(stage.Sheet).IsVisible);
        AssertSynced(stage.Sheet);
    }

    [AvaloniaFact]
    public void ThePresentationDefaultsToTheEdge() => Assert.Equal(SheetPresentation.Edge, new Sheet().Presentation);

    [AvaloniaTheory]
    [InlineData("hidden")]
    [InlineData("null")]
    public void WithoutAVisibleAnchorItOpensAtItsEdge(string anchor)
    {
        var stage = Stage(new Flyout());
        if (anchor == "null") stage.Sheet.Anchor = null;
        else stage.Anchor.IsVisible = false;

        stage.Sheet.IsOpen = true;
        Tick();
        Frames.Advance(3000, 20);

        Assert.False(stage.Flyout!.IsOpen);
        Assert.Same(Panel(stage.Sheet), stage.Content.Parent);
        Assert.True(stage.Content.IsEffectivelyVisible);
        Assert.Equal(100, stage.Sheet.Position.Value, 1);
        AssertSynced(stage.Sheet);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void AnOpenSheetMovesBetweenItsEdgeAndItsFlyoutWhenThePresentationFlips(bool reduced, bool popover)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var flyout = new Flyout();
        if (popover) Popover.SetClose(flyout, Recipes.FadeOut(duration: 200, ease: "linear"));
        var stage = Stage(flyout);
        stage.Sheet.Presentation = SheetPresentation.Edge;
        stage.Sheet.DetentIndex = 2;
        Frames.Advance(3000, 20);
        Assert.Equal(200, stage.Sheet.Position.Value, 1);

        stage.Sheet.Presentation = SheetPresentation.Anchored;
        Tick();

        Assert.True(flyout.IsOpen);
        Assert.NotNull(stage.Content.FindAncestorOfType<FlyoutPresenter>());
        Assert.False(Panel(stage.Sheet).IsVisible);
        Assert.Equal(0, stage.Sheet.Position.Value, 1);
        Assert.Equal((1, true), (stage.Sheet.DetentIndex, stage.Sheet.IsOpen));

        stage.Sheet.Presentation = SheetPresentation.Edge;
        Tick();
        RunUntil(() => !flyout.IsOpen);
        stage.Window.UpdateLayout();

        Assert.Same(Panel(stage.Sheet), stage.Content.Parent);
        Assert.True(Panel(stage.Sheet).IsVisible);
        Assert.Equal(100, stage.Sheet.Position.Value, 1);
        Assert.True(Scrim(stage.Sheet).IsHitTestVisible);
        Assert.Equal((1, true), (stage.Sheet.DetentIndex, stage.Sheet.IsOpen));
        Frames.Advance(3000, 20);
        Assert.Equal(100, stage.Sheet.Position.Value, 1);
    }

    [AvaloniaFact]
    public void EscapeOnAnAnchoredSheetIsLeftToItsFlyout()
    {
        var stage = Stage(new Flyout());
        var modal = new Modal { Content = new Border() };
        ((Panel)stage.Window.Content!).Children.Add(modal);
        modal.IsOpen = true;
        stage.Sheet.IsOpen = true;
        Tick();
        var escape = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = stage.Window };

        stage.Window.RaiseEvent(escape);

        Assert.False(escape.Handled);
        Assert.True(modal.IsOpen);
        Assert.True(stage.Sheet.IsOpen);

        Dismiss(stage.Window, stage.Content.FindAncestorOfType<FlyoutPresenter>()!, "escape", stage.Flyout!.Hide);
        Tick();

        Assert.False(stage.Sheet.IsOpen);
        Assert.True(modal.IsOpen);
        AssertSynced(stage.Sheet);
    }

    [AvaloniaFact]
    public void AnAnchoredSheetWithASingleDetentStaysClosed()
    {
        var stage = Stage(new Flyout());
        stage.Sheet.Detents = SheetDetents.Parse("0");

        stage.Sheet.IsOpen = true;
        Tick();

        Assert.False(stage.Flyout!.IsOpen);
        Assert.Same(Panel(stage.Sheet), stage.Content.Parent);
        Assert.Equal(0, stage.Sheet.DetentIndex);
        AssertSynced(stage.Sheet);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AStyleHidingTheAnchorWhileOpenMovesTheSheetToItsEdge(bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var stage = Stage(new Flyout());
        stage.Window.Styles.Add(new Style(x => x.OfType<Panel>().Class("compact").Child().OfType<Button>()) { Setters = { new Setter(Visual.IsVisibleProperty, false) } });
        stage.Sheet.IsOpen = true;
        Tick();

        ((Panel)stage.Window.Content!).Classes.Add("compact");
        stage.Window.UpdateLayout();

        Assert.False(stage.Flyout!.IsOpen);
        Assert.True(stage.Sheet.IsOpen);
        Assert.Same(Panel(stage.Sheet), stage.Content.Parent);
        Assert.Equal(100, stage.Sheet.Position.Value, 1);
        AssertSynced(stage.Sheet);
    }

    private sealed record Staged(Window Window, Button Anchor, Sheet Sheet, Flyout? Flyout, Border Content);

    private static void AssertSynced(Sheet sheet) => Assert.Equal(sheet.DetentIndex > 0, sheet.IsOpen);

    private static Border Scrim(Sheet sheet) => sheet.GetVisualChildren().OfType<Border>().First();

    private static Decorator Panel(Sheet sheet) => sheet.GetVisualChildren().OfType<Decorator>().Single(d => d is not Border);

    private static Border Strip(Sheet sheet) => sheet.GetVisualChildren().OfType<Border>().Last();

    private static Staged Stage(Flyout? flyout)
    {
        var (window, anchor) = PopupStage();
        var content = Lime();
        var sheet = new Sheet
        {
            Detents = SheetDetents.Parse("0,0.5,1"),
            EdgeSwipe = 20,
            Content = content,
            Presentation = SheetPresentation.Anchored,
            Anchor = anchor,
            Flyout = flyout,
        };
        ((Panel)window.Content!).Children.Add(sheet);
        Tick();
        return new(window, anchor, sheet, flyout, content);
    }
}
