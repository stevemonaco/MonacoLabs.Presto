using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// <see cref="Animate.LayoutProperty"/> and <see cref="Animate.LayoutModeProperty"/> (DECLARATIVE-LAYOUT-PLAN §5): the
/// layout transition tied to attachment, observed in the rendered frame as <see cref="LayoutTransitionTests"/> does.
/// </summary>
/// <remarks>
/// A <see cref="Held"/> move gets nowhere while the test looks at it, so a control still drawn in its old slot the frame
/// after layout moved it is easing, and one drawn in its new slot has moved in one frame.
/// </remarks>
public sealed class AnimateLayoutAttachedTests : CompositorTestBase
{
    private const double Shift = 40;

    [AvaloniaFact]
    public void SettingItOnAnAttachedControlEasesAMarginChange()
    {
        var (_, bar) = ShowBarInPanel();
        Animate.SetLayout(bar, HeldMove);
        var start = Top(bar);

        MoveDown(bar);

        Assert.Equal(start, Top(bar));
    }

    [AvaloniaFact]
    public void DetachingRevertsAndReattachingDoesNotEaseTheArrival()
    {
        var (panel, bar) = ShowBarInPanel(HeldMove);
        var start = Top(bar);
        MoveDown(bar);
        Assert.Equal(start, Top(bar));

        panel.Children.Remove(bar);
        Tick();
        panel.Children.Insert(0, new Border { Height = Shift });
        panel.Children.Add(bar);
        Tick();

        Assert.Equal(SlotOf(bar), Box(bar));
        MoveDown(bar);
        Assert.Equal(start + (2 * Shift), Top(bar));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AControlAttachedWithStaleBoundsRecordsItsFirstPass(bool otherParent)
    {
        var (panel, bar) = ShowBarInPanel(HeldMove);
        var other = new StackPanel { Margin = new Thickness(50, 30, 0, 0) };
        panel.Children.Add(other);
        Tick();

        panel.Children.Remove(bar);
        bar.Margin = new Thickness(0, Shift, 0, 0);
        (otherParent ? other : panel).Children.Add(bar);
        Tick();

        Assert.Equal(SlotOf(bar), Box(bar));
    }

    [AvaloniaFact]
    public void ChangingTheBuilderMidMoveRevertsAndTheNextMoveUsesTheNewOne()
    {
        var (_, bar) = ShowBarInPanel(HeldMove);
        var start = Top(bar);
        MoveDown(bar);
        Assert.Equal(start, Top(bar));

        Animate.SetLayout(bar, Presto.Build().Duration(150).Ease(Eases.None));
        Tick();
        Assert.Equal(start + Shift, Top(bar));

        MoveDown(bar);
        Assert.True(Top(bar) < start + (2 * Shift), "the move with the new builder jumped");
        Assert.True(Pump(() => Top(bar) == start + (2 * Shift), rounds: 40), "the new builder's move never landed");

        Animate.SetLayout(bar, null);
        MoveDown(bar);
        Assert.Equal(start + (3 * Shift), Top(bar));
    }

    [AvaloniaFact]
    public void SwitchingToPositionAndSizeMakesAResizeScale()
    {
        var (_, bar) = ShowBarInPanel(HeldMove);
        var width = bar.Width;

        bar.Width += 20;
        Tick();
        Assert.Equal(width + 20, Box(bar).Width);

        Animate.SetLayoutMode(bar, LayoutAnimateMode.PositionAndSize);
        bar.Width += 20;
        Tick();

        Assert.Equal(width + 40, bar.Bounds.Width);
        Assert.Equal(width + 20, Box(bar).Width);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AConnectedFlightWinsItsMoveAndTheLayoutPropertyResumesAfterIt(bool layoutFirst)
    {
        // A row's window stays open, so sharing the key would make the next row's leaving fly from this row's arriving.
        var key = $"{nameof(AConnectedFlightWinsItsMoveAndTheLayoutPropertyResumesAfterIt)}{layoutFirst}";
        var root = new Panel();
        new Window { Content = root, Background = Brushes.White, Width = 160, Height = 220 }.Show();
        var leaving = Placed(new Border { Background = Brushes.Blue, Width = 40, Height = 20 }, 0, 50);
        Connected.SetKey(leaving, key);
        root.Children.Add(leaving);
        Tick();
        var drawn = Extent(root, Colors.Blue)!.Value;
        root.Children.Remove(leaving);

        var arriving = Placed(new Border { Background = Brushes.Red, Width = 40, Height = 20 }, 80, 130);
        if (layoutFirst) Animate.SetLayout(arriving, HeldMove);
        Connected.SetKey(arriving, key);
        Connected.SetMotion(arriving, HeldMove);
        if (!layoutFirst) Animate.SetLayout(arriving, HeldMove);
        root.Children.Add(arriving);
        Tick();
        Assert.Equal(drawn, Box(arriving));

        Frames.Advance(Held + 1);
        Assert.True(Pump(() => Box(arriving) == SlotOf(arriving), rounds: 8), "the layout property did not take over after the flight");

        var start = Top(arriving);
        arriving.Margin += new Thickness(0, Shift, 0, 0);
        Tick();
        Assert.Equal(start, Top(arriving));
    }

    [AvaloniaFact]
    public void ClearingAndRestoringTheSameBuilderMidFlightLeavesOneTransitionThatDetachReverts()
    {
        var key = nameof(ClearingAndRestoringTheSameBuilderMidFlightLeavesOneTransitionThatDetachReverts);
        var root = new Panel();
        new Window { Content = root, Background = Brushes.White, Width = 160, Height = 220 }.Show();
        var leaving = Placed(new Border { Background = Brushes.Blue, Width = 40, Height = 20 }, 0, 50);
        Connected.SetKey(leaving, key);
        root.Children.Add(leaving);
        Tick();
        root.Children.Remove(leaving);

        var layout = HeldMove;
        var arriving = Placed(new Border { Background = Brushes.Red, Width = 40, Height = 20 }, 80, 130);
        Animate.SetLayout(arriving, layout);
        Connected.SetKey(arriving, key);
        Connected.SetMotion(arriving, HeldMove);
        root.Children.Add(arriving);
        Tick();
        Animate.SetLayout(arriving, null);
        Animate.SetLayout(arriving, layout);

        // The restored arm re-arms at its next layout pass; the released one must not fill the gap before it.
        Frames.Advance(Held + 1);
        Pump(() => LayoutTransition.Live(arriving) is { Once: false }, rounds: 8);
        root.Children.Remove(arriving);
        Assert.False(LayoutTransition.Live(arriving) is { Once: false }, "a released arm re-armed after the flight and outlived the detach");

        root.Children.Add(arriving);
        Tick();
        Assert.True(LayoutTransition.Live(arriving) is { Once: false });
        root.Children.Remove(arriving);
        Assert.Null(LayoutTransition.Live(arriving));
    }

    [AvaloniaFact]
    public void AStyleSetterSharesOneReflowAndEachElementGetsItsOwnTransition()
    {
        var reflow = Recipes.Reflow(Held, ease: Eases.None);
        var spacer = new Border { Height = 10 };
        var red = new Border { Background = Brushes.Red, Width = 40, Height = 20, Classes = { "reflow" } };
        var blue = new Border { Background = Brushes.Blue, Width = 40, Height = 20, Classes = { "reflow" } };
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, Children = { spacer, red, blue } };
        var window = new Window { Content = panel, Background = Brushes.White, Width = 100, Height = 100 };
        window.Styles.Add(new Style(x => x.OfType<Border>().Class("reflow")) { Setters = { new Setter(Animate.LayoutProperty, reflow) } });
        window.Show();
        Tick();
        var (redStart, blueStart) = (Top(red), Extent(panel, Colors.Blue)!.Value.Top);

        spacer.Height += Shift;
        Tick();

        Assert.Equal(redStart, Top(red));
        Assert.Equal(blueStart, Extent(panel, Colors.Blue)!.Value.Top);
        Assert.NotNull(LayoutTransition.Live(red));
        Assert.NotSame(LayoutTransition.Live(red), LayoutTransition.Live(blue));
    }

    /// <remarks><see cref="Monaco.Presto.Tests.EngineTestBase"/> gives every test fresh defaults, which resets both flags.</remarks>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AReducedMoveIsInstant(bool reduceAll)
    {
        var (_, bar) = ShowBarInPanel();
        if (reduceAll) Presto.Defaults.ReduceAll = true;
        else Presto.Defaults.ReducedMotion = true;
        Animate.SetLayout(bar, reduceAll ? HeldMove : Recipes.Reflow(Held, ease: Eases.None));
        var start = Top(bar);

        MoveDown(bar);

        Assert.Equal(start + Shift, Top(bar));
    }

    [AvaloniaFact]
    public void FlippingReducedMotionRearmsAtTheNextLayoutPass()
    {
        var (_, bar) = ShowBarInPanel(Recipes.Reflow(Held, ease: Eases.None));
        var start = Top(bar);

        Presto.Defaults.ReducedMotion = true;
        Tick();
        MoveDown(bar);
        Assert.Equal(start + Shift, Top(bar));

        // The move whose layout pass detects the flip still runs on the old arm.
        Presto.Defaults.ReducedMotion = false;
        MoveDown(bar);
        MoveDown(bar);
        Assert.Equal(start + (2 * Shift), Top(bar));
    }

    public static TheoryData<AnimationBuilder> Unsupported => new() { Presto.Build().X(10), Presto.Build().Loop(2) };

    [AvaloniaTheory]
    [MemberData(nameof(Unsupported))]
    public void ABuilderALayoutTransitionCannotHonorThrowsWhenApplied(AnimationBuilder builder) =>
        Assert.Throws<NotSupportedException>(() => Animate.SetLayout(new Border(), builder));

    /// <summary>A red 40x20 bar at the top left of a 100x200 window, ticked once so it has a composition visual.</summary>
    private static (StackPanel Panel, Border Bar) ShowBarInPanel(AnimationBuilder? layout = null)
    {
        var bar = new Border { Background = Brushes.Red, Width = 40, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
        Animate.SetLayout(bar, layout);
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Children = { bar } };
        new Window { Content = panel, Background = Brushes.White, Width = 100, Height = 200 }.Show();
        Tick();
        return (panel, bar);
    }

    private static Border Placed(Border border, double x, double y)
    {
        border.HorizontalAlignment = HorizontalAlignment.Left;
        border.VerticalAlignment = VerticalAlignment.Top;
        border.Margin = new Thickness(x, y, 0, 0);
        return border;
    }

    private static void MoveDown(Control bar)
    {
        bar.Margin += new Thickness(0, Shift, 0, 0);
        Tick();
    }
}
