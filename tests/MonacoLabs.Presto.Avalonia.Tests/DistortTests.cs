using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using MonacoLabs.Presto.Controls;

namespace MonacoLabs.Presto.Tests;

/// <summary>A <see cref="Distort"/> draws its child live at identity and a filtered snapshot of it otherwise.</summary>
public sealed class DistortTests : CompositorTestBase
{
    // The red 40x20 bar sits 20 DIPs inside the 80x60 control at the window's top left.
    private static readonly Rect Bar = new(20, 20, 40, 20);

    private static (Distort Distort, Border Bar) ShowDistort(Action<Distort>? set = null)
    {
        var bar = new Border { Width = Bar.Width, Height = Bar.Height, Background = new SolidColorBrush(Colors.Red) };
        var distort = new Distort
        {
            Child = bar, Padding = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        set?.Invoke(distort);
        new Window { Content = distort, Background = Brushes.White, Width = 100, Height = 100 }.Show();
        Tick();
        return (distort, bar);
    }

    private static int RedPixels(Visual visual, Rect box)
    {
        var count = 0;
        for (var y = (int)box.Y; y < box.Bottom; y++)
            foreach (var pixel in Row(visual, y)[(int)box.X..(int)box.Right])
                if (pixel == Colors.Red) count++;
        return count;
    }

    private static readonly Rect Around = Bar.Inflate(10);

    [AvaloniaFact]
    public void AtIdentityTheChildDrawsLiveWithNoSnapshot()
    {
        var (distort, _) = ShowDistort();

        Assert.Null(distort.SnapshotSize);
        Assert.Equal(0, distort.Snapshots);
        Assert.Equal(800, RedPixels(distort, Around));
        Assert.Equal(Extent(distort, Colors.Red), Bar);
    }

    [AvaloniaTheory]
    [InlineData(nameof(Distort.Turbulence))]
    [InlineData(nameof(Distort.Goo))]
    [InlineData(nameof(Distort.Glitch))]
    public void EachEffectDrawsASnapshotThatChangesPixelsInsteadOfTheChild(string effect)
    {
        var (distort, _) = ShowDistort();
        var identity = Capture(distort);

        Set(distort, effect);
        Tick();

        Assert.Equal(1, distort.Snapshots);
        Assert.True(Difference(identity, Capture(distort)) > 0);
        // The live bar would cover every one of its 800 pixels red, so fewer means only the filtered snapshot is drawn.
        Assert.InRange(RedPixels(distort, Bar), 1, 799);
    }

    private static void Set(Distort distort, string effect)
    {
        switch (effect)
        {
            case nameof(Distort.Turbulence):
                distort.Turbulence = 8;
                distort.TurbulenceScale = 6;
                break;
            case nameof(Distort.Goo):
                distort.Goo = 16;
                break;
            default:
                distort.Glitch = 6;
                break;
        }
    }

    [AvaloniaFact]
    public void TheSnapshotIsTakenAtTheRenderScaling()
    {
        var bar = new Border { Width = 40, Height = 20, Background = Brushes.Red };
        var distort = new Distort { Child = bar, Goo = 4 };
        var window = new Window { Content = distort, Width = 100, Height = 100 };
        window.SetRenderScaling(1.5);
        window.Show();
        Tick();

        Assert.Equal(new PixelSize(60, 30), distort.SnapshotSize);
    }

    [AvaloniaFact]
    public void AChildThatRedrawsWhileActiveIsSnapshottedAgain()
    {
        var (distort, bar) = ShowDistort(d => d.Glitch = 6);
        Assert.Equal(1, distort.Snapshots);

        bar.Background = new SolidColorBrush(Colors.Blue);
        Tick();
        Tick();

        Assert.Equal(2, distort.Snapshots);
        Assert.Equal(0, RedPixels(distort, Around));
        Assert.NotNull(Extent(distort, Colors.Blue));

        Tick();
        Tick();
        Assert.Equal(2, distort.Snapshots);
    }

    [AvaloniaFact]
    public void AHeldEffectStopsCheckingForRedrawsOnceNothingChanges()
    {
        var (distort, _) = ShowDistort(d => d.Glitch = 6);
        Tick();
        var checks = distort.Checks;

        for (var i = 0; i < 5; i++) Tick();

        Assert.Equal(checks, distort.Checks);
        Assert.Equal(1, distort.Snapshots);
    }

    [AvaloniaFact]
    public void AnAnimatedEffectIsCheckedEveryFrame()
    {
        var (distort, _) = ShowDistort(d => d.Turbulence = 8);
        distort.Animate(b => b.Property(Distort.TurbulencePhaseProperty, from: 0.0, to: 4.0).Duration(1000).Ease(Eases.None));

        for (var i = 0; i < 3; i++)
        {
            var checks = distort.Checks;
            Frames.Advance(16);
            Tick();
            Assert.True(distort.Checks > checks, $"frame {i} was not checked");
        }
    }

    [AvaloniaFact]
    public void AnAnimatedEffectDrawsBetweenFramesAndRevertsToTheLocalValue()
    {
        var (distort, _) = ShowDistort();
        var identity = Capture(distort);
        var animation = distort.Animate(b => b.Property(Distort.GlitchProperty, from: 0.0, to: 8.0).Duration(100).Ease(Eases.None));

        Frames.Advance(50, 10);
        Tick();
        Assert.InRange(distort.Glitch, 1, 7);
        Assert.NotNull(distort.SnapshotSize);
        Assert.True(Difference(identity, Capture(distort)) > 0);

        animation.Revert();
        Tick();
        Assert.Equal(0, distort.Glitch);
        Assert.Null(distort.SnapshotSize);
        Assert.Equal(identity, Capture(distort));
    }

    [AvaloniaFact]
    public void DetachingDropsTheSnapshot()
    {
        var (distort, _) = ShowDistort(d => d.Turbulence = 8);
        Assert.NotNull(distort.SnapshotSize);

        ((Window)TopLevel.GetTopLevel(distort)!).Content = null;

        Assert.Null(distort.SnapshotSize);
    }

    [AvaloniaFact]
    public void UnderReducedMotionEveryEffectDrawsAsIdentity()
    {
        var identity = Capture(ShowDistort().Distort);
        Presto.Defaults.ReducedMotion = true;

        var (distort, _) = ShowDistort(d => (d.Turbulence, d.Goo, d.Glitch) = (8, 16, 6));

        Assert.Null(distort.SnapshotSize);
        Assert.Equal(identity, Capture(distort));
    }

    [AvaloniaFact]
    public void TheDefaultsAreIdentityWithA32DipTurbulenceScale()
    {
        var distort = new Distort();

        Assert.Equal((0d, 32d, 0d, 0d, 0d), (distort.Turbulence, distort.TurbulenceScale, distort.TurbulencePhase, distort.Goo, distort.Glitch));
    }
}
