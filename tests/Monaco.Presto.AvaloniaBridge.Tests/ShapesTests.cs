using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Covers <c>PrestoAvalonia.CreateDrawable</c>, <c>PrestoAvalonia.CreateMotionPath</c> and <c>PrestoAvalonia.MorphTo</c>, the
/// parts of tests/suites/svg.test.js that do not need a browser: the "stroke-linecap" drawable test, the
/// "svg.createMotionPath with offset" test and the morphTo tests, plus the Avalonia-specific stroke dash and revert behavior.
/// </summary>
public sealed class ShapesTests : BridgeTestBase
{
    private static Path CreatePath() => new()
    {
        Data = Geometry.Parse("M 0,0 L 100,0 L 100,100"),
        Stretch = Stretch.None,
        Stroke = Brushes.Black,
        StrokeThickness = 2,
    };

    [AvaloniaFact]
    public void DrawWalksTheDashArrayFromHiddenToFullyDrawn()
    {
        var path = CreatePath();
        var length = path.RenderedGeometry!.ContourLength;
        Assert.True(length > 0);

        var animation = PrestoAvalonia.CreateDrawable(path).Animate(new()
        {
            ["Draw"] = TweenValue.FromTo("0 0", "0 1"),
            Duration = 100,
            Autoplay = false,
        });

        animation.Seek(0);
        Assert.Equal(0, path.StrokeDashArray![0]);
        var drawn = 0d;
        for (var time = 10d; time < 100; time += 10)
        {
            animation.Seek(time);
            Assert.True(path.StrokeDashArray![0] > drawn);
            drawn = path.StrokeDashArray[0];
            Assert.Equal(0, path.StrokeDashOffset);
        }

        animation.Seek(animation.Duration);
        Assert.Equal(length / path.StrokeThickness, path.StrokeDashArray![0], 3);
        Assert.Equal(0, path.StrokeDashArray[1], 3);
    }

    [AvaloniaFact]
    public void DrawWalksTheDashOffsetAsTheStartMoves()
    {
        var path = CreatePath();
        var length = path.RenderedGeometry!.ContourLength;

        var animation = PrestoAvalonia.CreateDrawable(path, 0, 1).Animate(new()
        {
            ["Draw"] = "1 1",
            Duration = 100,
            Autoplay = false,
        });

        var offset = 0d;
        for (var time = 10d; time <= 100; time += 10)
        {
            animation.Seek(time);
            Assert.True(path.StrokeDashOffset < offset);
            offset = path.StrokeDashOffset;
        }

        Assert.Equal(-length / path.StrokeThickness, offset, 3);
    }

    [AvaloniaFact]
    public void LineCapIsFlattenedWhileTheStartAndEndMatch()
    {
        var path = CreatePath();
        path.StrokeLineCap = PenLineCap.Round;

        PrestoAvalonia.CreateDrawable(path, 0, .5);
        Assert.Equal(PenLineCap.Round, path.StrokeLineCap);
        PrestoAvalonia.CreateDrawable(path, 0, 1);
        Assert.Equal(PenLineCap.Round, path.StrokeLineCap);
        PrestoAvalonia.CreateDrawable(path, .5, 1);
        Assert.Equal(PenLineCap.Round, path.StrokeLineCap);
        PrestoAvalonia.CreateDrawable(path, 1, 1);
        Assert.Equal(PenLineCap.Flat, path.StrokeLineCap);
        PrestoAvalonia.CreateDrawable(path, .25, .25);
        Assert.Equal(PenLineCap.Flat, path.StrokeLineCap);
    }

    [AvaloniaFact]
    public void RevertRestoresTheStrokeStateTheShapeHadBeforeDrawing()
    {
        var path = CreatePath();
        path.StrokeLineCap = PenLineCap.Round;
        path.StrokeDashArray = [4, 2];
        path.StrokeDashOffset = 3;

        var animation = PrestoAvalonia.CreateDrawable(path).Animate(new()
        {
            ["Draw"] = "0 1",
            Duration = 100,
            Autoplay = false,
        });
        animation.Seek(50);
        animation.Revert();

        Assert.Equal([4d, 2d], path.StrokeDashArray!);
        Assert.Equal(3, path.StrokeDashOffset);
        Assert.Equal(PenLineCap.Round, path.StrokeLineCap);
    }

    [AvaloniaFact]
    public void MotionPathTranslatesAndRotatesAlongTheContour()
    {
        var path = CreatePath();
        var geometry = path.RenderedGeometry!;
        var rider = new Border();
        var motion = PrestoAvalonia.CreateMotionPath(path);

        var animation = rider.Animate(new()
        {
            ["X"] = motion.X,
            ["Y"] = motion.Y,
            ["Rotate"] = motion.Rotate,
            Duration = 100,
            Ease = Eases.None, // so the seeked time is the progress along the contour
            Autoplay = false,
        });

        foreach (var progress in (double[])[0, .5, 1])
        {
            animation.Seek(animation.Duration * progress);
            var distance = geometry.ContourLength * progress;
            Assert.True(geometry.TryGetPointAndTangentAtDistance(distance, out var point, out var tangent));
            Assert.Equal(point.X, Transform<TranslateTransform>(rider).X, 3);
            Assert.Equal(point.Y, Transform<TranslateTransform>(rider).Y, 3);
            Assert.Equal(Math.Atan2(tangent.Y, tangent.X) * 180 / Math.PI, Transform<RotateTransform>(rider).Angle, 3);
        }
    }

    [AvaloniaFact]
    public void MotionPathOffsetWrapsAroundInsteadOfClamping()
    {
        var path = CreatePath();
        var geometry = path.RenderedGeometry!;
        var rider = new Border();
        var motion = PrestoAvalonia.CreateMotionPath(path, .5);

        var animation = rider.Animate(new()
        {
            ["X"] = motion.X,
            ["Y"] = motion.Y,
            Duration = 100,
            Autoplay = false,
        });

        animation.Seek(0);
        var start = new Point(Transform<TranslateTransform>(rider).X, Transform<TranslateTransform>(rider).Y);
        animation.Seek(animation.Duration);
        var end = new Point(Transform<TranslateTransform>(rider).X, Transform<TranslateTransform>(rider).Y);

        Assert.True(geometry.TryGetPointAtDistance(geometry.ContourLength * .5, out var middle));
        Assert.Equal(middle.X, start.X, 3);
        Assert.Equal(middle.Y, start.Y, 3);
        Assert.Equal(start.X, end.X, 3);
        Assert.Equal(start.Y, end.Y, 3);
    }

    [AvaloniaFact]
    public void PointMotionPathDrivesTheShorthandsThroughThePoints()
    {
        Point[] points = [new(0, 0), new(100, 60), new(200, 0)];
        var path = MotionPath.Through([(0, 0), (100, 60), (200, 0)]);
        var rider = new Border();
        var motion = PrestoAvalonia.CreateMotionPath(points, autoRotate: 90);

        var animation = rider.Animate(new()
        {
            ["X"] = motion.X,
            ["Y"] = motion.Y,
            ["Rotate"] = motion.Rotate,
            Duration = 100,
            Ease = Eases.None,
            Autoplay = false,
        });

        foreach (var progress in (double[])[0, .5, 1])
        {
            animation.Seek(animation.Duration * progress);
            path.Sample(path.Length * progress, out var x, out var y, out var angle);
            Assert.Equal(x, Transform<TranslateTransform>(rider).X, 3);
            Assert.Equal(y, Transform<TranslateTransform>(rider).Y, 3);
            Assert.Equal(angle + 90, Transform<RotateTransform>(rider).Angle, 3);
        }

        AssertPoint(new Point(100, 60), PointAt(PrestoAvalonia.MotionPathGeometry(points), .5));
    }

    [AvaloniaFact]
    public void MorphWalksThePointsOntoTheDestinationGeometry()
    {
        var path = CreatePath();
        var destination = Geometry.Parse("M 0,100 L 100,100 L 100,0");

        var animation = path.Animate(new()
        {
            ["Data"] = PrestoAvalonia.MorphTo(destination),
            Duration = 100,
            Ease = Eases.None,
            Autoplay = false,
        });

        var target = PointAt(destination, 0);
        var remaining = double.MaxValue;
        for (var time = 0d; time <= 100; time += 10)
        {
            animation.Seek(time);
            var start = PointAt(path.Data!, 0);
            var distance = Math.Abs(start.X - target.X) + Math.Abs(start.Y - target.Y);
            Assert.True(distance < remaining);
            remaining = distance;
        }

        // The morph lands on a polyline through the destination's contour, so only its endpoints are the destination's own.
        AssertPoint(PointAt(destination, 0), PointAt(path.Data!, 0));
        AssertPoint(PointAt(destination, 1), PointAt(path.Data!, 1));
    }

    [AvaloniaFact]
    public void MorphInATimelineStartsFromThePreviousChildsShape()
    {
        var path = CreatePath();
        var first = Geometry.Parse("M 0,100 L 100,100 L 100,0");
        var second = Geometry.Parse("M 50,0 L 50,100 L 150,100");

        var timeline = Presto.CreateTimeline(new() { Defaults = new() { Duration = 100, Ease = Eases.None }, Autoplay = false });
        timeline.Add(path, new AvaloniaAnimationParams { ["Data"] = PrestoAvalonia.MorphTo(first) });
        timeline.Add(path, new AvaloniaAnimationParams { ["Data"] = PrestoAvalonia.MorphTo(second) });

        timeline.Seek(150);
        AssertPoint(new Point(25, 50), PointAt(path.Data!, 0));
        timeline.Seek(timeline.Duration);
        AssertPoint(PointAt(second, 0), PointAt(path.Data!, 0));
    }

    [AvaloniaFact]
    public void MorphKeyframesStartFromThePreviousFramesShape()
    {
        var path = CreatePath();
        var first = Geometry.Parse("M 0,100 L 100,100 L 100,0");
        var second = Geometry.Parse("M 50,0 L 50,100 L 150,100");

        var animation = path.Animate(new()
        {
            ["Data"] = TweenValue.Keyframes(
                new TweenParams { To = PrestoAvalonia.MorphTo(first) },
                new TweenParams { To = PrestoAvalonia.MorphTo(second) }),
            Duration = 100,
            Ease = Eases.None,
            Autoplay = false,
        });

        animation.Seek(50);
        AssertPoint(PointAt(first, 0), PointAt(path.Data!, 0));
        animation.Seek(75);
        AssertPoint(new Point(25, 50), PointAt(path.Data!, 0));
    }

    [AvaloniaFact]
    public void MorphReadsTheDataOfAnyGeometryProperty()
    {
        var icon = new PathIcon { Data = Geometry.Parse("M 0,0 L 100,0 L 100,100") };
        var destination = Geometry.Parse("M 0,100 L 100,100 L 100,0");

        var animation = icon.Animate(new() { ["Data"] = PrestoAvalonia.MorphTo(destination), Duration = 100, Autoplay = false });
        animation.Seek(animation.Duration);

        AssertPoint(PointAt(destination, 0), PointAt(icon.Data!, 0));
        AssertPoint(PointAt(destination, 1), PointAt(icon.Data!, 1));
    }

    [AvaloniaFact]
    public void MorphPointCountIsTheNumberOfPointsTheMorphSamples()
    {
        var path = CreatePath();
        var destination = Geometry.Parse("M 0,0 L 300,0");
        var expected = PrestoAvalonia.MorphPointCount(path.Data!, destination);

        var animation = path.Animate(new() { ["Data"] = PrestoAvalonia.MorphTo(destination), Duration = 100, Autoplay = false });

        Assert.True(expected > 2);
        Assert.Equal(expected, animation._headTween!._toNumbers.Length / 2);
    }

    /// <summary>Upstream's <c>precision: 0</c> morphs the raw points attribute, which an Avalonia <c>Geometry</c> cannot carry.</summary>
    [AvaloniaFact]
    public void MorphRejectsAPrecisionOfZero()
    {
        var destination = Geometry.Parse("M 0,100 L 100,100 L 100,0");

        Assert.Throws<ArgumentOutOfRangeException>(() => PrestoAvalonia.MorphTo(destination, 0));
    }

    [AvaloniaFact]
    public void MorphRevertRestoresTheDataThePathStartedWith()
    {
        var path = CreatePath();
        var original = path.Data;

        var animation = path.Animate(new()
        {
            ["Data"] = PrestoAvalonia.MorphTo(Geometry.Parse("M 0,100 L 100,100 L 100,0")),
            Duration = 100,
            Autoplay = false,
        });
        animation.Seek(animation.Duration);
        Assert.NotSame(original, path.Data);

        animation.Revert();
        Assert.Same(original, path.Data);
    }

    private static T Transform<T>(Visual visual) where T : Avalonia.Media.Transform =>
        Assert.IsType<TransformGroup>(visual.RenderTransform).Children.OfType<T>().Single();

    private static Point PointAt(Geometry geometry, double fraction)
    {
        Assert.True(geometry.TryGetPointAtDistance(geometry.ContourLength * fraction, out var point));
        return point;
    }

    private static void AssertPoint(Point expected, Point actual)
    {
        Assert.Equal(expected.X, actual.X, 2);
        Assert.Equal(expected.Y, actual.Y, 2);
    }
}
