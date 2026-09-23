using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Covers what a layout transition puts on screen (COMPOSITOR-PLAN §4.4). Layout itself stays immediate, so the lag exists
/// only in the rendered frame and never in <see cref="Visual.Bounds"/>.
/// </summary>
/// <remarks>
/// The render thread reads its own clock, so the move on screen runs on wall time and <see cref="CompositorTestBase.Tick"/>
/// is what lets it pass, while the mirror that times the move rides the engine's manual frame source and only moves on
/// <c>Frames.Advance</c>. A test that needs the two in step says so; the rest hold the mirror still by moving over a minute.
/// </remarks>
public sealed class LayoutTransitionTests : CompositorTestBase
{
    private const double Shift = 40;
    private const double Growth = 20;
    private const double Duration = 800;

    /// <summary>Long enough that neither the rendered move nor the mirror gets anywhere while the test looks at them.</summary>
    private const double Held = 60000;

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
    /// move is wherever elapsed time puts it, and the render thread reads its own clock, so the wall time is the one to measure.
    /// </remarks>
    [AvaloniaFact]
    public async Task ALayoutMoveEasesIntoPlaceInsteadOfJumping()
    {
        var (spacer, bar) = ShowStack();
        _ = bar.AnimateLayout(Duration, Eases.None);
        var start = Top(bar);
        var laidOut = bar.Bounds.Top;

        spacer.Height += Shift;
        var at = Stopwatch.GetTimestamp();
        double? halfway = null;
        while (Stopwatch.GetElapsedTime(at).TotalMilliseconds < Duration * .7)
        {
            Tick();
            await Task.Delay(8);
            Assert.Equal(laidOut + Shift, bar.Bounds.Top);
            Assert.InRange(Top(bar), start, start + Shift - 2);
            if (Stopwatch.GetElapsedTime(at).TotalMilliseconds > Duration / 2) halfway ??= Top(bar);
        }

        Assert.NotNull(halfway);
        Assert.InRange(halfway.Value, start + (Shift * .3), start + (Shift * .7));
        Assert.True(await Pump(() => Top(bar) >= start + Shift - 1), "the control never caught up with where layout put it");
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
    /// Advancing the mirror is what moves the control out of its slot here, since the rendered move is wall-clock and has
    /// barely started; inverting from the stale slot instead would put the bar a whole <see cref="Shift"/> down.
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
    public async Task ASecondTransitionReplacesTheFirst()
    {
        var (spacer, bar) = ShowStack();
        _ = bar.AnimateLayout(Held, Eases.None);
        _ = bar.AnimateLayout(150, Eases.None);
        var start = Top(bar);

        spacer.Height += Shift;

        Assert.True(await Pump(() => Top(bar) >= start + Shift - 1, rounds: 40), "the replaced transition is still driving the move");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndingATransitionLeavesTheControlInItsLayoutSlot(bool reverted)
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
        Assert.False(await Pump(() => Top(bar) != start + Shift, rounds: 8), "the ended transition kept moving the control");
    }

    /// <inheritdoc cref="ALayoutMoveEasesIntoPlaceInsteadOfJumping" path="/remarks"/>
    [AvaloniaFact]
    public async Task AResizeUnderPositionAndSizeGrowsIntoPlace()
    {
        var (spacer, bar) = ShowStack();
        _ = bar.AnimateLayout(Duration, Eases.None, LayoutAnimateMode.PositionAndSize);
        var start = Box(bar);

        spacer.Height += Shift;
        bar.Width += Growth;
        bar.Height += Growth;
        var halfway = await HalfwayBox(bar);

        Assert.InRange(halfway.Width, start.Width + (Growth * .2), start.Width + (Growth * .8));
        Assert.InRange(halfway.Height, start.Height + (Growth * .2), start.Height + (Growth * .8));

        Assert.True(await Pump(() => Box(bar).Size == bar.Bounds.Size), "the bar never grew to its layout size");
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
    public async Task ASharedElementMoveStartsWhereTheSourceWasAndLandsInTheTargetSlot()
    {
        var (left, right, bar) = ShowColumns();
        var start = Box(bar);

        _ = PrestoAvalonia.AnimateSharedElement(bar, bar, Duration, Eases.None);
        left.Children.Remove(bar);
        bar.Margin = new Thickness(0, 10, 0, 0);
        right.Children.Add(bar);
        Tick();

        Assert.Equal(start, Box(bar));

        var halfway = await HalfwayBox(bar);

        Assert.InRange(halfway.Left, start.Left + (Shift * .3), start.Left + (Shift * .7));
        Assert.InRange(halfway.Top, start.Top - (Shift * .7), start.Top - (Shift * .3));
        Assert.True(
            await Pump(() => Box(bar) == start.Translate(new Vector(Shift, -Shift))),
            "the bar never reached the slot its new parent gave it");
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

    /// <summary>The box the bar is rendered in halfway through a <see cref="Duration"/> move, pumping the render thread until it gets there.</summary>
    private static async Task<Rect> HalfwayBox(Visual bar)
    {
        var at = Stopwatch.GetTimestamp();
        Rect? halfway = null;
        while (Stopwatch.GetElapsedTime(at).TotalMilliseconds < Duration * .7)
        {
            Tick();
            await Task.Delay(8);
            if (Stopwatch.GetElapsedTime(at).TotalMilliseconds > Duration / 2) halfway ??= Box(bar);
        }

        Assert.NotNull(halfway);
        return halfway.Value;
    }

    private static double Top(Visual bar) => Box(bar).Top;

    private static Rect Box(Visual bar) =>
        Extent(bar, Colors.Red) ?? throw new InvalidOperationException("The bar is not in the rendered frame.");
}
