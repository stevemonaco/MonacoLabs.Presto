using Avalonia;
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
