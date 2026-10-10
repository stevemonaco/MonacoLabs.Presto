using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Covers what a layout transition puts on screen (COMPOSITOR-PLAN §4.4). Layout itself stays immediate, so the lag exists
/// only in the rendered frame and never in <see cref="Visual.Bounds"/>.
/// </summary>
/// <remarks>
/// The render thread reads its own clock, so the move on screen runs on the compositor clock that the pumps move on, while
/// the mirror that times the move rides the engine's manual frame source and only moves on <c>Frames.Advance</c>. A test
/// that needs the two in step says so; the rest hold the mirror still by moving over a minute.
/// </remarks>
public sealed class LayoutTransitionTests : CompositorTestBase
{
    private const double Shift = 40;
    private const double Growth = 20;
    private const double Duration = 800;

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AControlWithNoLiveTransitionJumpsInOneFrame(bool reverted)
    {
        var (spacer, bar) = ShowStack();
        if (reverted) bar.AnimateLayout(800, Eases.None).Revert();
        var start = Top(bar);

        spacer.Height += Shift;
        Tick();

        Assert.Equal(start + Shift, Top(bar));
    }

    /// <remarks>
    /// The halfway sample is what tells easing apart from a one-frame jump that merely has not been observed yet: a linear
    /// move is wherever elapsed time puts it, and the render thread reads its own clock, so its time is the one to count.
    /// </remarks>
    [AvaloniaFact]
    public void ALayoutMoveEasesIntoPlaceInsteadOfJumping()
    {
        var (spacer, bar) = ShowStack();
        _ = bar.AnimateLayout(Duration, Eases.None);
        var start = Top(bar);
        var laidOut = bar.Bounds.Top;

        spacer.Height += Shift;
        double? halfway = null;
        for (var elapsed = Frame; elapsed < Duration * .7; elapsed += Frame)
        {
            Tick(Frame);
            Assert.Equal(laidOut + Shift, bar.Bounds.Top);
            Assert.InRange(Top(bar), start, start + Shift - 2);
            if (elapsed > Duration / 2) halfway ??= Top(bar);
        }

        Assert.NotNull(halfway);
        Assert.InRange(halfway.Value, start + (Shift * .3), start + (Shift * .7));
        Assert.True(Pump(() => Top(bar) >= start + Shift - 1), "the control never caught up with where layout put it");
    }

    /// <summary>The guard on COMPOSITOR-PLAN §4.4 step 3: the invert has to reach the server in the commit that carries layout's new offset.</summary>
    [AvaloniaFact]
    public void TheFrameAfterALayoutChangeStillShowsTheOldPosition()
    {
        var (spacer, bar) = ShowStack();
        _ = bar.AnimateLayout(Held, Eases.None);
        var start = Top(bar);
        var laidOut = bar.Bounds.Top;

        spacer.Height += Shift;
        Tick();

        Assert.Equal(laidOut + Shift, bar.Bounds.Top);
        Assert.Equal(start, Top(bar));
    }

    /// <remarks>
    /// Advancing the mirror is what moves the control out of its slot here, since the rendered move runs on the compositor clock
    /// and has barely started; inverting from the stale slot instead would put the bar a whole <see cref="Shift"/> down.
    /// </remarks>
    [AvaloniaFact]
    public void ASecondLayoutChangeRetargetsFromWhereTheControlIsDrawn()
    {
        var (spacer, bar) = ShowStack();
        _ = bar.AnimateLayout(Duration, Eases.None);
        var start = Top(bar);

        spacer.Height += Shift;
        Tick();
        Frames.Advance(Duration / 2);
        spacer.Height += Shift;
        Tick();

        Assert.InRange(Top(bar), start + (Shift / 2) - 3, start + (Shift / 2) + 3);
    }

    [AvaloniaFact]
    public void ASecondTransitionReplacesTheFirst()
    {
        var (spacer, bar) = ShowStack();
        _ = bar.AnimateLayout(Held, Eases.None);
        _ = bar.AnimateLayout(150, Eases.None);
        var start = Top(bar);

        spacer.Height += Shift;

        Assert.True(Pump(() => Top(bar) >= start + Shift - 1, rounds: 40), "the replaced transition is still driving the move");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void EndingATransitionLeavesTheControlInItsLayoutSlot(bool reverted)
    {
        var (spacer, bar) = ShowStack();
        var transition = bar.AnimateLayout(Held, Eases.None);
        var start = Top(bar);

        spacer.Height += Shift;
        Tick();
        Assert.Equal(start, Top(bar));

        _ = reverted ? transition.Revert() : transition.Cancel();
        Tick();

        Assert.Equal(start + Shift, Top(bar));
        Assert.False(Pump(() => Top(bar) != start + Shift, rounds: 8), "the ended transition kept moving the control");
    }

    /// <inheritdoc cref="ALayoutMoveEasesIntoPlaceInsteadOfJumping" path="/remarks"/>
    [AvaloniaFact]
    public void AResizeUnderPositionAndSizeGrowsIntoPlace()
    {
        var (spacer, bar) = ShowStack();
        _ = bar.AnimateLayout(Duration, Eases.None, LayoutAnimateMode.PositionAndSize);
        var start = Box(bar);

        spacer.Height += Shift;
        bar.Width += Growth;
        bar.Height += Growth;
        var halfway = HalfwayBox(bar);

        Assert.InRange(halfway.Width, start.Width + (Growth * .2), start.Width + (Growth * .8));
        Assert.InRange(halfway.Height, start.Height + (Growth * .2), start.Height + (Growth * .8));

        Assert.True(Pump(() => Box(bar).Size == bar.Bounds.Size), "the bar never grew to its layout size");
    }

    [AvaloniaFact]
    public void AResizeUnderPositionAloneLandsAtItsNewSizeInOneFrame()
    {
        var (spacer, bar) = ShowStack();
        _ = bar.AnimateLayout(Held, Eases.None);
        var start = Box(bar);

        spacer.Height += Shift;
        bar.Width += Growth;
        bar.Height += Growth;
        Tick();

        var box = Box(bar);
        Assert.Equal(start.Top, box.Top);
        Assert.Equal(bar.Bounds.Width, box.Width);
        Assert.Equal(bar.Bounds.Height, box.Height);
    }

    /// <summary>
    /// COMPOSITOR-PLAN §4.4 step 6: moving an item into another container's slot is a shared element move, measured where
    /// it was and played where it lands.
    /// </summary>
    /// <inheritdoc cref="ALayoutMoveEasesIntoPlaceInsteadOfJumping" path="/remarks"/>
    [AvaloniaFact]
    public void ASharedElementMoveStartsWhereTheSourceWasAndLandsInTheTargetSlot()
    {
        var (left, right, bar) = ShowColumns();
        var start = Box(bar);

        _ = PrestoAvalonia.AnimateSharedElement(bar, bar, Duration, Eases.None);
        left.Children.Remove(bar);
        bar.Margin = new Thickness(0, 10, 0, 0);
        right.Children.Add(bar);
        Tick();

        Assert.Equal(start, Box(bar));

        var halfway = HalfwayBox(bar);

        Assert.InRange(halfway.Left, start.Left + (Shift * .3), start.Left + (Shift * .7));
        Assert.InRange(halfway.Top, start.Top - (Shift * .7), start.Top - (Shift * .3));
        Assert.True(
            Pump(() => Box(bar) == start.Translate(new Vector(Shift, -Shift))),
            "the bar never reached the slot its new parent gave it");
    }

    /// <summary>A dropped drag: the source is drawn off its slot by its render transform, and the move starts from there.</summary>
    [AvaloniaFact]
    public void ASharedElementMoveStartsWhereTheSourceIsDrawnByItsRenderTransform()
    {
        var (left, right, bar) = ShowColumns();
        _ = bar.Animate(new() { ["X"] = Growth, ["Y"] = Growth / 2, Duration = 100 });
        Frames.Advance(200);
        Tick();
        var drawn = Box(bar);

        _ = PrestoAvalonia.AnimateSharedElement(bar, bar, Held, Eases.None);
        left.Children.Remove(bar);
        right.Children.Add(bar);
        _ = PrestoAvalonia.Set(bar, new() { ["X"] = 0d, ["Y"] = 0d });
        Tick();

        Assert.Equal(drawn, Box(bar));
    }

    /// <summary>
    /// A 600 ms move interrupted halfway and re-armed as a shared element carries on from where the bar is drawn instead of
    /// jumping to the slot it was heading for, and with <c>once</c> off keeps easing the layout changes after it.
    /// </summary>
    /// <inheritdoc cref="ASecondLayoutChangeRetargetsFromWhereTheControlIsDrawn" path="/remarks"/>
    [AvaloniaFact]
    public void ReArmingAMoveMidFlightAsASharedElementCarriesOnFromWhereItIsDrawn()
    {
        var (spacer, bar) = ShowStack();
        _ = bar.AnimateLayout(600, Eases.None);
        var start = Top(bar);

        spacer.Height += Shift;
        Tick();
        Frames.Advance(300);
        var transition = PrestoAvalonia.AnimateSharedElement(bar, bar, 600, Eases.None, once: false);
        spacer.Height -= Shift;
        Tick();

        Assert.InRange(Top(bar), start + (Shift / 2) - 3, start + (Shift / 2) + 3);

        Frames.Advance(1200);
        spacer.Height += Shift;
        Tick();

        Assert.False(transition.Completed, "the shared element stopped easing layout changes");
    }

    [AvaloniaFact]
    public void ASharedElementOutOfAScrolledListStartsWhereItIsDrawn()
    {
        var (list, panel) = ShowScrolledList();
        var source = new Border { Background = Brushes.Blue, Width = 40, Height = 20 };
        list.Children.Insert(1, source);
        Tick();
        var drawn = Extent(source, Colors.Blue)!.Value;

        var target = new Border { Background = Brushes.Red, Width = 40, Height = 20 };
        _ = PrestoAvalonia.AnimateSharedElement(source, target, Held, Eases.None);
        list.Children.Remove(source);
        panel.Children.Add(target);
        Tick();

        Assert.Equal(drawn, Box(target));
    }

    [AvaloniaFact]
    public void ASharedElementIntoAScrolledListStartsWhereTheSourceIsDrawn()
    {
        var (list, panel) = ShowScrolledList();
        var source = new Border { Background = Brushes.Blue, Width = 40, Height = 20 };
        panel.Children.Add(source);
        Tick();
        var drawn = Extent(source, Colors.Blue)!.Value;

        var target = new Border { Background = Brushes.Red, Width = 40, Height = 20 };
        _ = PrestoAvalonia.AnimateSharedElement(source, target, Held, Eases.None);
        panel.Children.Remove(source);
        list.Children.Insert(1, target);
        Tick();

        Assert.Equal(drawn, Box(target));
    }

    [AvaloniaFact]
    public void AKeyedSharedElementFliesFromWhereItsSourceWasPreparedAfterTheSourceIsGone()
    {
        var (list, panel) = ShowScrolledList();
        var source = new Border { Background = Brushes.Blue, Width = 40, Height = 20 };
        list.Children.Insert(1, source);
        Tick();
        var drawn = Extent(source, Colors.Blue)!.Value;

        Assert.True(PrestoAvalonia.PrepareSharedElement("keyed", source));
        list.Children.Remove(source);
        Tick();
        var target = new Border { Background = Brushes.Red, Width = 40, Height = 20 };
        var transition = PrestoAvalonia.AnimateSharedElement("keyed", target, Held, Eases.None);
        panel.Children.Add(target);
        Tick();

        Assert.NotNull(transition);
        Assert.Equal(drawn, Box(target));
        Assert.Null(PrestoAvalonia.AnimateSharedElement("keyed", target, Held));
    }

    [AvaloniaFact]
    public void AKeyedSharedElementWithNothingPreparedReturnsNullAndLeavesTheTargetAlone()
    {
        var (_, panel) = ShowScrolledList();
        var target = new Border { Background = Brushes.Red, Width = 40, Height = 20 };

        Assert.False(PrestoAvalonia.PrepareSharedElement("detached", new Border()));
        Assert.Null(PrestoAvalonia.AnimateSharedElement("detached", target, Held));
        Assert.Null(PrestoAvalonia.AnimateSharedElement("never", target, Presto.Build().Duration(Held)));
        panel.Children.Add(target);
        Tick();

        Assert.Equal(SlotOf(target), Box(target));
    }

    [AvaloniaFact]
    public void ABuilderDelayHoldsTheControlWhereItWasBeforeItMoves()
    {
        var (left, right, bar) = ShowColumns();
        var start = Box(bar);

        _ = PrestoAvalonia.AnimateSharedElement(bar, bar, Presto.Build().Duration(50).Delay(Held).Ease(Eases.None));
        left.Children.Remove(bar);
        bar.Margin = new Thickness(0, 10, 0, 0);
        right.Children.Add(bar);
        Pump(() => false, 30);

        Assert.Equal(start, Box(bar));
    }

    /// <summary>X eased linearly and Y eased in: halfway through, X has covered half its way and Y only a quarter, off the straight line.</summary>
    /// <inheritdoc cref="ALayoutMoveEasesIntoPlaceInsteadOfJumping" path="/remarks"/>
    [AvaloniaFact]
    public void DifferentAxisEasesFlyTheSharedElementInAnArc()
    {
        var (left, right, bar) = ShowColumns();
        var start = Box(bar);

        _ = PrestoAvalonia.AnimateSharedElement(bar, bar, Presto.Build().Duration(Duration).X(ease: Eases.None).Y(ease: Eases.InQuad));
        left.Children.Remove(bar);
        bar.Margin = new Thickness(0, 10, 0, 0);
        right.Children.Add(bar);
        Tick();

        var halfway = HalfwayBox(bar);

        Assert.InRange(halfway.Left, start.Left + (Shift * .3), start.Left + (Shift * .7));
        Assert.InRange(halfway.Top, start.Top - (Shift * .45), start.Top - (Shift * .05));
        Assert.True(
            Pump(() => Box(bar) == start.Translate(new Vector(Shift, -Shift))),
            "the bar never reached the slot its new parent gave it");
    }

    [AvaloniaFact]
    public void ABuilderThatGivesALayoutAxisAValueOrSetsAnythingElseThrows()
    {
        var (_, bar) = ShowStack();

        Assert.Throws<NotSupportedException>(() => bar.AnimateLayout(Presto.Build().X(10)));
        Assert.Throws<NotSupportedException>(() => bar.AnimateLayout(Presto.Build().Y(from: 0, ease: Eases.OutQuad)));
        Assert.Throws<NotSupportedException>(() => bar.AnimateLayout(Presto.Build().Opacity(0.5)));
        Assert.Throws<NotSupportedException>(() => bar.AnimateLayout(Presto.Build().Loop()));
        Assert.Throws<NotSupportedException>(() => bar.AnimateLayout(Presto.Build().Delay(Presto.Stagger(10))));
    }

    [AvaloniaFact]
    public void ScrollingAnArmedControlIsNotALayoutMove()
    {
        var bar = new Border { Background = Brushes.Red, Width = 40, Height = 20, Margin = new Thickness(0, 50, 0, 150) };
        var scroller = new ScrollViewer { Content = bar };
        new Window { Content = scroller, Background = Brushes.White, Width = 100, Height = 100 }.Show();
        Tick();
        var transition = bar.AnimateLayout(Held, Eases.None);
        var start = Top(bar);

        scroller.Offset = new Vector(0, Shift);
        Tick();

        Assert.True(transition.Completed, "scrolling started a transition");
        Assert.Equal(start - Shift, Top(bar));
    }

    [AvaloniaFact]
    public void ALayoutPassDoesNotInvertAShorthandTranslation()
    {
        var (spacer, bar) = ShowStack();
        var transition = bar.AnimateLayout(Held, Eases.None);
        var start = Box(bar);

        _ = bar.Animate(new() { ["X"] = Shift, Duration = 100 });
        Frames.Advance(200);
        spacer.Width += Growth;
        Tick();

        Assert.True(transition.Completed, "the layout pass started a transition");
        Assert.Equal(start.Left + Shift, Box(bar).Left);
    }

    [AvaloniaFact]
    public async Task ThenResolvesWhenTheMoveCompletes()
    {
        var (spacer, bar) = ShowStack();
        var transition = bar.AnimateLayout(200, Eases.None);

        spacer.Height += Shift;
        Tick();
        Frames.Advance(400);

        Assert.Same(transition, await transition.Then());
        Assert.True(transition.Completed);
        Assert.Equal(1, transition.Progress);
    }

    [AvaloniaFact]
    public void ARecordFirstTransitionDoesNotEaseItsFirstPass()
    {
        var (spacer, bar) = ShowStack();
        var transition = new LayoutTransition(bar, null, false, new LayoutMotion(Held, Ease: Eases.None), LayoutAnimateMode.Position, once: false, recordFirst: true);
        var start = Top(bar);

        spacer.Height += Shift;
        Tick();

        Assert.True(transition.Completed, "the first pass started a transition");
        Assert.Equal(start + Shift, Top(bar));

        spacer.Height += Shift;
        Tick();

        Assert.Equal(start + Shift, Top(bar));
    }

    /// <remarks><see cref="MonacoLabs.Presto.Tests.EngineTestBase"/> gives every test fresh defaults, which resets <c>ReduceAll</c>.</remarks>
    [AvaloniaTheory]
    [InlineData(true, Held)]
    [InlineData(false, 0d)]
    public void AReducedOrZeroDurationMoveLandsInOneFrame(bool reduceAll, double duration)
    {
        var (spacer, bar) = ShowStack();
        Presto.Defaults.ReduceAll = reduceAll;
        var transition = bar.AnimateLayout(Presto.Build().Duration(duration).Delay(duration).Ease(Eases.None));
        var start = Top(bar);

        spacer.Height += Shift;
        Tick();

        Assert.Equal(start + Shift, Top(bar));
        Assert.True(transition.Completed);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADurationLayoutMoveLandsInOneFrameOnlyUnderReducedMotion(bool reduced)
    {
        var (spacer, bar) = ShowStack();
        Presto.Defaults.ReducedMotion = reduced;
        var transition = bar.AnimateLayout(Held, Eases.None);
        var start = Top(bar);

        spacer.Height += Shift;
        Tick();

        Assert.Equal(reduced ? start + Shift : start, Top(bar));
        Assert.Equal(reduced, transition.Completed);
    }

    [AvaloniaFact]
    public void APersistentDurationLayoutTransitionLandsOnceReducedMotionIsSet()
    {
        var (spacer, bar) = ShowStack();
        var transition = bar.AnimateLayout(Held, Eases.None);
        var start = Top(bar);
        spacer.Height += Shift;
        Tick();
        Assert.Equal(start, Top(bar));

        Presto.Defaults.ReducedMotion = true;
        spacer.Height += Shift;
        Tick();

        Assert.Equal(start + (2 * Shift), Top(bar));
        Assert.True(transition.Completed);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADurationSharedElementMoveLandsInOneFrameOnlyUnderReducedMotion(bool reduced)
    {
        var (left, right, bar) = ShowColumns();
        var start = Box(bar);
        Presto.Defaults.ReducedMotion = reduced;

        _ = PrestoAvalonia.AnimateSharedElement(bar, bar, Held, Eases.None);
        left.Children.Remove(bar);
        bar.Margin = new Thickness(0, 10, 0, 0);
        right.Children.Add(bar);
        Tick();

        Assert.Equal(reduced ? start.Translate(new Vector(Shift, -Shift)) : start, Box(bar));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADurationKeyedSharedElementLandsInOneFrameOnlyUnderReducedMotion(bool reduced)
    {
        var key = $"reduced-{reduced}";
        var (list, panel) = ShowScrolledList();
        var source = new Border { Background = Brushes.Blue, Width = 40, Height = 20 };
        list.Children.Insert(1, source);
        Tick();
        var drawn = Extent(source, Colors.Blue)!.Value;
        Assert.True(PrestoAvalonia.PrepareSharedElement(key, source));
        list.Children.Remove(source);
        Presto.Defaults.ReducedMotion = reduced;

        var target = new Border { Background = Brushes.Red, Width = 40, Height = 20 };
        Assert.NotNull(PrestoAvalonia.AnimateSharedElement(key, target, Held, Eases.None));
        panel.Children.Add(target);
        Tick();

        Assert.Equal(reduced ? SlotOf(target) : drawn, Box(target));
    }

    /// <summary>A red 40x20 bar under a spacer whose height is what moves it, in a 100x100 window ticked once so it has a composition visual.</summary>
    private static (Border Spacer, Border Bar) ShowStack()
    {
        var spacer = new Border { Width = 40, Height = 10 };
        var bar = new Border { Background = Brushes.Red, Width = 40, Height = 20 };
        new Window
        {
            Content = new StackPanel { Children = { spacer, bar } },
            Background = Brushes.White,
            Width = 100,
            Height = 100,
        }.Show();
        Tick();
        return (spacer, bar);
    }

    /// <summary>The same bar 50 down the left of two 40-wide columns, so that a move to the right one is <see cref="Shift"/> each way.</summary>
    private static (StackPanel Left, StackPanel Right, Border Bar) ShowColumns()
    {
        var bar = new Border { Background = Brushes.Red, Width = 40, Height = 20, Margin = new Thickness(0, 50, 0, 0) };
        var left = new StackPanel { Width = 40, Children = { bar } };
        var right = new StackPanel { Width = 40 };
        new Window
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { left, right } },
            Background = Brushes.White,
            Width = 100,
            Height = 100,
        }.Show();
        Tick();
        return (left, right, bar);
    }

    /// <summary>
    /// A list scrolled 100 down, whose first child is a 150-tall spacer so an item inserted after it is drawn 50 from the
    /// top on the left, under an unscrolled panel on the right. The panel lies over the list, as a detail view does, since
    /// a control flying into the list is clipped to the list's viewport until it lands.
    /// </summary>
    private static (StackPanel List, StackPanel Panel) ShowScrolledList()
    {
        var list = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, Children = { new Border { Height = 150 }, new Border { Height = 400 } } };
        var scroller = new ScrollViewer { Width = 120, Height = 200, Content = list };
        var panel = new StackPanel { Width = 40, Margin = new Thickness(0, 120, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        new Window
        {
            Content = new Grid { Width = 120, Height = 200, Children = { scroller, panel } },
            Background = Brushes.White,
            Width = 160,
            Height = 220,
        }.Show();
        Tick();
        scroller.Offset = new Vector(0, 100);
        Tick();
        return (list, panel);
    }

    /// <summary>The box the bar is rendered in the first frame past halfway through a <see cref="Duration"/> move, ticking on to 70% of it.</summary>
    private static Rect HalfwayBox(Visual bar)
    {
        TickFor(Duration / 2 + Frame);
        var halfway = Box(bar);
        TickFor(Duration * .2 - Frame);
        return halfway;
    }
}
