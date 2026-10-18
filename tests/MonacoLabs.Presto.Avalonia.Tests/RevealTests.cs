using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Tests;

/// <summary>The reveals: <c>ClipInset</c>, <c>ClipCircle</c> and <c>ClipCircleCenter</c> over one bridge-owned clip, and <c>MaskSweep</c> over one bridge-owned opacity mask (MOTION-KIT-PLAN §3.4).</summary>
public sealed class RevealTests : BridgeTestBase
{
    private static Border ShownBox()
    {
        var box = Box();
        Show(box);
        return box;
    }

    [AvaloniaFact]
    public void ADipInsetAnimatesTheRectangle()
    {
        var box = ShownBox();

        Seeked(box, new() { ["ClipInset"] = TweenValue.Of(new Thickness(10, 4, 20, 8)) }, 0.5);

        Assert.Equal(new Rect(5, 2, 35, 44), Assert.IsType<RectangleGeometry>(box.Clip).Rect);
    }

    [AvaloniaFact]
    public void APercentInsetFollowsAResize()
    {
        var box = ShownBox();
        Seeked(box, new() { ["ClipInset"] = "0 0 50% 10%" });
        var clip = Assert.IsType<RectangleGeometry>(box.Clip);
        Assert.Equal(new Rect(0, 0, 25, 45), clip.Rect);

        box.Width = 100;
        box.Height = 80;
        Dispatcher.UIThread.RunJobs();

        Assert.Same(clip, box.Clip);
        Assert.Equal(new Rect(0, 0, 50, 72), clip.Rect);
    }

    [AvaloniaFact]
    public void AnInsetBackToZeroEndsWithNoClip()
    {
        var box = ShownBox();
        Seeked(box, new() { ["ClipInset"] = "10%" });

        var closing = Seeked(box, new() { ["ClipInset"] = 0 }, 0.99);
        Assert.NotNull(box.Clip);
        closing.Seek(closing.Duration);

        Assert.Null(box.Clip);
    }

    [AvaloniaFact]
    public void ACircleRevealEndsWithNoClip()
    {
        var box = ShownBox();

        var reveal = Seeked(box, new() { ["ClipCircle"] = TweenValue.FromTo(0, 1) }, 0.5);
        var radius = Math.Sqrt(25 * 25 * 2) / 2;
        Assert.Equal(new Rect(25 - radius, 25 - radius, radius * 2, radius * 2), Assert.IsType<EllipseGeometry>(box.Clip).Rect);
        reveal.Seek(reveal.Duration);

        Assert.Null(box.Clip);
    }

    [AvaloniaFact]
    public void TheCircleCenterMovesTheEllipse()
    {
        var box = ShownBox();

        Seeked(box, new() { ["ClipCircle"] = 0.5, ["ClipCircleCenter"] = TweenValue.Of(RelativePoint.TopLeft) });

        var farthest = Math.Sqrt(50 * 50 * 2);
        Assert.Equal(new Rect(-farthest / 2, -farthest / 2, farthest, farthest), Assert.IsType<EllipseGeometry>(box.Clip).Rect);
    }

    [AvaloniaFact]
    public void TheCircleCenterAloneLeavesNoClip()
    {
        var box = ShownBox();

        Seeked(box, new() { ["ClipCircleCenter"] = TweenValue.Of(RelativePoint.TopLeft) }, 0.5);

        Assert.Null(box.Clip);
    }

    [AvaloniaTheory]
    [InlineData("ClipInset", "20%")]
    [InlineData("ClipCircle", "0.5")]
    public void RevertRestoresTheUsersClip(string shorthand, string value)
    {
        var own = new RectangleGeometry(new Rect(0, 0, 10, 10));
        var box = Box();
        box.Clip = own;
        Show(box);

        var animation = Seeked(box, new() { [shorthand] = value });
        Assert.NotSame(own, box.Clip);

        animation.Revert();

        Assert.Same(own, box.Clip);
    }

    [AvaloniaFact]
    public void AnInsetAndACircleTogetherThrow()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new Border().Animate(new() { ["ClipInset"] = 4, ["ClipCircle"] = 0.5 }));

        Assert.Contains("ClipInset", error.Message);
        Assert.Contains("ClipCircle", error.Message);
    }

    [AvaloniaFact]
    public void AMaskSweepEndsWithNoMask()
    {
        var box = ShownBox();

        var sweep = Seeked(box, new() { ["MaskSweep"] = TweenValue.FromTo(0, 1) }, 0.99);
        Assert.IsType<LinearGradientBrush>(box.OpacityMask);
        sweep.Seek(sweep.Duration);

        Assert.Null(box.OpacityMask);
    }

    [AvaloniaTheory]
    [InlineData("MaskSweepAngle", 90)]
    [InlineData("MaskSweepFeather", 0.5)]
    public void AMaskCompanionAloneLeavesNoMask(string shorthand, double value)
    {
        var box = ShownBox();

        Seeked(box, new() { [shorthand] = value }, 0.5);

        Assert.Null(box.OpacityMask);
    }

    [AvaloniaFact]
    public void RevertingTheSweepWhileACompanionRunsLeavesNoMask()
    {
        var box = ShownBox();
        var sweep = Seeked(box, new() { ["MaskSweep"] = TweenValue.FromTo(0, 0.5) });
        Seeked(box, new() { ["MaskSweepAngle"] = 90 }, 0.5);
        Assert.IsType<LinearGradientBrush>(box.OpacityMask);

        sweep.Revert();

        Assert.Null(box.OpacityMask);
    }

    [AvaloniaTheory]
    [InlineData(0, 0.4, 0.5, 0.6, 0.5)]
    [InlineData(90, 0.5, 0.4, 0.5, 0.6)]
    [InlineData(180, 0.6, 0.5, 0.4, 0.5)]
    public void TheSweepEdgeSitsAtProgressAlongTheAngle(double angle, double startX, double startY, double endX, double endY)
    {
        var box = ShownBox();

        Seeked(box, new() { ["MaskSweep"] = TweenValue.FromTo(0, 1), ["MaskSweepAngle"] = TweenValue.FromTo(angle, angle) }, 0.5);

        var mask = Assert.IsType<LinearGradientBrush>(box.OpacityMask);
        AssertPoint(startX, startY, mask.StartPoint);
        AssertPoint(endX, endY, mask.EndPoint);
    }

    [AvaloniaFact]
    public void TheFeatherWidensTheEdgeAndTheAngleTakesTurns()
    {
        var box = ShownBox();

        Seeked(box, new() { ["MaskSweep"] = TweenValue.FromTo(0.5, 0.5), ["MaskSweepAngle"] = "0.25turn", ["MaskSweepFeather"] = 0.5 });

        var mask = Assert.IsType<LinearGradientBrush>(box.OpacityMask);
        AssertPoint(0.5, 0.25, mask.StartPoint);
        AssertPoint(0.5, 0.75, mask.EndPoint);
    }

    [AvaloniaFact]
    public void RevertRestoresTheUsersMask()
    {
        var own = new SolidColorBrush(Colors.Black);
        var box = Box();
        box.OpacityMask = own;
        Show(box);

        var animation = Seeked(box, new() { ["MaskSweep"] = TweenValue.FromTo(0, 1) }, 0.5);
        Assert.NotSame(own, box.OpacityMask);

        animation.Revert();

        Assert.Same(own, box.OpacityMask);
    }

    [AvaloniaTheory]
    [InlineData("Blur")]
    [InlineData("RotateY")]
    [InlineData("ClipInset")]
    [InlineData("MaskSweep")]
    public void TheCompositorRejectsTheShorthandsOnlyTheBridgeRenders(string shorthand) =>
        Assert.Throws<NotSupportedException>(() => new Border().AnimateOnCompositor(new() { [shorthand] = 1, Duration = 100 }));

    [AvaloniaFact]
    public void TheMaskShorthandsAnimateAlongsideEachOtherAndTheEffectAndClipShorthands()
    {
        var box = ShownBox();

        Seeked(box, new()
        {
            ["MaskSweep"] = TweenValue.FromTo(0, 0.5), ["MaskSweepAngle"] = 90, ["MaskSweepFeather"] = 0.5,
            ["Blur"] = 4, ["ClipInset"] = 4,
        }, 0.5);
        Seeked(box, new() { ["MaskSweep"] = TweenValue.FromTo(0, 0.5), ["ShadowBlur"] = 4, ["ClipCircle"] = 0.5 }, 0.5);

        Assert.IsType<LinearGradientBrush>(box.OpacityMask);
        Assert.IsType<DropShadowEffect>(box.Effect);
        Assert.IsType<EllipseGeometry>(box.Clip);
    }

    [AvaloniaFact]
    public void AnUnanimatedClipCircleReadsAndStartsFromOne()
    {
        var box = ShownBox();
        Assert.Equal(1d, Convert.ToDouble(Presto.Get(box, "ClipCircle", withUnit: false)));

        Seeked(box, new() { ["ClipCircle"] = 0 }, 0.5);

        var farthest = Math.Sqrt(25 * 25 * 2);
        Assert.Equal(farthest / 2, Assert.IsType<EllipseGeometry>(box.Clip).Rect.Width / 2, 9);
    }

    [AvaloniaFact]
    public void AnUnanimatedMaskSweepReadsAndStartsFromOneAndSweepsLeftToRight()
    {
        var box = ShownBox();
        Assert.Equal(1d, Convert.ToDouble(Presto.Get(box, "MaskSweep", withUnit: false)));

        Seeked(box, new() { ["MaskSweep"] = 0 }, 0.5);

        var mask = Assert.IsType<LinearGradientBrush>(box.OpacityMask);
        AssertPoint(0.4, 0.5, mask.StartPoint);
        AssertPoint(0.6, 0.5, mask.EndPoint);
    }

    private static void AssertPoint(double x, double y, RelativePoint actual)
    {
        Assert.Equal(RelativeUnit.Relative, actual.Unit);
        Assert.Equal(x, actual.Point.X, 1e-9);
        Assert.Equal(y, actual.Point.Y, 1e-9);
    }
}
