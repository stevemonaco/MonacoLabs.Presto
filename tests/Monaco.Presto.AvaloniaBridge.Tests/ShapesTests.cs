using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Shapes;
using Monaco.Presto.AvaloniaBridge.Text;
using Monaco.Presto.AvaloniaBridge.Values;
using Path = Avalonia.Controls.Shapes.Path;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Covers <c>PrestoAvalonia.CreateDrawable</c>, <c>PrestoAvalonia.CreateMotionPath</c> and <c>PrestoAvalonia.MorphTo</c>, the
/// parts of tests/suites/svg.test.js that do not need a browser: the "stroke-linecap" drawable test, the
/// "svg.createMotionPath with offset" test and the morphTo tests, plus the Avalonia-specific stroke dash and revert behavior.
/// </summary>
public sealed class ShapesTests : BridgeTestBase
{
    private static readonly Typeface Font = new(FontFamily.Default);

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
    public void AThicknessChangeAfterADrawKeepsOneDashPerContour()
    {
        var path = CreatePath();
        var length = path.RenderedGeometry!.ContourLength;
        PrestoAvalonia.CreateDrawable(path, .25, 1);

        path.StrokeThickness = .001;

        Assert.Equal(.75 * length / .001, path.StrokeDashArray![0], 3);
        Assert.Equal(-.25 * length / .001, path.StrokeDashOffset, 3);
    }

    [AvaloniaFact]
    public void ARevertedDrawNoLongerFollowsTheThickness()
    {
        var path = CreatePath();
        var animation = path.Animate(new() { ["Draw"] = "0 1", Duration = 100, Autoplay = false });
        animation.Seek(100);

        animation.Revert();
        path.StrokeThickness = .001;

        Assert.Null(path.StrokeDashArray);
    }

    [AvaloniaFact]
    public void DrawOnAnOutlinePieceWalksTheDashOverItsLongestFigure()
    {
        var block = new TextBlock { Text = "i", FontSize = 100 };
        Show(block);
        var piece = (Path)PrestoAvalonia.SplitText(block, new() { Words = null, Chars = new(), Outlines = true }).Chars[0];
        piece.Stroke = Brushes.Black;
        piece.StrokeThickness = 2;
        var figures = TextSplitter.ContoursOf(piece)!.Contours;
        var longest = figures.Max(f => f.Length);
        Assert.True(figures.Length > 1);
        Assert.True(longest > piece.RenderedGeometry!.ContourLength + 1);

        var animation = PrestoAvalonia.CreateDrawable(piece).Animate(new()
        {
            ["Draw"] = TweenValue.FromTo("0 0", "0 1"),
            Duration = 100,
            Ease = Eases.Linear(),
            Autoplay = false,
        });

        animation.Seek(50);
        Assert.Equal(longest / 2 / piece.StrokeThickness, piece.StrokeDashArray![0], 3);
        animation.Seek(100);
        Assert.Equal(longest / piece.StrokeThickness, piece.StrokeDashArray[0], 3);
        Assert.Equal(0, piece.StrokeDashArray[1], 3);
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

    [AvaloniaFact]
    public void SingleContourMorphKeepsItsOpenPolylineSamples()
    {
        var path = CreatePath();

        var animation = path.Animate(new()
        {
            ["Data"] = PrestoAvalonia.MorphTo(Geometry.Parse("M 0,0 L 300,0"), .02),
            Duration = 100,
            Autoplay = false,
        });
        animation.Seek(animation.Duration);

        var tween = animation._headTween!;
        Assert.Equal([0, 0, 40, 0, 80, 0, 100, 20, 100, 60, 100, 100], tween._fromNumbers);
        Assert.Equal("M0,0L60,0L120,0L180,0L240,0L300,0", tween.ComposeComplexString());
        Assert.Equal(300, path.Data!.ContourLength, 3);
    }

    [AvaloniaFact]
    public void MorphKeepsAHoleThroughoutAGlyphMorph()
    {
        var path = new Path { Data = PrestoAvalonia.TextOutline("O", Font, 100), Stretch = Stretch.None };

        var animation = path.Animate(new()
        {
            ["Data"] = PrestoAvalonia.MorphTo(PrestoAvalonia.TextOutline("8", Font, 100)),
            Duration = 100,
            Ease = Eases.None,
            Autoplay = false,
        });

        for (var time = 0d; time <= 100; time += 25)
        {
            animation.Seek(time);
            var hole = GlyphContours.From(path.Data!)!.Contours.Where(c => c.IsHole).MaxBy(c => Math.Abs(c.SignedArea))!;
            Assert.True(Math.Abs(hole.SignedArea) > 100, $"no hole at {time}");
            Assert.False(path.Data!.FillContains(hole.Centroid), $"hole filled at {time}");
        }
    }

    [AvaloniaFact]
    public void MorphCollapsesAContourWithoutAPartnerToAPoint()
    {
        var destination = PrestoAvalonia.TextOutline("l", Font, 100);
        var path = new Path { Data = PrestoAvalonia.TextOutline("i", Font, 100), Stretch = Stretch.None };

        var (from, to, _) = ContourMorph.Of(path.Data, destination, GeometryCodec.DefaultPrecision)!.Value;
        var collapsed = Array.FindIndex(FiguresOf(to), f => f.All(p => p == f[0]));
        Assert.True(collapsed >= 0);
        Assert.True(Width(FiguresOf(from)[collapsed]) > 1);

        var animation = path.Animate(new() { ["Data"] = PrestoAvalonia.MorphTo(destination), Duration = 100, Autoplay = false });
        animation.Seek(animation.Duration);
        Assert.True(destination.Bounds.Inflate(1).Contains(path.Data.Bounds));
    }

    [AvaloniaFact]
    public void MorphPairsOutersWithOutersAndHolesWithHoles()
    {
        var from = Geometry.Parse("M0,0 L100,0 L100,100 L0,100 Z M40,40 L40,60 L60,60 L60,40 Z");
        var to = Geometry.Parse("M30,30 L30,70 L70,70 L70,30 Z M0,0 L100,0 L100,100 L0,100 Z");

        var (fromData, toData, _) = ContourMorph.Of(from, to, GeometryCodec.DefaultPrecision)!.Value;

        Assert.Equal([(100d, 100d), (20d, 40d)], FiguresOf(fromData).Zip(FiguresOf(toData), (f, t) => (Width(f), Width(t))));

        var path = new Path { Data = from, Stretch = Stretch.None };
        var animation = path.Animate(new() { ["Data"] = PrestoAvalonia.MorphTo(to), Duration = 100, Autoplay = false });
        Assert.Equal(PrestoAvalonia.MorphPointCount(from, to), animation._headTween!._toNumbers.Length / 2);
    }

    [AvaloniaFact]
    public void AHoleWithoutAPartnerCollapsesIntoInk()
    {
        var destination = PrestoAvalonia.TextOutline("0", Font, 100);

        var (_, to, _) = ContourMorph.Of(PrestoAvalonia.TextOutline("B", Font, 100), destination, GeometryCodec.DefaultPrecision)!.Value;

        var collapsed = FiguresOf(to).Single(f => f.All(p => p == f[0]));
        Assert.True(destination.FillContains(collapsed[0]));
    }

    [AvaloniaFact]
    public void OverlappingOuterContoursStayFilledMidMorph()
    {
        var squares = Geometry.Parse("M0,0 L60,0 L60,60 L0,60 Z M30,30 L90,30 L90,90 L30,90 Z");
        var path = new Path { Data = squares, Stretch = Stretch.None };

        var animation = path.Animate(new() { ["Data"] = PrestoAvalonia.MorphTo(squares), Duration = 100, Autoplay = false });
        animation.Seek(50);

        Assert.True(path.Data!.FillContains(new Point(45, 45)));
    }

    [AvaloniaFact]
    public void MorphWindsEachContourByItsRoleAndRotatesItToTheLeastTravel()
    {
        var from = Geometry.Parse("M0,0 L30,0 L30,30 L0,30 Z M100,0 L130,0 L130,30 L100,30 Z");
        var to = Geometry.Parse("M30,30 L30,0 L0,0 L0,30 Z M130,30 L130,0 L100,0 L100,30 Z");

        var (fromData, toData, _) = ContourMorph.Of(from, to, GeometryCodec.DefaultPrecision)!.Value;

        var travel = FiguresOf(fromData).Zip(FiguresOf(toData), (f, t) => f.Zip(t, (p, q) => ((Vector)(p - q)).SquaredLength).Sum()).Sum();
        Assert.Equal(0, travel, 3);
    }

    private static Point[][] FiguresOf(string data) =>
        data.Split('M', StringSplitOptions.RemoveEmptyEntries)
            .Select(figure => figure.TrimEnd('Z').Split('L').Select(Point.Parse).ToArray())
            .ToArray();

    private static double Width(Point[] points) => points.Max(p => p.X) - points.Min(p => p.X);

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
