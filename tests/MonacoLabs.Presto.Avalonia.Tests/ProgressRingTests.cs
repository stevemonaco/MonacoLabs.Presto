using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Controls;
using Path = Avalonia.Controls.Shapes.Path;

namespace MonacoLabs.Presto.Tests;

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
        var center = Center(ring, window);

        window.MouseDown(center, MouseButton.Left);
        Frames.Advance(200, 16);
        Assert.InRange(ring.Value, .4, .6);
        Assert.Equal(0, confirmed);
        Frames.Advance(240, 16);
        Assert.Equal(1, ring.Value);
        Assert.Equal(1, confirmed);

        window.MouseUp(center, MouseButton.Left);
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
        var center = Center(ring, window);

        window.MouseDown(center, MouseButton.Left);
        Frames.Advance(200, 16);
        Assert.InRange(ring.Value, .4, .6);
        window.MouseUp(center, MouseButton.Left);
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

    [AvaloniaFact]
    public void AConfirmedHoldExecutesTheCommandAfterConfirmed()
    {
        var ring = Ring();
        ring.HoldDuration = 200;
        var order = new List<object?>();
        ring.Confirmed += (_, _) => order.Add("confirmed");
        ring.Command = new TestCommand(order.Add);
        ring.CommandParameter = "abandon";
        var window = Show(ring);
        var center = Center(ring, window);

        window.MouseDown(center, MouseButton.Left);
        Frames.Advance(240, 16);
        window.MouseUp(center, MouseButton.Left);

        Assert.Equal(["confirmed", "abandon"], order);
    }

    [AvaloniaFact]
    public void ACommandThatCannotExecuteDisablesTheRingAndFollowsCanExecuteChanged()
    {
        var ring = Ring();
        ring.HoldDuration = 200;
        var command = new TestCommand(_ => { }) { Enabled = false };
        ring.Command = command;
        var window = Show(ring);
        var center = Center(ring, window);

        Assert.False(ring.IsEffectivelyEnabled);
        window.MouseDown(center, MouseButton.Left);
        Frames.Advance(100, 16);
        window.MouseUp(center, MouseButton.Left);
        Assert.Equal(0, ring.Value);

        command.Enabled = true;
        Assert.True(ring.IsEffectivelyEnabled);
        window.MouseDown(center, MouseButton.Left);
        Frames.Advance(100, 16);
        Assert.InRange(ring.Value, .3, .7);
        window.MouseUp(center, MouseButton.Left);

        ring.Command = new TestCommand(_ => { }) { Enabled = false };
        Assert.False(ring.IsEffectivelyEnabled);
        ring.Command = null;
        Assert.True(ring.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public void AHoldRaisesHoldStartedOnceByPointerAndByKey()
    {
        var ring = Ring();
        ring.HoldDuration = 200;
        var started = new List<double>();
        ring.HoldStarted += (_, _) => started.Add(ring.Value);
        var window = Show(ring);
        var center = Center(ring, window);

        window.MouseDown(center, MouseButton.Left);
        Frames.Advance(100, 16);
        window.MouseUp(center, MouseButton.Left);
        Frames.Advance(Recipes.Tokens.Short + 32, 16);
        Assert.Equal([0d], started);

        ring.Focus();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Frames.Advance(48, 16);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Frames.Advance(48, 16);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);

        Assert.Equal([0d, 0d], started);
    }

    [AvaloniaFact]
    public void ReleasingShortRaisesHoldCanceledOnceAndAConfirmedReleaseRaisesNone()
    {
        var ring = Ring();
        ring.HoldDuration = 200;
        var canceled = new List<double>();
        ring.HoldCanceled += (_, _) => canceled.Add(ring.Value);
        var window = Show(ring);
        var center = Center(ring, window);

        window.MouseDown(center, MouseButton.Left);
        Frames.Advance(100, 16);
        var held = ring.Value;
        window.MouseUp(center, MouseButton.Left);
        Frames.Advance(Recipes.Tokens.Short + 32, 16);
        Assert.Equal([held], canceled);

        window.MouseDown(center, MouseButton.Left);
        Frames.Advance(240, 16);
        window.MouseUp(center, MouseButton.Left);
        Frames.Advance(Recipes.Tokens.Short + 32, 16);
        Assert.Single(canceled);
    }

    private static double[] ArcLengths(ProgressRing ring) =>
        ring.GetVisualChildren().Cast<Path>().Take(ring.Segments).Select(track => track.RenderedGeometry!.ContourLength).ToArray();

    [AvaloniaFact]
    public void ChangingSegmentsOrBrushesRebuildsTheArcsAndGapOrThicknessReshapesThemOnTheNextArrange()
    {
        var ring = Ring();
        ring.Value = .5;
        Show(ring);

        ring.Segments = 3;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(6, ring.GetVisualChildren().Count());
        Assert.Equal([1, .5, 0], Drawn(ring));

        ring.Fill = Brushes.Green;
        ring.Track = Brushes.Gray;
        Dispatcher.UIThread.RunJobs();
        var arcs = ring.GetVisualChildren().Cast<Path>().ToArray();
        Assert.All(arcs.Take(3), track => Assert.Same(Brushes.Gray, track.Stroke));
        Assert.All(arcs.Skip(3), fill => Assert.Same(Brushes.Green, fill.Stroke));

        ring.Gap = 30;
        ring.Thickness = 10;
        Dispatcher.UIThread.RunJobs();
        var radius = (100 - 10) / 2d;
        Assert.All(ArcLengths(ring), length => Assert.Equal(radius * (120 - 30) * Math.PI / 180, length, .5));
        Assert.All(ring.GetVisualChildren().Cast<Path>(), arc => Assert.Equal(10, arc.StrokeThickness));
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(-2)]
    public void ANonPositiveSegmentCountThrows(int segments)
    {
        var ring = Ring();

        Assert.Throws<ArgumentException>(() => ring.Segments = segments);
        Assert.Equal(1, ring.Segments);
    }

    [AvaloniaFact]
    public void ItMeasuresToZeroAndTakesTheSizeItIsArrangedAt()
    {
        var ring = new ProgressRing();

        ring.Measure(Size.Infinity);
        ring.Arrange(new Rect(0, 0, 80, 60));

        Assert.Equal(default, ring.DesiredSize);
        Assert.Equal(new Size(80, 60), ring.Bounds.Size);
        Assert.Equal((60 - 4) / 2d * 2 * Math.PI, ArcLengths(ring).Single(), .5);
    }

    [AvaloniaFact]
    public void ByDefaultItDrawsNoFillOverOneWholeFaintTrackFourDipsWide()
    {
        var ring = new ProgressRing { Width = 100, Height = 100 };
        Show(ring);

        Assert.Equal([0d], Drawn(ring));
        Assert.Equal((100 - 4) / 2d * 2 * Math.PI, ArcLengths(ring).Single(), .5);
        var (track, fill) = (ring.GetVisualChildren().Cast<Path>().First(), ring.GetVisualChildren().Cast<Path>().Last());
        Assert.Equal(Colors.White, ((ISolidColorBrush)fill.Stroke!).Color);
        Assert.Equal(1, fill.Stroke!.Opacity);
        Assert.Equal(Color.Parse("#66FFFFFF"), ((ISolidColorBrush)track.Stroke!).Color);
        Assert.All(ring.GetVisualChildren().Cast<Path>(), arc => Assert.Equal(4, arc.StrokeThickness));

        ring.Segments = 4;
        Dispatcher.UIThread.RunJobs();
        Assert.All(ArcLengths(ring), length => Assert.Equal((100 - 4) / 2d * (90 - 6) * Math.PI / 180, length, .5));
    }

    [AvaloniaFact]
    public void WithoutAHoldDurationItTakesNoHolds()
    {
        var ring = new ProgressRing { Width = 100, Height = 100 };
        var window = Show(ring);
        var center = Center(ring, window);

        window.MouseDown(center, MouseButton.Left);
        Frames.Advance(200, 16);
        window.MouseUp(center, MouseButton.Left);

        Assert.Equal(0, ring.HoldDuration);
        Assert.False(ring.Focusable);
        Assert.Equal(0, ring.Value);
    }

    [AvaloniaFact]
    public void WithoutACommandTheRingIsEnabledAndAHoldConfirms()
    {
        var ring = Ring();
        ring.HoldDuration = 200;
        var confirmed = false;
        ring.Confirmed += (_, _) => confirmed = true;
        var window = Show(ring);
        var center = Center(ring, window);

        window.MouseDown(center, MouseButton.Left);
        Frames.Advance(240, 16);
        window.MouseUp(center, MouseButton.Left);

        Assert.Null(ring.Command);
        Assert.Null(ring.CommandParameter);
        Assert.True(ring.IsEffectivelyEnabled);
        Assert.True(confirmed);
    }
}
