using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// <see cref="Popover"/> on flyouts and context menus (GAPS-2-PLAN §7.2): Open plays on the presenter from its first
/// frame, and Close plays on it in a canceled <c>Closing</c> before the popup closes for real.
/// </summary>
public sealed class PopoverFlyoutTests : CompositorTestBase
{
    private static AnimationBuilder ScaleIn => Recipes.ScaleIn(duration: 200, ease: "linear");

    private static AnimationBuilder FadeOut => Recipes.FadeOut(duration: 200, ease: "linear");

    [AvaloniaTheory]
    [InlineData("flyout", false)]
    [InlineData("flyout", true)]
    [InlineData("menu", false)]
    [InlineData("menu", true)]
    [InlineData("context", false)]
    [InlineData("context", true)]
    public void OpenRendersItsStartValuesOnTheFirstFrame(string owner, bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var stage = Stage(owner);
        Popover.SetOpen(stage.Owner, ScaleIn);

        stage.Open();
        var presenter = stage.Presenter();

        Assert.Equal(0, presenter.Opacity);
        Assert.Equal(reduced ? 1 : Recipes.Tokens.Scale, Scale(presenter));
        Tick();
        Assert.Null(Extent(stage.Window, Colors.Lime));

        Frames.Advance(300, 16);
        Tick();

        Assert.Equal(1, presenter.Opacity);
        Assert.NotNull(Extent(stage.Window, Colors.Lime));
    }

    [AvaloniaTheory]
    [InlineData("flyout", "light")]
    [InlineData("flyout", "escape")]
    [InlineData("flyout", "hide")]
    [InlineData("menu", "light")]
    [InlineData("menu", "escape")]
    [InlineData("menu", "hide")]
    [InlineData("context", "light")]
    [InlineData("context", "escape")]
    [InlineData("context", "hide")]
    public void ClosePlaysOnTheLivePresenterAndThenCloses(string owner, string path)
    {
        var stage = Stage(owner);
        Popover.SetClose(stage.Owner, FadeOut);
        stage.Open();
        Tick();
        var presenter = stage.Presenter();

        Dismiss(stage.Window, presenter, path, stage.Close);
        Tick();
        Frames.Advance(100, 16);
        Dismiss(stage.Window, presenter, path, stage.Close);
        Frames.Advance(16);

        Assert.True(stage.IsOpen());
        Assert.InRange(presenter.Opacity, .3, .7);

        RunUntil(() => !stage.IsOpen());
        Tick();

        Assert.Null(Extent(stage.Window, Colors.Lime));
        Assert.Equal(1, presenter.Opacity);
    }

    [AvaloniaTheory]
    [InlineData("flyout")]
    [InlineData("menu")]
    [InlineData("context")]
    public void UnderReducedMotionCloseIsInstant(string owner)
    {
        Presto.Defaults.ReducedMotion = true;
        var stage = Stage(owner);
        Popover.SetClose(stage.Owner, FadeOut);
        stage.Open();
        Tick();

        stage.Close();
        Tick();

        Assert.False(stage.IsOpen());
        Assert.Null(Extent(stage.Window, Colors.Lime));
    }

    [AvaloniaTheory]
    [InlineData("flyout", false)]
    [InlineData("flyout", true)]
    [InlineData("menu", false)]
    [InlineData("menu", true)]
    [InlineData("context", false)]
    [InlineData("context", true)]
    public void AnAnchorDetachingClosesAtOnceAndRevertsTheClose(string owner, bool midClose)
    {
        var stage = Stage(owner);
        Popover.SetClose(stage.Owner, FadeOut);
        stage.Open();
        Tick();
        var presenter = stage.Presenter();
        if (midClose)
        {
            stage.Close();
            Frames.Advance(100, 16);
        }

        ((Panel)stage.Window.Content!).Children.Remove(stage.Anchor);
        Tick();

        Assert.False(stage.IsOpen());
        Assert.Null(Extent(stage.Window, Colors.Lime));
        Assert.Equal(1, presenter.Opacity);
    }

    [AvaloniaFact]
    public void AContextMenuRequestedAgainMidClosePlaysBackFromWhereItStands()
    {
        var stage = Stage("context");
        Popover.SetClose(stage.Owner, FadeOut);
        stage.Open();
        Tick();
        var menu = (ContextMenu)stage.Owner;
        menu.Close();
        Frames.Advance(100, 16);
        var opacity = menu.Opacity;

        stage.Anchor.RaiseEvent(new ContextRequestedEventArgs());
        Frames.Advance(16);

        Assert.True(menu.IsOpen);
        Assert.InRange(menu.Opacity, opacity, opacity + .15);

        RunUntil(() => menu.Opacity == 1);
        Frames.Advance(200, 16);
        Assert.True(menu.IsOpen);

        menu.Close();
        Frames.Advance(100, 16);
        Assert.True(menu.IsOpen);
        RunUntil(() => !menu.IsOpen);
        Assert.Equal(1, menu.Opacity);
    }

    /// <summary>A flyout cannot tell a reopen from a repeated close, so only a new target replays Open.</summary>
    [AvaloniaTheory]
    [InlineData("flyout", false)]
    [InlineData("flyout", true)]
    [InlineData("menu", false)]
    [InlineData("menu", true)]
    public void AShowAtMidCloseOnTheSameTargetLetsCloseFinishAndOnAnotherReplaysOpen(string owner, bool otherTarget)
    {
        var stage = Stage(owner);
        var flyout = (PopupFlyoutBase)stage.Owner;
        Popover.SetOpen(flyout, ScaleIn);
        Popover.SetClose(flyout, FadeOut);
        var other = new Button { Width = 60, Height = 30, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        ((Panel)stage.Window.Content!).Children.Add(other);
        stage.Open();
        Frames.Advance(300, 16);
        var presenter = stage.Presenter();
        stage.Close();
        Frames.Advance(100, 16);

        flyout.ShowAt(otherTarget ? other : stage.Anchor);

        Assert.True(flyout.IsOpen);
        if (otherTarget)
        {
            Assert.Same(other, flyout.Target);
            Assert.Equal(0, presenter.Opacity);
            Assert.Equal(Recipes.Tokens.Scale, Scale(presenter));
            Frames.Advance(300, 16);
            Assert.True(flyout.IsOpen);
            Assert.Equal(1, presenter.Opacity);
            return;
        }
        Frames.Advance(16);
        Assert.InRange(presenter.Opacity, .3, .7);
        RunUntil(() => !flyout.IsOpen);
        Assert.Equal(1, presenter.Opacity);
    }

    [AvaloniaTheory]
    [InlineData("flyout", false)]
    [InlineData("flyout", true)]
    [InlineData("menu", false)]
    [InlineData("menu", true)]
    [InlineData("context", false)]
    public void AStyleNarrowedByAPresenterClassSetsTheBuildersAndAValueOnTheFlyoutWins(string owner, bool onFlyout)
    {
        var stage = Stage(owner);
        var classes = stage.Owner switch
        {
            Flyout flyout => flyout.FlyoutPresenterClasses,
            MenuFlyout menu => menu.FlyoutPresenterClasses,
            _ => ((ContextMenu)stage.Owner).Classes,
        };
        classes.Add("pop");
        stage.Window.Styles.Add(new Style(x => owner switch
        {
            "flyout" => x.OfType<FlyoutPresenter>().Class("pop"),
            "menu" => x.OfType<MenuFlyoutPresenter>().Class("pop"),
            _ => x.OfType<ContextMenu>().Class("pop"),
        })
        {
            Setters = { new Setter(Popover.OpenProperty, ScaleIn), new Setter(Popover.CloseProperty, FadeOut) },
        });
        if (onFlyout) Popover.SetOpen(stage.Owner, Recipes.FadeLeft(duration: 200, ease: "linear"));

        stage.Open();
        var presenter = stage.Presenter();

        Assert.Equal(0, presenter.Opacity);
        Assert.Equal(onFlyout ? 1 : Recipes.Tokens.Scale, Scale(presenter));
        Assert.Equal(onFlyout ? Recipes.Tokens.Distance : 0, X(presenter));

        Frames.Advance(300, 16);
        stage.Close();
        Frames.Advance(100, 16);

        Assert.True(stage.IsOpen());
        Assert.InRange(presenter.Opacity, .3, .7);
        RunUntil(() => !stage.IsOpen());
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnderReduceAllCloseIsInstantAndOpenPlaysOnlyTheReducedFade(bool reducedMotion)
    {
        Presto.Defaults.ReduceAll = true;
        Presto.Defaults.ReducedMotion = reducedMotion;
        var stage = Stage("flyout");
        Popover.SetOpen(stage.Owner, ScaleIn);
        Popover.SetClose(stage.Owner, FadeOut);

        stage.Open();
        var presenter = stage.Presenter();

        Assert.Equal(reducedMotion ? 0 : 1, presenter.Opacity);
        Assert.Equal(1, Scale(presenter));

        stage.Close();
        Tick();

        Assert.False(stage.IsOpen());
        Assert.Null(Extent(stage.Window, Colors.Lime));
        Assert.Equal(1, presenter.Opacity);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnlyAnOwnerWithPopoverIsHandledAndHasItsPresentersOpacityTransitionLifted(bool set)
    {
        var stage = Stage("flyout");
        var fade = new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(150) };
        var width = new DoubleTransition { Property = Layoutable.WidthProperty, Duration = TimeSpan.FromMilliseconds(150) };
        stage.Window.Styles.Add(new Style(x => x.OfType<FlyoutPresenter>()) { Setters = { new Setter(Avalonia.Animation.Animatable.TransitionsProperty, new Transitions { fade, width }) } });
        if (set) Popover.SetOpen(stage.Owner, ScaleIn);
        var flyout = (PopupFlyoutBase)stage.Owner;
        var closings = 0;
        flyout.Closing += (_, _) => closings++;

        stage.Open();
        var presenter = stage.Presenter();

        Assert.Equal(set ? [width] : [fade, width], presenter.Transitions!);

        stage.Close();

        Assert.Equal(1, closings);
        Assert.False(stage.IsOpen());

        Popover.SetOpen(stage.Owner, null);
        stage.Open();

        Assert.Equal([fade, width], presenter.Transitions!);
    }

    private sealed record Staged(Window Window, Button Anchor, AvaloniaObject Owner, Action Open, Action Close, Func<bool> IsOpen, Func<Control> Presenter);

    private static double Scale(Visual visual) => Convert.ToDouble(PrestoAvalonia.Get(visual, "Scale"));

    private static Staged Stage(string owner)
    {
        var (window, anchor) = PopupStage();
        if (owner == "context")
        {
            var menu = new ContextMenu { Items = { new MenuItem { Header = Lime() } } };
            anchor.ContextMenu = menu;
            return new(window, anchor, menu, () => menu.Open(anchor), menu.Close, () => menu.IsOpen, () => menu);
        }
        PopupFlyoutBase flyout = owner == "flyout" ? new Flyout { Content = Lime() } : new MenuFlyout { Items = { new MenuItem { Header = Lime() } } };
        return new(window, anchor, flyout, () => flyout.ShowAt(anchor), flyout.Hide, () => flyout.IsOpen, () => flyout.Popup.Child!);
    }
}
