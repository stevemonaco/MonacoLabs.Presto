using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Controls;
using Path = Avalonia.Controls.Shapes.Path;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>The determinate <see cref="ProgressRing"/> and its hold-to-confirm (GAPS-2-PLAN §10.2 65b).</summary>
public sealed class ProgressRingTests : BridgeTestBase
{
    private static ProgressRing Ring() =>new() { Width = 100, Height = 100, Thickness = 4 };

    private static double[] Drawn(ProgressRing ring) =>
        ring.GetVisualChildren().Cast<Path>().Skip(ring.Segments).Select(fill => ((double[])PrestoAvalonia.Get(fill, "Draw")!)[1]).ToArray();

    [AvaloniaTheory]
    [InlineData(1, .25, new[] { .25 })]
    [InlineData(4, .625, new[] { 1, 1, .5, 0 })]
    public void ItDrawsItsValueAcrossItsSegments(int segments, double value, double[] drawn)
    {
        var ring = Ring();
        ring.Segments = segments;
        ring.Value = value;
        Show(ring);

        Assert.Equal(drawn, Drawn(ring));
        var radius = (100 - 4) / 2d;
        var span = segments == 1 ? 360 : (360d / segments) - ring.Gap;
        var tracks = ring.GetVisualChildren().Cast<Path>().Take(segments).ToArray();
        Assert.All(tracks, track => Assert.Equal(radius * span * Math.PI / 180, track.RenderedGeometry!.ContourLength, .5));
    }

    [AvaloniaFact]
    public void AHoldFillsTheRingAndConfirmsAtOneThenRewindsOnRelease()
    {
        var ring = Ring();
        ring.HoldDuration = 400;
        var confirmed = 0;
        ring.Confirmed += (_, _) => confirmed++;
        var window = Show(ring);
        var centre = Centre(ring, window);

        window.MouseDown(centre, MouseButton.Left);
        Frames.Advance(200, 16);
        Assert.InRange(ring.Value, .4, .6);
        Assert.Equal(0, confirmed);
        Frames.Advance(240, 16);
        Assert.Equal(1, ring.Value);
        Assert.Equal(1, confirmed);

        window.MouseUp(centre, MouseButton.Left);
        Frames.Advance(Recipes.Tokens.Short + 32, 16);
        Assert.Equal(0, ring.Value);
        Assert.Equal(1, confirmed);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReleasingBeforeOneRewindsWithoutConfirming(bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var ring = Ring();
        ring.HoldDuration = 400;
        var confirmed = false;
        ring.Confirmed += (_, _) => confirmed = true;
        var window = Show(ring);
        var centre = Centre(ring, window);

        window.MouseDown(centre, MouseButton.Left);
        Frames.Advance(200, 16);
        Assert.InRange(ring.Value, .4, .6);
        window.MouseUp(centre, MouseButton.Left);
        Frames.Advance(16);
        if (reduced) Assert.Equal(0, ring.Value);
        else Assert.InRange(ring.Value, .01, .6);
        Frames.Advance(Recipes.Tokens.Short + 400, 16);

        Assert.Equal(0, ring.Value);
        Assert.Equal([0d], Drawn(ring));
        Assert.False(confirmed);
    }

    [AvaloniaFact]
    public void SpaceHoldsTheRingWhileFocused()
    {
        var ring = Ring();
        ring.HoldDuration = 200;
        var confirmed = false;
        ring.Confirmed += (_, _) => confirmed = true;
        var window = Show(ring);
        ring.Focus();

        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Frames.Advance(240, 16);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);

        Assert.True(confirmed);
    }
}
