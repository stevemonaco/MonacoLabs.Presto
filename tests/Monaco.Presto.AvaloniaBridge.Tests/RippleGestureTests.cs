using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// What cancels a press ripple, the touch tap timeout inside a scroller, the state layer and reduced motion (Phase 62,
/// GAPS-2-PLAN §8.4).
/// </summary>
public sealed class RippleGestureTests : CompositorTestBase
{
    private static readonly Point InScroller = new(100, 100);

    [AvaloniaTheory]
    [InlineData("drag out")]
    [InlineData("scroll")]
    [InlineData("touch scroll")]
    public void DraggingOutOrScrollingCancelsAHeldRipple(string how)
    {
        var stage = ShowInScroller();
        var touch = new Touch(stage.Window);
        if (how == "touch scroll")
        {
            touch.Down(1, InScroller).Pointer.Capture(stage.Target);
            Run(10);
        }
        else
        {
            stage.Window.MouseDown(InScroller, MouseButton.Left);
            TickFor(32);
        }
        Assert.NotNull(Extent(stage.Target, Colors.Red));

        if (how == "drag out") stage.Window.MouseMove(new Point(100, 190));
        else if (how == "scroll") ScrollStage.ScrollTo(stage.Scroller, 260);
        else
        {
            touch.Move(1, InScroller - new Vector(0, 2));
            touch.Move(1, InScroller - new Vector(0, 30));
        }
        TickFor(250);

        Assert.Null(Extent(stage.Target, Colors.Red));
        Assert.Equal(0, Ripple.HandlerOf(stage.Target)!.Live);
    }

    [AvaloniaFact]
    public void ATouchFlingInAScrollerShowsNothing()
    {
        var stage = ShowInScroller();
        var touch = new Touch(stage.Window);

        touch.Down(1, InScroller).Pointer.Capture(stage.Target);
        touch.Move(1, InScroller - new Vector(0, 2));
        touch.Move(1, InScroller - new Vector(0, 30));
        for (var i = 0; i < 20; i++)
        {
            Run(1);
            Assert.Equal(0, Ripple.HandlerOf(stage.Target)!.Live);
        }
    }

    [AvaloniaFact]
    public void ATouchPressInAScrollerShowsOnlyAfterTheTapTimeout()
    {
        var stage = ShowInScroller();

        new Touch(stage.Window).Down(1, InScroller);
        Run(5);
        Assert.Equal(0, Ripple.HandlerOf(stage.Target)!.Live);
        Run(5);

        Assert.Equal(1, Ripple.HandlerOf(stage.Target)!.Live);
        Assert.NotNull(Extent(stage.Target, Colors.Red));
    }

    [AvaloniaFact]
    public void AQuickTouchTapInAScrollerStillRipplesAndFades()
    {
        var stage = ShowInScroller();
        var touch = new Touch(stage.Window);

        touch.Down(1, InScroller);
        Run(2);
        touch.Up(1, InScroller);
        Run(2);

        Assert.InRange(Row(stage.Target, 100)[100].G, 0, 128);
        Run(15);
        Assert.Equal(0, Ripple.HandlerOf(stage.Target)!.Live);
    }

    [AvaloniaFact]
    public void AMousePressInAScrollerShowsAtOnce()
    {
        var stage = ShowInScroller();

        stage.Window.MouseDown(InScroller, MouseButton.Left);
        TickFor(32);

        Assert.NotNull(Extent(stage.Target, Colors.Red));
    }

    /// <remarks>
    /// A key press ripples from the centre, so for a moment the corner shows the pressed tint alone; hover is 8 % of red
    /// over white and focus or press 10 %.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("hover", 235)]
    [InlineData("focus", 229)]
    [InlineData("press", 229)]
    public void TheStateLayerTintsTheHostWhileHoveredFocusedOrPressedAndClearsAfterwards(string state, byte tinted)
    {
        var host = RippleTests.ShowHost(new Size(160, 100));
        Ripple.SetStateLayer(host, true);
        host.Focusable = true;
        var other = new Border { Focusable = true, Height = 10 };
        ((Panel)host.Parent!).Children.Add(other);
        var window = WindowOf(host);
        Tick();

        switch (state)
        {
            case "hover":
                window.MouseMove(new Point(150, 90));
                break;
            case "focus":
                host.Focus(NavigationMethod.Tab);
                break;
            default:
                host.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = host });
                break;
        }
        TickFor(208);
        AssertNear(Color.FromRgb(255, tinted, tinted), Row(host, 94)[154]);

        switch (state)
        {
            case "hover":
                window.MouseMove(new Point(190, 190));
                break;
            case "focus":
                other.Focus(NavigationMethod.Tab);
                break;
            default:
                host.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Enter, Source = host });
                break;
        }
        TickFor(300);
        AssertNear(Colors.White, Row(host, 94)[154]);
        AssertNear(Colors.White, Row(host, 50)[80]);
    }

    [AvaloniaFact]
    public void UnderReducedMotionAPressShowsOnlyThePressedTintAtOnce()
    {
        Presto.Defaults.ReducedMotion = true;
        var host = RippleTests.ShowHost(new Size(160, 100));
        var window = WindowOf(host);

        window.MouseDown(new Point(60, 40), MouseButton.Left);
        Tick(Frame);

        Assert.Equal(0, Ripple.HandlerOf(host)!.Live);
        AssertNear(Color.FromRgb(255, 229, 229), Row(host, 40)[60]);
        AssertNear(Color.FromRgb(255, 229, 229), Row(host, 94)[154]);

        window.MouseUp(new Point(60, 40), MouseButton.Left);
        Tick(Frame);
        AssertNear(Colors.White, Row(host, 40)[60]);
    }

    /// <summary>A <see cref="ScrollStage"/> scrolled so its white host with red ink spans y 50 to 150 of the window.</summary>
    private static ScrollStage ShowInScroller()
    {
        var host = new Border { Background = Brushes.White };
        Ripple.SetEnabled(host, true);
        Ripple.SetColor(host, Colors.Red);
        var stage = ScrollStage.Show(target: host);
        ScrollStage.ScrollTo(stage.Scroller, ScrollStage.Lead - 50);
        Tick();
        return stage;
    }
}
