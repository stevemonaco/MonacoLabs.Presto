using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using MonacoLabs.Presto.Controls;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="Flip"/> (Phase 65e): a 100x60 card in the middle of a 200x200 window, turning over a linear 400 ms.</summary>
public sealed class FlipTests : CompositorTestBase
{
    [AvaloniaFact]
    public void TheFrontShowsBelow90DegreesAndTheBackAbove()
    {
        var flip = Card();

        flip.IsFlipped = true;
        Frames.Advance(160, 20);
        Assert.True(flip.Front!.IsEffectivelyVisible);
        Assert.False(flip.Back!.IsEffectivelyVisible);

        Frames.Advance(80, 20);
        Assert.False(flip.Front.IsEffectivelyVisible);
        Assert.True(flip.Back.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void TogglingMidFlipTurnsBackFromWhereTheCardStands()
    {
        var flip = Card();
        flip.IsFlipped = true;
        Frames.Advance(240, 20);
        var stood = Angle(flip);

        flip.IsFlipped = false;
        Frames.Advance(20, 20);

        Assert.InRange(stood, 100, 120);
        Assert.InRange(Angle(flip), stood - 30, stood - .1);
        Frames.Advance(1000, 20);
        Assert.Equal(0, Angle(flip));
        Assert.True(flip.Front!.IsEffectivelyVisible);
    }

    [AvaloniaTheory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void TheBackReadsTheRightWayRoundOnceTurned(Orientation orientation)
    {
        var flip = Card(orientation);

        flip.IsFlipped = true;
        Frames.Advance(1000, 20);

        Tick();
        (Point First, Point Second) probes = orientation == Orientation.Horizontal ? (new(60, 100), new(140, 100)) : (new(100, 75), new(100, 125));
        Assert.True(Pump(() => Pixel(flip, probes.First) == Colors.Red && Pixel(flip, probes.Second) == Colors.Blue),
            $"the back drew {Pixel(flip, probes.First)} then {Pixel(flip, probes.Second)}");
    }

    [AvaloniaFact]
    public void ReducedSwapsTheFacesAtOnce()
    {
        var flip = Card();
        Presto.Defaults.ReducedMotion = true;

        flip.IsFlipped = true;

        Assert.False(flip.Front!.IsEffectivelyVisible);
        Assert.True(flip.Back!.IsEffectivelyVisible);
        Assert.Equal(180, Angle(flip));
    }

    [AvaloniaFact]
    public void LeavingTheTreeMidTurnLandsTheTurn()
    {
        var flip = Card();
        flip.IsFlipped = true;
        Frames.Advance(120, 20);

        ((Window)TopLevel.GetTopLevel(flip)!).Content = null;

        Assert.False(Shows(flip.Front!));
        Assert.True(Shows(flip.Back!));
        Assert.Equal(180, Angle(flip));
    }

    [AvaloniaFact]
    public void ACardOutsideATreeTurnsAtOnce()
    {
        var flip = Card(show: false);

        flip.IsFlipped = true;

        Assert.False(Shows(flip.Front!));
        Assert.True(Shows(flip.Back!));
        Assert.Equal(180, Angle(flip));
    }

    [AvaloniaFact]
    public void AMotionWithMoreThanTimingAndEaseThrowsWhenSet()
    {
        Assert.Throws<NotSupportedException>(() => new Flip { Motion = Presto.Build().Duration(400).Opacity(to: 0) });
        Assert.Throws<NotSupportedException>(() => new Flip { Motion = Presto.Build().Duration(400).OnComplete(_ => { }) });
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangingTheOrientationWhileFlippedMovesTheTurnOntoTheNewAxis(bool midTurn)
    {
        var flip = Card();
        flip.IsFlipped = true;
        Frames.Advance(midTurn ? 120 : 1000, 20);

        flip.Orientation = Orientation.Vertical;
        Frames.Advance(1000, 20);

        var card = flip.Front!.Parent!.Parent!;
        Assert.Equal(0, Convert.ToDouble(Presto.Get(card, "RotateY", withUnit: false)));
        Assert.Equal(180, Convert.ToDouble(Presto.Get(card, "RotateX", withUnit: false)));
        Assert.True(Shows(flip.Back!));
    }

    private static Flip Card(Orientation orientation = Orientation.Horizontal, bool show = true)
    {
        // The back's first half is red and its second blue, along the axis the card turns across.
        var back = new Grid { Background = Brushes.Blue };
        back.Children.Add(orientation == Orientation.Horizontal
            ? new Border { Background = Brushes.Red, Width = 50, HorizontalAlignment = HorizontalAlignment.Left }
            : new Border { Background = Brushes.Red, Height = 30, VerticalAlignment = VerticalAlignment.Top });
        var flip = new Flip
        {
            Width = 100,
            Height = 60,
            Orientation = orientation,
            Motion = Presto.Build().Duration(400).Ease("linear"),
            Front = new Border { Background = Brushes.Green },
            Back = back,
        };
        if (show) Show(flip);
        return flip;
    }

    // Effective visibility is only kept up to date inside a tree, so the face's holder is read.
    private static bool Shows(Control face) => ((Visual)face.Parent!).IsVisible;

    private static Color Pixel(Visual visual, Point at) => Row(visual, (int)at.Y)[(int)at.X];

    private static double Angle(Flip flip) =>
        Convert.ToDouble(Presto.Get(flip.Front!.Parent!.Parent, flip.Orientation == Orientation.Horizontal ? "RotateY" : "RotateX", withUnit: false));
}
