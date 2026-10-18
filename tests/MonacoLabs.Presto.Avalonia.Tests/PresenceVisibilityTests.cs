using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Targets;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// An element's own <see cref="Visual.IsVisible"/> flip as a <see cref="Presence"/> moment (GAPS-2-PLAN §3): a hide plays
/// <see cref="Presence.ExitProperty"/> as <see cref="Presence.HideModeProperty"/> says, and a show plays the entrance.
/// </summary>
public sealed class PresenceVisibilityTests : CompositorTestBase
{
    private static AnimationBuilder FadeOut =>
        Presto.Build().Opacity(to: 0).X(to: 40).Duration(200).Ease("linear").WhenReduced(reduced => reduced.Opacity(to: 0));

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ABoundHidePlaysTheExitOnTheLiveElementInItsSlotAndThenHides(bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var (box, below) = ShowBox();
        var toggle = new Border();
        box.Bind(Visual.IsVisibleProperty, toggle.GetObservable(Visual.IsVisibleProperty));

        toggle.IsVisible = false;
        Frames.Advance(100, 16);
        Tick();

        Assert.True(box.IsVisible);
        Assert.InRange(box.Opacity, .3, .7);
        Assert.InRange(X(box), reduced ? 0 : 12, reduced ? 0 : 28);
        Assert.Equal(50, below.Bounds.Y);
        Assert.Empty(Ghosts(box));

        RunUntil(() => !box.IsVisible);
        Tick();

        Assert.Equal(1, box.Opacity);
        Assert.Equal(0d, X(box));
        Assert.Equal(0, below.Bounds.Y);
    }

    [AvaloniaFact]
    public void ItemLayoutClosesTheSlotOnceTheHeldExitEnds()
    {
        Border? red = null;
        var (list, _) = ReorderList(virtualized: false, itemLayout: true, decorate: row =>
        {
            if (row.Background != Brushes.Red) return;
            red = row;
            Presence.SetExit(row, FadeOut);
        });
        var lime = Extent(list, Colors.Lime)!.Value;

        red!.IsVisible = false;
        Frames.Advance(100, 16);
        Tick();
        Assert.Equal(lime, Extent(list, Colors.Lime));

        RunUntil(() => !red.IsVisible);
        Tick();
        var green = Row(list, Brushes.Lime);

        Assert.Equal(lime.Y - RowHeight, green.GetVisualParent()!.Bounds.Y);
        Assert.Equal(lime, Extent(list, Colors.Lime));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShownAgainMidExitPlaysBackFromWhereItStands(bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var (box, _) = ShowBox();
        box.IsVisible = false;
        Frames.Advance(100, 16);
        var opacity = box.Opacity;

        box.IsVisible = true;
        Frames.Advance(16);

        Assert.True(box.IsVisible);
        Assert.InRange(box.Opacity, opacity - .1, opacity + .1);

        RunUntil(() => box.Opacity == 1);
        Frames.Advance(16);
        Assert.Equal(0d, X(box));

        box.IsVisible = false;
        Assert.True(box.IsVisible);
        RunUntil(() => !box.IsVisible);
        Assert.Equal(1, box.Opacity);
    }

    [AvaloniaFact]
    public void HiddenAtLoadAndShownLaterEnters()
    {
        var (initial, entered) = (0, 0);
        var box = Box();
        box.IsVisible = false;
        Presence.SetInitial(box, Presto.Build().Opacity(from: 0).Duration(200).OnBegin(_ => initial++));
        Presence.SetEnter(box, Presto.Build().Opacity(from: 0).Duration(200).Ease("linear").OnBegin(_ => entered++));
        ShowWhite(box);
        Frames.Advance(300, 16);
        initial = 0;

        box.IsVisible = true;
        Frames.Advance(100, 16);

        Assert.Equal((0, 1), (initial, entered));
        Assert.InRange(box.Opacity, .3, .7);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void GhostModeClosesTheSlotAtOnceWhileAGhostExits(bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var (box, below) = ShowBox(PresenceHideMode.Ghost);
        var slot = Box(box);

        box.IsVisible = false;
        Tick();

        Assert.False(box.IsVisible);
        Assert.Equal(0, below.Bounds.Y);
        Assert.Single(Ghosts(box));
        Assert.Equal(slot, Box(box));

        RunUntil(() => Ghosts(box).Count == 0);
        Assert.Equal(1, box.Opacity);
    }

    [AvaloniaFact]
    public void GhostModeShownAgainMidExitDismissesTheGhostAndEnters()
    {
        var (box, _) = ShowBox(PresenceHideMode.Ghost);
        var entered = 0;
        Presence.SetEnter(box, Presto.Build().Opacity(from: 0).Duration(200).OnBegin(_ => entered++));
        box.IsVisible = false;
        Frames.Advance(50, 16);

        box.IsVisible = true;
        Frames.Advance(16);

        Assert.Empty(Ghosts(box));
        Assert.Equal(1, entered);
    }

    [AvaloniaTheory]
    [InlineData(PresenceHideMode.Hold)]
    [InlineData(PresenceHideMode.Ghost)]
    public void UnderReduceAllAHideAndAShowAreInstant(PresenceHideMode mode)
    {
        Presto.Defaults.ReduceAll = true;
        var (box, below) = ShowBox(mode);
        Presence.SetEnter(box, Presto.Build().Opacity(from: 0).Duration(200));

        box.IsVisible = false;
        Tick();

        Assert.False(box.IsVisible);
        Assert.Equal(0, below.Bounds.Y);
        Assert.Empty(Ghosts(box));

        box.IsVisible = true;
        Frames.Advance(16);

        Assert.True(box.IsVisible);
        Assert.Equal(1, box.Opacity);
    }

    [AvaloniaFact]
    public void DetachingMidHoldReleasesTheHoldAndCancelsTheExit()
    {
        var (box, below) = ShowBox();
        box.IsVisible = false;
        Frames.Advance(100, 16);

        ((Panel)box.Parent!).Children.Remove(box);
        Frames.Advance(200, 16);

        Assert.False(box.IsVisible);
        Assert.Equal(1, box.Opacity);
        Assert.Empty(Ghosts(below));
    }

    [AvaloniaFact]
    public void ClearingTheBuildersMidHoldReleasesTheHold()
    {
        var (box, _) = ShowBox();
        box.IsVisible = false;
        Frames.Advance(100, 16);

        Presence.SetExit(box, null);

        Assert.False(box.IsVisible);
        Assert.Equal(1, box.Opacity);
    }

    [AvaloniaFact]
    public void AnimationPriorityVisibilityHoldsPlayNothing()
    {
        var (box, _) = ShowBox();
        var entered = 0;
        var owner = new object();
        Presence.SetEnter(box, Presto.Build().Opacity(from: 0).Duration(200).OnBegin(_ => entered++));

        VisibilityHold.Hold(box, owner, false);
        Frames.Advance(16);
        Assert.False(box.IsVisible);
        VisibilityHold.Release(box, owner);
        Frames.Advance(16);

        Assert.True(box.IsVisible);
        Assert.Equal(1, box.Opacity);
        Assert.Equal(0, entered);
        Assert.Empty(Ghosts(box));
    }

    [AvaloniaFact]
    public void AnElementWithoutBuildersHidesAtOnce()
    {
        var (box, below) = ShowBox();
        Presence.SetExit(box, null);

        box.IsVisible = false;
        Tick();

        Assert.False(box.IsVisible);
        Assert.Equal(0, below.Bounds.Y);
    }

    [AvaloniaTheory]
    [InlineData(PresenceHideMode.Hold, false)]
    [InlineData(PresenceHideMode.Hold, true)]
    [InlineData(PresenceHideMode.Ghost, false)]
    [InlineData(PresenceHideMode.Ghost, true)]
    public void AnElementNotDrawnHidesAtOnceWithoutItsExit(PresenceHideMode mode, bool hiddenByParent)
    {
        var exited = 0;
        var (box, _) = ShowBox(mode, FadeOut.OnBegin(_ => exited++));
        if (hiddenByParent) ((Visual)box.GetVisualParent()!).IsVisible = false;
        else box.Opacity = 0;
        Tick();

        box.IsVisible = false;

        Assert.False(box.IsVisible);
        Frames.Advance(100, 16);
        Assert.Equal(0, exited);
        Assert.Empty(Ghosts(box));
    }

    [AvaloniaFact]
    public void AVisibilityFlipWhileDetachedPlaysNothing()
    {
        var (box, _) = ShowBox();
        Presence.SetEnter(box, Presto.Build().Opacity(from: 0).Duration(200));
        ((Panel)box.GetVisualParent()!).Children.Remove(box);

        box.IsVisible = false;

        Assert.False(box.IsVisible);
        Assert.Empty(Presto.GetAnimations(box));

        box.IsVisible = true;

        Assert.Empty(Presto.GetAnimations(box));
        Assert.Equal(1, box.Opacity);
    }

    private static (Border Box, Border Below) ShowBox(PresenceHideMode mode = PresenceHideMode.Hold, AnimationBuilder? exit = null)
    {
        var box = Box();
        Presence.SetExit(box, exit ?? FadeOut);
        Presence.SetHideMode(box, mode);
        var below = new Border { Height = 20, Background = Brushes.Blue };
        ShowWhite(new StackPanel { Children = { box, below } });
        return (box, below);
    }
}
