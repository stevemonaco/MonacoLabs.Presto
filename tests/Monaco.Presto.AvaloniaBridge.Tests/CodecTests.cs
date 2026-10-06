using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Covers the Avalonia value codecs and the easing interop, mirroring tests/suites/values.test.js and colors.test.js for the Avalonia types.</summary>
public sealed class CodecTests : BridgeTestBase
{
    private static T Tweened<T>(T from, TweenValue to, double progress = 1)
    {
        var box = new ValueBox<T> { Value = from };
        var animation = Presto.Animate(box, new() { ["Value"] = to, Duration = 100, Ease = "linear", Autoplay = false });
        animation.Seek(progress * animation.Duration);
        return box.Value;
    }

    [AvaloniaFact]
    public void GeometryStructsInterpolateEveryChannel()
    {
        Assert.Equal(new Thickness(5, 15, 25, 35), Tweened(new Thickness(0, 10, 20, 30), TweenValue.Of(new Thickness(10, 20, 30, 40)), .5));
        Assert.Equal(new CornerRadius(5, 15, 25, 35), Tweened(new CornerRadius(0, 10, 20, 30), TweenValue.Of(new CornerRadius(10, 20, 30, 40)), .5));
        Assert.Equal(new Point(5, 10), Tweened(new Point(0, 0), TweenValue.Of(new Point(10, 20)), .5));
        Assert.Equal(new Size(5, 10), Tweened(new Size(0, 0), TweenValue.Of(new Size(10, 20)), .5));
        Assert.Equal(new Rect(5, 10, 20, 25), Tweened(new Rect(0, 0, 10, 10), TweenValue.Of(new Rect(10, 20, 30, 40)), .5));
        Assert.Equal(new Vector(5, 10), Tweened(new Vector(0, 0), TweenValue.Of(new Vector(10, 20)), .5));
    }

    [AvaloniaFact]
    public void ANumberFillsEveryChannelOfAGeometryStruct() =>
        Assert.Equal(new Thickness(10), Tweened(default(Thickness), 10));

    [AvaloniaFact]
    public void RelativePointTweensItsCoordinatesAndTakesTheUnitOfItsEndValue()
    {
        var absolute = new RelativePoint(10, 20, RelativeUnit.Absolute);
        Assert.Equal(absolute, Tweened(default(RelativePoint), TweenValue.Of(absolute)));
        Assert.Equal(
            new RelativePoint(.25, .75, RelativeUnit.Relative),
            Tweened(RelativePoint.TopLeft, TweenValue.Of(new RelativePoint(.5, 1.5, RelativeUnit.Relative)), .5));
    }

    [AvaloniaFact]
    public void PixelGridLengthsInterpolateFromALengthOrANumber()
    {
        Assert.Equal(new GridLength(150), Tweened(new GridLength(100), TweenValue.Of(new GridLength(200)), .5));
        Assert.Equal(new GridLength(50), Tweened(new GridLength(100), 0, .5));
    }

    [AvaloniaFact]
    public void AColumnWidthAndARowHeightAnimateInOneCall()
    {
        var (column, row) = (new ColumnDefinition(new GridLength(100)), new RowDefinition(new GridLength(20)));

        Presto.Animate(column, new() { ["Width"] = 40, Duration = 100, Ease = "linear", Autoplay = false }).Seek(50);
        Presto.Animate(row, new() { ["Height"] = "+=20", Duration = 100, Ease = "linear", Autoplay = false }).Seek(50);

        Assert.Equal(new GridLength(70), column.Width);
        Assert.Equal(new GridLength(30), row.Height);
    }

    [AvaloniaFact]
    public void AStarOrAutoLengthAtEitherEndThrows()
    {
        Assert.Throws<ArgumentException>(() => Tweened(GridLength.Star, 100));
        Assert.Throws<ArgumentException>(() => Tweened(new GridLength(100), TweenValue.Of(GridLength.Auto)));
    }

    [AvaloniaFact]
    public void AGridLengthOvershootingZeroWritesZero() =>
        Assert.Equal(new GridLength(0), Tweened(new GridLength(10), -20));

    [AvaloniaFact]
    public void ColorTweensFromAHexStringAndFromATypedColor()
    {
        Assert.Equal(Color.FromRgb(255, 107, 107), Tweened(Colors.Black, "#ff6b6b"));
        var translucent = Color.FromArgb(128, 255, 107, 107);
        Assert.Equal(translucent, Tweened(Colors.Black, TweenValue.Of(translucent)));
    }

    [AvaloniaFact]
    public void SolidColorBrushTweenWritesANewBrushAndLeavesTheOriginalUntouched()
    {
        var original = new SolidColorBrush(Colors.Red);
        var box = new ValueBox<IBrush> { Value = original };
        var animation = Presto.Animate(box, new() { ["Value"] = "#0000ff", Duration = 100, Ease = "linear", Autoplay = false });

        animation.Seek(animation.Duration);

        Assert.Equal(Colors.Blue, Assert.IsType<SolidColorBrush>(box.Value).Color);
        Assert.NotSame(original, box.Value);
        Assert.Equal(Colors.Red, original.Color);
        Assert.Equal(Colors.Blue, Assert.IsType<SolidColorBrush>(Tweened<IBrush>(Brushes.Red, "#0000ff")).Color);
    }

    [AvaloniaFact]
    public void LinearGradientInterpolatesItsStopsAndPoints()
    {
        var from = Linear(new(0, 0), new(1, 0), (0, Color.FromRgb(0, 0, 200)), (1, Color.FromRgb(200, 0, 0)));
        var to = Linear(new(0, 0), new(0, 1), (0, Color.FromRgb(200, 0, 0)), (1, Color.FromRgb(0, 0, 200)));

        var brush = Assert.IsType<LinearGradientBrush>(Tweened<IBrush>(from, TweenValue.Of(to), .5));

        Assert.Equal(new RelativePoint(.5, .5, RelativeUnit.Relative), brush.EndPoint);
        Assert.Equal([Color.FromRgb(100, 0, 100), Color.FromRgb(100, 0, 100)], brush.GradientStops.Select(stop => stop.Color));
    }

    [AvaloniaFact]
    public void GradientsWithDifferentStopCountsInterpolateOnTheUnionOfTheirOffsets()
    {
        var from = Linear(new(0, 0), new(1, 0), (0, Colors.Black), (1, Color.FromRgb(200, 200, 200)));
        var to = Linear(new(0, 0), new(1, 0), (0, Colors.Black), (.5, Color.FromRgb(0, 200, 0)), (1, Color.FromRgb(200, 200, 200)));

        var halfway = Assert.IsType<LinearGradientBrush>(Tweened<IBrush>(from, TweenValue.Of(to), .5));
        var end = Assert.IsType<LinearGradientBrush>(Tweened<IBrush>(from, TweenValue.Of(to)));

        Assert.Equal([0, .5, 1], halfway.GradientStops.Select(stop => stop.Offset));
        Assert.Equal(Color.FromRgb(50, 150, 50), halfway.GradientStops[1].Color);
        Assert.Equal(to.GradientStops.Select(stop => stop.Color), end.GradientStops.Select(stop => stop.Color));
    }

    [AvaloniaFact]
    public void ASolidBrushTweensIntoAGradient()
    {
        var to = Linear(new(0, 0), new(1, 0), (0, Color.FromRgb(0, 0, 200)), (1, Color.FromRgb(0, 200, 0)));

        var brush = Assert.IsType<LinearGradientBrush>(Tweened<IBrush>(new SolidColorBrush(Color.FromRgb(200, 0, 0)), TweenValue.Of(to), .5));

        Assert.Equal([Color.FromRgb(100, 0, 100), Color.FromRgb(100, 100, 0)], brush.GradientStops.Select(stop => stop.Color));
    }

    [AvaloniaFact]
    public void AGradientTweenedToAColorEndsOnASolidBrush()
    {
        var from = Linear(new(0, 0), new(1, 0), (0, Color.FromRgb(0, 0, 200)), (1, Color.FromRgb(0, 200, 0)));

        var halfway = Assert.IsType<LinearGradientBrush>(Tweened<IBrush>(from, "#c80000", .5));
        var end = Assert.IsType<SolidColorBrush>(Tweened<IBrush>(from, "#c80000"));

        Assert.Equal([Color.FromRgb(100, 0, 100), Color.FromRgb(100, 100, 0)], halfway.GradientStops.Select(stop => stop.Color));
        Assert.Equal(Color.FromRgb(200, 0, 0), end.Color);
    }

    [AvaloniaFact]
    public void AMissingBrushFadesIntoAGradientInItsOwnColors()
    {
        var to = Linear(new(0, 0), new(1, 0), (0, Color.FromRgb(0, 0, 200)), (1, Color.FromRgb(0, 200, 0)));

        var brush = Assert.IsType<LinearGradientBrush>(Tweened<IBrush>(null!, TweenValue.Of(to), .5));

        Assert.Equal([Color.FromArgb(128, 0, 0, 200), Color.FromArgb(128, 0, 200, 0)], brush.GradientStops.Select(stop => stop.Color));
    }

    [AvaloniaFact]
    public void RadialAndConicGradientsInterpolateTheirGeometry()
    {
        var radial = Assert.IsType<RadialGradientBrush>(Tweened<IBrush>(
            Radial(new(.5, .5, RelativeUnit.Relative), .5),
            TweenValue.Of(Radial(new(0, 0, RelativeUnit.Relative), 1)),
            .5));
        var conic = Assert.IsType<ConicGradientBrush>(Tweened<IBrush>(Conic(0), TweenValue.Of(Conic(90)), .5));

        Assert.Equal(new RelativePoint(.25, .25, RelativeUnit.Relative), radial.Center);
        Assert.Equal(new RelativeScalar(.75, RelativeUnit.Relative), radial.RadiusX);
        Assert.Equal(45, conic.Angle);
    }

    [AvaloniaFact]
    public void AGradientTweenedToAnotherKindTakesTheEndGeometryAndInterpolatesItsStops()
    {
        var from = Linear(new(0, 0), new(1, 0), (0, Color.FromRgb(0, 0, 200)), (1, Color.FromRgb(0, 0, 200)));

        var brush = Assert.IsType<RadialGradientBrush>(Tweened<IBrush>(from, TweenValue.Of(Radial(new(0, 0, RelativeUnit.Relative), 1)), .5));

        Assert.Equal(new RelativePoint(0, 0, RelativeUnit.Relative), brush.Center);
        Assert.Equal(Color.FromRgb(100, 0, 100), brush.GradientStops[0].Color);
    }

    [AvaloniaFact]
    public void AGradientTweenReusesOneBrushAndLeavesTheOriginalUntouched()
    {
        var from = Linear(new(0, 0), new(1, 0), (0, Colors.Black), (1, Colors.White));
        var box = new ValueBox<IBrush> { Value = from };
        var animation = Presto.Animate(box, new() { ["Value"] = TweenValue.Of(Linear(new(0, 0), new(0, 1), (0, Colors.White), (1, Colors.Black))), Duration = 100, Ease = "linear", Autoplay = false });

        animation.Seek(25);
        var first = box.Value;
        animation.Seek(50);

        Assert.Same(first, box.Value);
        Assert.NotSame(from, box.Value);
        Assert.Equal(new RelativePoint(1, 0, RelativeUnit.Relative), from.EndPoint);
    }

    [AvaloniaFact]
    public void BoxShadowStringsParseAndInterpolate()
    {
        var shadows = Tweened(BoxShadows.Parse("0 2 4 0 #40000000"), "0 10 20 0 #80000000", .5);

        Assert.Equal(1, shadows.Count);
        Assert.Equal(6, shadows[0].OffsetY);
        Assert.Equal(12, shadows[0].Blur);
        Assert.Equal(96, shadows[0].Color.A);
    }

    [AvaloniaFact]
    public void AnAddedShadowGrowsInItsOwnColorFromNothing()
    {
        var shadows = Tweened(BoxShadows.Parse("0 2 4 0 #FF0000"), "0 2 4 0 #FF0000, 0 0 20 0 #0000FF", .5);

        Assert.Equal(2, shadows.Count);
        Assert.Equal(10, shadows[1].Blur);
        Assert.Equal(Color.FromArgb(128, 0, 0, 255), shadows[1].Color);
    }

    [AvaloniaFact]
    public void AShadowTakesItsInsetFromTheEndValue() =>
        Assert.True(Tweened(BoxShadows.Parse("0 0 10 0 #000000"), "inset 0 0 10 0 #000000", .5)[0].IsInset);

    [AvaloniaFact]
    public void ABorderWithoutAShadowTweensOneIn()
    {
        var border = new Border();

        var animation = border.Animate(new() { ["BoxShadow"] = "0 4 16 0 #000000", Duration = 100, Ease = "linear", Autoplay = false });
        animation.Seek(50);

        Assert.Equal(1, border.BoxShadow.Count);
        Assert.Equal(2, border.BoxShadow[0].OffsetY);
        Assert.Equal(8, border.BoxShadow[0].Blur);
        Assert.Equal(128, border.BoxShadow[0].Color.A);
    }

    private static LinearGradientBrush Linear(Point start, Point end, params (double Offset, Color Color)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(start, RelativeUnit.Relative),
            EndPoint = new RelativePoint(end, RelativeUnit.Relative),
        };
        foreach (var (offset, color) in stops) brush.GradientStops.Add(new GradientStop(color, offset));
        return brush;
    }

    private static RadialGradientBrush Radial(RelativePoint center, double radius) => new()
    {
        Center = center,
        GradientOrigin = center,
        RadiusX = new RelativeScalar(radius, RelativeUnit.Relative),
        RadiusY = new RelativeScalar(radius, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.FromRgb(200, 0, 0), 0), new GradientStop(Colors.Black, 1) },
    };

    private static ConicGradientBrush Conic(double angle) => new()
    {
        Angle = angle,
        GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Colors.Black, 1) },
    };

    [AvaloniaFact]
    public void ColorsConvertBetweenPrestoAndAvaloniaChannels()
    {
        var color = Color.FromArgb(128, 255, 107, 107);
        Assert.Equal(new PrestoColor(255, 107, 107, 128 / 255d), color.ToPresto());
        Assert.Equal(color, color.ToPresto().ToAvalonia());
        Assert.Equal(Color.FromRgb(255, 0, 100), new PrestoColor(254.6, .4, 100).ToAvalonia());
    }

    [AvaloniaFact]
    public void PrestoEasingSamplesTheWrappedEase()
    {
        var ease = Eases.OutQuad;
        var easing = new PrestoEasing(ease);

        foreach (var t in new[] { 0, .25, .5, .75, 1d }) Assert.Equal(ease(t), easing.Ease(t));
    }
}

/// <summary>A target whose single property carries the Avalonia type under test.</summary>
internal sealed class ValueBox<T>
{
    public T Value { get; set; } = default!;
}
