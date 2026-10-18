using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Controls;
using MonacoLabs.Presto.Transforms;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Marquee"/> in a 200x200 window, content 50 wide and the default 24 gap unless a test says otherwise. The motion
/// tests widen the gap to 950, a 1000 DIP period, so a frame's travel is read without a wrap.
/// </summary>
public sealed class MarqueeTests : CompositorTestBase
{
    [AvaloniaFact]
    public void TheContentIsMeasuredUnconstrainedAlongTheAxis()
    {
        var row = new WrapPanel();
        for (var i = 0; i < 10; i++) row.Children.Add(new Border { Width = 50, Height = 20 });
        Stage(new Marquee { Content = row });

        Assert.Equal(new Size(500, 20), row.DesiredSize);
        Assert.Equal(500, row.Bounds.Width);
    }

    [AvaloniaFact]
    public void ControlContentHasOneVisualParentAndCopiesStartAPeriodApart()
    {
        var content = Bar(80);
        var marquee = Stage(new Marquee { Content = content });
        var instances = Instances(marquee);

        Assert.Same(instances[0], content.GetVisualParent());
        Assert.Single(marquee.GetVisualDescendants(), v => v == content);
        Assert.Equal([0, 104, 208], instances.Select(i => i.Bounds.X));
    }

    [AvaloniaFact]
    public void TheCopyCountFollowsAResizeAndAGapChangeReusingTheCopies()
    {
        var marquee = Stage(new Marquee { Content = Bar(50) });
        var window = WindowOf(marquee);
        Assert.Equal(4, Instances(marquee).Count);
        var copy = Instances(marquee)[1];

        window.Width = 400;
        Tick();
        Assert.Equal(7, Instances(marquee).Count);
        Assert.Same(copy, Instances(marquee)[1]);

        marquee.Gap = 0;
        Tick();
        Assert.Equal(9, Instances(marquee).Count);
        Assert.Equal(50, Instances(marquee)[1].Bounds.X);
    }

    [AvaloniaFact]
    public void DataContentCopiesAreRealButtonsThatTakeClicks()
    {
        var clicks = 0;
        var marquee = Stage(new Marquee
        {
            Content = "Go",
            ContentTemplate = new FuncDataTemplate<string>((text, _) =>
            {
                var button = new Button { Content = text, Width = 60, Height = 40 };
                button.Click += (_, _) => clicks++;
                return button;
            }),
        });
        var copy = Instances(marquee)[1];
        var button = copy.GetVisualDescendants().OfType<Button>().Single();

        Assert.IsType<ContentPresenter>(copy);
        Assert.True(copy.IsHitTestVisible);
        Press(WindowOf(marquee), Center(button, WindowOf(marquee)));

        Assert.Equal(1, clicks);
    }

    [AvaloniaFact]
    public void ControlContentCopiesAreMirrorsThatTakeNoHits()
    {
        var marquee = Stage(new Marquee { Content = Bar(80) });

        var copy = Assert.IsType<Border>(Instances(marquee)[1]);
        var brush = Assert.IsType<VisualBrush>(copy.Background);
        Assert.Same(Instances(marquee)[0], brush.Visual);
        Assert.False(copy.IsHitTestVisible);
        Assert.Equal(new Size(80, 200), copy.Bounds.Size);
    }

    [AvaloniaFact]
    public void CopiesAreRawAndOutOfTheTabOrder()
    {
        var marquee = Stage(new Marquee
        {
            Content = "Go",
            ContentTemplate = new FuncDataTemplate<string>((text, _) => new Button { Content = text, Width = 60 }),
        });
        var instances = Instances(marquee);
        var primary = instances[0].GetVisualDescendants().OfType<Button>().Single();
        var copies = instances.Skip(1).SelectMany(c => c.GetVisualDescendants().OfType<Button>()).ToList();

        Assert.NotEqual(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(instances[0]));
        Assert.All(instances.Skip(1), c => Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(c)));
        Assert.All(instances.Skip(1), c => Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(c)));
        var window = WindowOf(marquee);
        primary.Focus();
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "\t");
        Assert.DoesNotContain(window.FocusManager!.GetFocusedElement(), copies);
        window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, "\t");
        Assert.DoesNotContain(window.FocusManager!.GetFocusedElement(), copies);
    }

    [AvaloniaFact]
    public void WhenOverflowingFittingContentIsShownOnceAlignedAndOverflowingContentGetsCopies()
    {
        var content = Bar(80);
        var marquee = Stage(new Marquee { Content = content, Mode = MarqueeMode.WhenOverflowing, HorizontalContentAlignment = HorizontalAlignment.Center });

        Assert.Single(Instances(marquee));
        Assert.Equal(60, Instances(marquee)[0].Bounds.X);

        content.Width = 300;
        Tick();
        Assert.Equal([0, 324], Instances(marquee).Select(i => i.Bounds.X));
    }

    [AvaloniaFact]
    public void FittingContentStretchedOnTheAxisFillsTheViewport()
    {
        var marquee = Stage(new Marquee { Content = Bar(80), Mode = MarqueeMode.WhenOverflowing, HorizontalContentAlignment = HorizontalAlignment.Stretch });

        Assert.Equal(new Rect(0, 0, 200, 200), Assert.Single(Instances(marquee)).Bounds);
    }

    [AvaloniaTheory]
    [InlineData(50, 50)]
    [InlineData(300, 200)]
    public void TheMarqueeMeasuresAsItsContentCappedAlongTheAxis(double width, double measured)
    {
        var marquee = Stage(new Marquee { Content = new Border { Width = width, Height = 20 }, HorizontalAlignment = HorizontalAlignment.Left });

        Assert.Equal(new Size(measured, 20), marquee.DesiredSize);
    }

    [AvaloniaFact]
    public void SwitchingBetweenControlAndDataContentReplacesTheCopies()
    {
        var marquee = Stage(new Marquee { Content = Bar(50), ContentTemplate = new FuncDataTemplate<string>((_, _) => Bar(50)) });

        marquee.Content = "data";
        Tick();
        Assert.All(Instances(marquee).Skip(1), c => Assert.IsType<ContentPresenter>(c));
        marquee.Content = Bar(50);
        Tick();

        Assert.All(Instances(marquee).Skip(1), c => Assert.IsType<Border>(c));
    }

    [AvaloniaFact]
    public void TheDefaults()
    {
        var marquee = new Marquee();

        Assert.Equal(
            (60d, MarqueeDirection.Left, 24d, 0d, true, false, MarqueeMode.Always),
            (marquee.Speed, marquee.Direction, marquee.Gap, marquee.FadeEdges, marquee.PauseOnHover, marquee.IsPaused, marquee.Mode));
        Assert.True(marquee.ClipToBounds);
    }

    [AvaloniaTheory]
    [InlineData(MarqueeDirection.Up, new double[] { 0, 74, 148, 222 })]
    [InlineData(MarqueeDirection.Down, new double[] { 0, -74, 74, 148 })]
    public void VerticalDirectionsLayCopiesOutDownTheStrip(MarqueeDirection direction, double[] tops)
    {
        var marquee = Stage(new Marquee { Content = new Border { Height = 50 }, Direction = direction });

        Assert.Equal(tops, Instances(marquee).Select(i => i.Bounds.Y));
        Assert.All(Instances(marquee), i => Assert.Equal(new Rect(0, i.Bounds.Y, 200, 50), i.Bounds));
    }

    [AvaloniaTheory]
    [InlineData(20, .1)]
    [InlineData(150, .5)]
    public void TheFadeRampIsCappedAtHalfTheViewport(double fade, double ramp)
    {
        var marquee = Stage(new Marquee { Content = Bar(50), FadeEdges = fade });

        var mask = Assert.IsType<LinearGradientBrush>(marquee.OpacityMask);
        Assert.Equal([0, ramp, 1 - ramp, 1], mask.GradientStops.Select(s => s.Offset));
        Assert.Equal([0, 255, 255, 0], mask.GradientStops.Select(s => (int)s.Color.A));
    }

    [AvaloniaTheory]
    [InlineData(TimeUnit.Milliseconds)]
    [InlineData(TimeUnit.Seconds)]
    public void ThePhaseAdvancesAtSpeedDipsPerSecondInEitherTimeUnit(TimeUnit unit)
    {
        Presto.Engine.TimeUnit = unit;
        try
        {
            var marquee = Moving(new Marquee { Content = Bar(50), Gap = 950 });

            Frames.Advance(1000, 16);

            Assert.Equal(-60, Offset(marquee), 3);
        }
        finally
        {
            Presto.Engine.TimeUnit = TimeUnit.Milliseconds;
        }
    }

    [AvaloniaFact]
    public void ThePhaseWrapsSeamlesslyAtThePeriod()
    {
        var marquee = Moving(new Marquee { Content = Bar(50) });

        Frames.Advance(1000, 16);
        Assert.Equal(-60, Offset(marquee), 3);
        Frames.Advance(500, 16);

        Assert.Equal(-90 + 74, Offset(marquee), 3);
    }

    [AvaloniaFact]
    public void ASpeedChangeEasesFromTheSpeedShown()
    {
        var marquee = Moving(new Marquee { Content = Bar(50), Gap = 950 });

        marquee.Speed = 120;
        Frames.Advance(100, 16);
        var midway = Velocity(marquee);
        Frames.Advance(500, 16);

        Assert.InRange(midway, 70, 110);
        Assert.Equal(120, Velocity(marquee), 3);
    }

    [AvaloniaFact]
    public void APauseDeceleratesThenStopsRequestingFrames()
    {
        var marquee = Moving(new Marquee { Content = Bar(50), Gap = 950 });

        marquee.IsPaused = true;
        Frames.Advance(100, 16);
        Assert.InRange(Velocity(marquee), 1, 55);
        RunUntil(() => !Presto.Engine.IsRunning);
        var rest = Offset(marquee);
        Frames.Advance(500, 16);

        Assert.False(Presto.Engine.IsRunning);
        Assert.Equal(rest, Offset(marquee));
        marquee.IsPaused = false;
        Assert.True(Presto.Engine.IsRunning);
    }

    [AvaloniaFact]
    public void ASameAxisDirectionFlipPassesThroughZeroAndKeepsThePhase()
    {
        var marquee = Moving(new Marquee { Content = Bar(50), Gap = 950 });
        Frames.Advance(500, 16);
        var before = Offset(marquee);

        marquee.Direction = MarqueeDirection.Right;
        Tick();
        Assert.Equal(before + 1000, Offset(marquee), 3);
        var velocities = new List<double>();
        for (var i = 0; i < 40; i++) velocities.Add(Velocity(marquee));

        Assert.True(velocities[0] < 0);
        Assert.Contains(velocities, v => Math.Abs(v) < 10);
        Assert.Equal(60, velocities[^1], 3);
    }

    [AvaloniaFact]
    public void AnAxisChangeResetsThePhase()
    {
        var marquee = Moving(new Marquee { Content = new Border { Width = 50, Height = 50 }, Gap = 950 });
        Frames.Advance(500, 16);

        marquee.Direction = MarqueeDirection.Up;
        Tick();

        Assert.Equal(0, Offset(marquee));
        Assert.Equal(0, Offset(marquee, TransformShorthand.Y));
        Frames.Advance(500, 16);
        Assert.Equal(0, Offset(marquee));
        Assert.True(Offset(marquee, TransformShorthand.Y) < 0);
    }

    [AvaloniaTheory]
    [InlineData("hover", true)]
    [InlineData("tab", true)]
    [InlineData("pointer", false)]
    [InlineData("programmatic", false)]
    public void HoverAndKeyboardFocusPauseWithoutWritingIsPaused(string hold, bool pauses)
    {
        var marquee = Moving(new Marquee
        {
            Content = "Go",
            Gap = 950,
            ContentTemplate = new FuncDataTemplate<string>((text, _) => new Button { Content = text, Width = 50 }),
        });
        var window = WindowOf(marquee);
        var button = Instances(marquee)[0].GetVisualDescendants().OfType<Button>().Single();

        switch (hold)
        {
            case "hover": window.MouseMove(Center(button, window)); break;
            case "tab": button.Focus(NavigationMethod.Tab); break;
            case "pointer": button.Focus(NavigationMethod.Pointer); break;
            default: button.Focus(); break;
        }
        Frames.Advance(1000, 16);

        Assert.False(marquee.IsPaused);
        if (pauses) Assert.Equal(0, Velocity(marquee));
        else Assert.Equal(60, Velocity(marquee), 3);
    }

    [AvaloniaFact]
    public void AnIsPausedBindingWithNoModeIsTwoWay()
    {
        var marquee = Stage(new Marquee { Content = Bar(50) });
        var toggle = new CheckBox();
        marquee.Bind(Marquee.IsPausedProperty, new Binding(nameof(CheckBox.IsChecked)) { Source = toggle });

        marquee.IsPaused = true;

        Assert.True(toggle.IsChecked);
    }

    [AvaloniaFact]
    public void WhenOverflowingRestsWhileTheContentFitsAndStartsFromZeroOnceItOverflows()
    {
        var marquee = new Marquee { Content = Bar(250), Mode = MarqueeMode.WhenOverflowing };
        var window = new Window { Content = marquee, Width = 300, Height = 200 };
        window.Show();
        Tick();
        Frames.Advance(500, 16);
        Assert.False(Presto.Engine.IsRunning);
        Assert.Equal(0, Offset(marquee));

        window.Width = 200;
        Tick();
        Assert.Equal(0, Offset(marquee));
        Frames.Advance(16);
        Frames.Advance(500, 16);
        Assert.Equal(-30, Offset(marquee), 3);

        window.Width = 300;
        Tick();
        Frames.Advance(100, 16);
        Assert.Equal(0, Offset(marquee));
        Assert.False(Presto.Engine.IsRunning);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnderReducedMotionTheMarqueeRestsAtZeroWithNoCopies(bool fromTheStart)
    {
        Presto.Defaults.ReducedMotion = fromTheStart;
        var marquee = Stage(new Marquee { Content = Bar(300) });
        Frames.Advance(500, 16);

        Presto.Defaults.ReducedMotion = true;
        Frames.Advance(16);
        Tick();
        Frames.Advance(500, 16);

        Assert.False(Presto.Engine.IsRunning);
        Assert.Equal(0, Offset(marquee));
        Assert.Equal(new Rect(0, 0, 300, 200), Assert.Single(Instances(marquee)).Bounds);
    }

    [AvaloniaFact]
    public void DetachAndReattachKeepThePhase()
    {
        var marquee = Moving(new Marquee { Content = Bar(50), Gap = 950 });
        var window = WindowOf(marquee);
        Frames.Advance(500, 16);
        var before = Offset(marquee);

        window.Content = null;
        Tick();
        Frames.Advance(1000, 16);
        Assert.Equal(before, Offset(marquee));
        window.Content = marquee;
        Tick();
        Frames.Advance(16);
        Assert.Equal(before, Offset(marquee));
        Frames.Advance(500, 16);

        Assert.Equal(before - 30, Offset(marquee), 3);
    }

    [AvaloniaTheory]
    [InlineData("hidden")]
    [InlineData("frozen")]
    public void HiddenAndFrozenHoldThePhaseAndResumeWithoutAJump(string hold)
    {
        var marquee = Moving(new Marquee { Content = Bar(50), Gap = 950 });
        Frames.Advance(500, 16);
        var before = Offset(marquee);

        if (hold == "hidden") Presto.Engine.Hidden = true;
        else Presto.Engine.Freeze();
        Frames.Advance(1000, 16);
        Assert.Equal(before, Offset(marquee));
        if (hold == "hidden") Presto.Engine.Hidden = false;
        else Presto.Engine.Thaw();
        Frames.Advance(500, 16);

        Assert.Equal(before - 30, Offset(marquee), 3);
    }

    [AvaloniaFact]
    public void APeriodChangeKeepsThePhaseModuloTheNewPeriod()
    {
        var marquee = Moving(new Marquee { Content = Bar(50), Gap = 950 });
        Frames.Advance(1000, 16);

        marquee.Gap = 0;
        Tick();

        Assert.Equal(-10, Offset(marquee), 3);
    }

    [AvaloniaTheory]
    [InlineData(0, 24)]
    [InlineData(60, -50)]
    public void ZeroSpeedOrAZeroPeriodRests(double speed, double gap)
    {
        var marquee = Stage(new Marquee { Content = Bar(50), Speed = speed, Gap = gap });

        Frames.Advance(500, 16);

        Assert.False(Presto.Engine.IsRunning);
        Assert.Equal(0, Offset(marquee));
    }

    private static Border Bar(double width) => new() { Width = width, Background = Brushes.Red };

    /// <summary>Stages <paramref name="marquee"/> and runs its first frame, which only takes the time.</summary>
    private Marquee Moving(Marquee marquee)
    {
        Stage(marquee);
        Frames.Advance(16);
        Assert.Equal(0, Offset(marquee));
        return marquee;
    }

    private static double Offset(Marquee marquee, TransformShorthand axis = TransformShorthand.X) =>
        RenderTransformComposer.Get(marquee.GetVisualChildren().Single(), axis);

    /// <summary>The speed shown over the next 16 ms frame, in DIPs per second along the marquee's Direction.</summary>
    private double Velocity(Marquee marquee)
    {
        var axis = marquee.Direction is MarqueeDirection.Left or MarqueeDirection.Right ? TransformShorthand.X : TransformShorthand.Y;
        var before = Offset(marquee, axis);
        Frames.Advance(16);
        return (Offset(marquee, axis) - before) / .016 * (marquee.Direction is MarqueeDirection.Left or MarqueeDirection.Up ? -1 : 1);
    }

    private static Marquee Stage(Marquee marquee)
    {
        new Window { Content = marquee, Width = 200, Height = 200 }.Show();
        Tick();
        return marquee;
    }

    private static List<Control> Instances(Marquee marquee) =>
        ((Panel)marquee.GetVisualChildren().Single()).Children.ToList();
}
