using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
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
    public void TheHiddenChildKeepsItsLayoutAndItsOwnValues()
    {
        var (distort, bar) = ShowDistort();
        var slot = bar.Bounds;
        var visual = ElementComposition.GetElementVisual(bar)!;
        var scale = visual.Scale;

        distort.Glitch = 6;
        Tick();

        Assert.Equal(default(Vector3D), visual.Scale);
        Assert.Equal(slot, bar.Bounds);
        Assert.True(bar.IsEffectivelyVisible);
        Assert.Equal((1d, (Transform?)null, (Geometry?)null, (IBrush?)null), (bar.Opacity, bar.RenderTransform, bar.Clip, bar.OpacityMask));

        distort.Glitch = 0;
        Tick();
        Assert.Equal(scale, visual.Scale);
    }

    [AvaloniaFact]
    public void TheEffectsDrawInThePaddingButNeverOutsideTheBounds()
    {
        var (distort, _) = ShowDistort(d => (d.Turbulence, d.TurbulenceScale, d.Glitch) = (20, 6, 30));

        Assert.True(CountPixels(distort, new Rect(0, 0, 80, 60), Colors.White) - CountPixels(distort, Bar, Colors.White) > 0, "nothing reached the padding");
        Assert.Equal(0, CountPixels(distort, new Rect(80, 0, 20, 100), Colors.White));
        Assert.Equal(0, CountPixels(distort, new Rect(0, 60, 100, 40), Colors.White));
    }

    [AvaloniaFact]
    public void ANewChildDropsTheSnapshotAndShowsThePreviousChildAgain()
    {
        var (distort, bar) = ShowDistort(d => d.Glitch = 6);
        var visual = ElementComposition.GetElementVisual(bar)!;
        Assert.Equal(default(Vector3D), visual.Scale);

        distort.Child = new Border { Width = 40, Height = 20, Background = Brushes.Blue };

        Assert.Null(distort.SnapshotSize);
        Assert.Equal(new Vector3D(1, 1, 1), visual.Scale);
        Tick();
        Assert.Equal(2, distort.Snapshots);
    }

    [AvaloniaFact]
    public void WithoutASkiaLeaseTheSnapshotDrawsUnfiltered()
    {
        var drawn = new List<string>();
        var context = NewContext(NoLease.Create(drawn), ownsImpl: false);
        using var bitmap = new WriteableBitmap(new PixelSize(10, 10), new Vector(96, 96));
        var owner = new Owner();

        using (var operation = new DistortDrawOperation(bitmap, new Rect(0, 0, 10, 10), new Rect(0, 0, 30, 30), (8, 32, 0, 16, 6), owner))
            operation.Render(context);

        Assert.Equal(["GetFeature", "DrawBitmap"], drawn.Where(name => name is "GetFeature" or "DrawBitmap"));
        Assert.True(owner.Disposed);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern ImmediateDrawingContext NewContext(IDrawingContextImpl impl, bool ownsImpl);

    private sealed class Owner : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    /// <summary>A drawing context with no features, so no Skia lease, that records the calls it gets.</summary>
    public class NoLease : DispatchProxy
    {
        private List<string> _calls = [];

        public static IDrawingContextImpl Create(List<string> calls)
        {
            var proxy = Create<IDrawingContextImpl, NoLease>();
            ((NoLease)(object)proxy)._calls = calls;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            _calls.Add(method!.Name);
            return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }

    [AvaloniaFact]
    public void TheEffectsApplyAsTurbulenceThenGooThenGlitch()
    {
        // Goo after Turbulence smooths its ragged edge, and Glitch after Goo keeps the steps its bands shift.
        Assert.True(MostEdgesInARow(ShowDistort(d => (d.Turbulence, d.TurbulenceScale) = (8, 6)).Distort) > 2);
        Assert.Equal(2, MostEdgesInARow(ShowDistort(d => (d.Turbulence, d.TurbulenceScale, d.Goo) = (8, 6, 16)).Distort));
        Assert.InRange(LargestStep(ShowDistort(d => d.Goo = 16).Distort), 0, 3);
        Assert.InRange(LargestStep(ShowDistort(d => (d.Goo, d.Glitch) = (16, 6)).Distort), 6, 12);
    }

    [AvaloniaFact]
    public void EachEffectAtItsIdentityIsSkipped()
    {
        var glitched = Capture(ShowDistort(d => d.Glitch = 6).Distort);

        Assert.Equal(glitched, Capture(ShowDistort(d => (d.Goo, d.Glitch, d.TurbulenceScale) = (-4, 6, 3)).Distort));
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1e-6)]
    [InlineData(-5)]
    public void ATurbulenceScaleBelowAThousandthIsAThousandth(double scale)
    {
        var floor = Capture(ShowDistort(d => (d.Turbulence, d.TurbulenceScale) = (8, 1e-3)).Distort);

        Assert.Equal(floor, Capture(ShowDistort(d => (d.Turbulence, d.TurbulenceScale) = (8, scale)).Distort));
        Assert.NotEqual(floor, Capture(ShowDistort(d => (d.Turbulence, d.TurbulenceScale) = (8, 6)).Distort));
    }

    private static bool IsRed(Color pixel) => pixel.R > 128 && pixel.G < 128;

    private static int MostEdgesInARow(Visual visual)
    {
        var most = 0;
        for (var y = 0; y < 60; y++)
        {
            var row = Row(visual, y);
            var edges = 0;
            for (var x = 1; x < 80; x++)
                if (IsRed(row[x]) != IsRed(row[x - 1])) edges++;
            most = Math.Max(most, edges);
        }
        return most;
    }

    // The largest change of the red shape's left edge from one row to the next.
    private static int LargestStep(Visual visual)
    {
        int? last = null;
        var largest = 0;
        for (var y = 0; y < 60; y++)
        {
            var left = Array.FindIndex(Row(visual, y)[..80], IsRed);
            if (left < 0) continue;
            if (last is { } previous) largest = Math.Max(largest, Math.Abs(left - previous));
            last = left;
        }
        return largest;
    }

    [AvaloniaFact]
    public void AFlagChangeRedrawsAtOnce()
    {
        var (distort, _) = ShowDistort(d => d.Glitch = 6);
        Assert.NotNull(distort.SnapshotSize);

        Presto.Defaults.ReducedMotion = true;
        Tick();
        Assert.Null(distort.SnapshotSize);

        Presto.Defaults.ReducedMotion = false;
        Tick();
        Assert.NotNull(distort.SnapshotSize);
    }

    [AvaloniaFact]
    public void TheDefaultsAreIdentityWithA32DipTurbulenceScale()
    {
        var distort = new Distort();

        Assert.Equal((0d, 32d, 0d, 0d, 0d), (distort.Turbulence, distort.TurbulenceScale, distort.TurbulencePhase, distort.Goo, distort.Glitch));
    }
}
