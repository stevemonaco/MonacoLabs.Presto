using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Text;
using Path = Avalonia.Controls.Shapes.Path;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary><see cref="TextWarp"/> and the warp shorthands: each kind's function, when outlines are written and given back, Jelly, reduced motion.</summary>
public sealed class TextWarpTests : BridgeTestBase
{
    private const double Tolerance = 1e-9;
    private static readonly Size Block = new(200, 100);

    private static TextBlock Split(TextWarpKind kind)
    {
        var block = new TextBlock { Text = "AOB", FontSize = 40 };
        Show(block);
        PrestoAvalonia.SplitText(block, new() { Words = null, Chars = new(), Outlines = true });
        TextWarp.SetKind(block, kind);
        return block;
    }

    private static Path Piece(TextBlock block, int index) => Assert.IsType<Path>(TextSplitter.Of(block)!.Chars[index]);

    private static Geometry?[] Outlines(TextBlock block) => [.. TextSplitter.Of(block)!.Chars.Select(c => ((Path)c).Data)];

    private static Point[] Points(Geometry geometry) => [.. GlyphContours.From(geometry)!.Contours.SelectMany(c => c.Points)];

    private static Point Warp(TextWarpKind kind, Point point, double amount, double phase = 0, Point origin = default, double radius = 100) =>
        WarpComposer.Warp(kind, point, amount, phase, origin, radius, Block, 40);

    private static void AssertPoint(Point expected, Point actual, double tolerance = Tolerance)
    {
        Assert.Equal(expected.X, actual.X, tolerance);
        Assert.Equal(expected.Y, actual.Y, tolerance);
    }

    [AvaloniaFact]
    public void WaveDisplacesVerticallyAlongASineThatTravelsWithThePhase()
    {
        AssertPoint(new Point(25, 10 + 20), Warp(TextWarpKind.Wave, new Point(25, 10), 1));
        AssertPoint(new Point(25, 10 + 10), Warp(TextWarpKind.Wave, new Point(25, 10), 0.5));
        AssertPoint(new Point(25, 10), Warp(TextWarpKind.Wave, new Point(25, 10), 1, phase: Math.PI / 2));
        AssertPoint(new Point(50, 10 + 20), Warp(TextWarpKind.Wave, new Point(50, 10), 1, phase: Math.PI / 2));
    }

    [AvaloniaFact]
    public void BendArcsTheCenterLineSweepingHalfACircleAtOne()
    {
        var bend = Block.Width / Math.PI;

        AssertPoint(new Point(100, 50), Warp(TextWarpKind.Bend, new Point(100, 50), 1));
        AssertPoint(new Point(100 + bend, 50 + bend), Warp(TextWarpKind.Bend, new Point(100 + bend * Math.PI / 2, 50), 1));
        AssertPoint(new Point(100 + bend + 10, 50 + bend), Warp(TextWarpKind.Bend, new Point(100 + bend * Math.PI / 2, 40), 1));
        AssertPoint(new Point(100 + bend, 50 - bend), Warp(TextWarpKind.Bend, new Point(100 + bend * Math.PI / 2, 50), -1));
    }

    [AvaloniaFact]
    public void BulgeMagnifiesWithinTheRadiusAndANegativeAmountPinches()
    {
        var origin = new Point(50, 50);

        AssertPoint(origin, Warp(TextWarpKind.Bulge, origin, 1, origin: origin));
        AssertPoint(new Point(50 + 10 * 1.81, 50), Warp(TextWarpKind.Bulge, new Point(60, 50), 1, origin: origin));
        AssertPoint(new Point(50 + 10 * (1 - 0.5 * 0.81), 50), Warp(TextWarpKind.Bulge, new Point(60, 50), -0.5, origin: origin));
        AssertPoint(new Point(160, 50), Warp(TextWarpKind.Bulge, new Point(160, 50), 1, origin: origin));
    }

    [AvaloniaFact]
    public void TwistTurnsEachPointByItsDistanceFromTheOrigin()
    {
        var half = Math.Sqrt(0.5) * 50;

        AssertPoint(new Point(0, 100), Warp(TextWarpKind.Twist, new Point(100, 0), Math.PI / 2));
        AssertPoint(new Point(half, half), Warp(TextWarpKind.Twist, new Point(50, 0), Math.PI / 2));
        AssertPoint(default, Warp(TextWarpKind.Twist, default, Math.PI / 2));
    }

    [AvaloniaFact]
    public void MeltDripsDownByColumnGrowingTowardsTheBottomAndWithTheAmount()
    {
        var drips = Enumerable.Range(0, 40).Select(x => Warp(TextWarpKind.Melt, new Point(x * 5, 100), 1)).ToArray();

        Assert.All(drips, (p, i) =>
        {
            Assert.Equal(i * 5, p.X);
            Assert.InRange(p.Y - 100, 0.25 * 40 - 1e-9, 40 + 1e-9);
        });
        Assert.True(drips.Max(p => p.Y) - drips.Min(p => p.Y) > 10, "the drips do not vary across columns");
        AssertPoint(new Point(37, 0), Warp(TextWarpKind.Melt, new Point(37, 0), 1));
        Assert.Equal(drips[7].Y - 100, 2 * (Warp(TextWarpKind.Melt, new Point(35, 50), 1).Y - 50), 1e-9);
        Assert.Equal(drips[7].Y - 100, 2 * (Warp(TextWarpKind.Melt, new Point(35, 100), 0.5).Y - 100), 1e-9);
    }

    [AvaloniaFact]
    public void PerspectiveNarrowsTheTopRelativeToTheBottom()
    {
        AssertPoint(new Point(50, 0), Warp(TextWarpKind.Perspective, new Point(0, 0), 0.5));
        AssertPoint(new Point(25, 50), Warp(TextWarpKind.Perspective, new Point(0, 50), 0.5));
        AssertPoint(new Point(0, 100), Warp(TextWarpKind.Perspective, new Point(0, 100), 0.5));
        AssertPoint(new Point(-50, 0), Warp(TextWarpKind.Perspective, new Point(0, 0), -0.5));
    }

    [AvaloniaFact]
    public void AFrameWritesEachPieceItsOutlineMappedInBlockCoordinates()
    {
        var block = Split(TextWarpKind.Wave);
        var split = TextSplitter.Of(block)!;

        Presto.Set(block, new AnimationParams { ["Warp"] = 1, ["WarpRadius"] = 60 });
        Frames.Advance(16);

        for (var i = 0; i < split.Chars.Count; i++)
        {
            var piece = Piece(block, i);
            var slot = split.SlotOf(piece).Position;
            var expected = TextSplitter.ContoursOf(piece)!.Contours.SelectMany(c => c.Points)
                .Select(p => WarpComposer.Warp(TextWarpKind.Wave, p + slot, 1, 0, default, 60, block.Bounds.Size, 40) - slot).ToArray();
            var bounds = piece.Data!.Bounds;
            Assert.Equal(expected.Min(p => p.X), bounds.Left, 0.01);
            Assert.Equal(expected.Min(p => p.Y), bounds.Top, 0.01);
            Assert.Equal(expected.Max(p => p.X), bounds.Right, 0.01);
            Assert.Equal(expected.Max(p => p.Y), bounds.Bottom, 0.01);
            Assert.IsType<StreamGeometry>(piece.Data);
        }
    }

    [AvaloniaFact]
    public void SeveralShorthandsMovingInOneFrameWriteOnce()
    {
        var block = Split(TextWarpKind.Bulge);
        var writes = 0;
        Piece(block, 0).PropertyChanged += (_, e) => writes += e.Property == Path.DataProperty ? 1 : 0;

        Presto.Set(block, new AnimationParams { ["Warp"] = 1, ["WarpPhase"] = 1, ["WarpRadius"] = 80, ["WarpOrigin"] = TweenValue.Of(new Point(20, 20)) });
        Frames.Advance(16);

        Assert.Equal(1, writes);
    }

    [AvaloniaFact]
    public void AtIdentityThePiecesKeepTheirOwnOutlines()
    {
        var block = Split(TextWarpKind.Bulge);
        var outlines = Outlines(block);

        Presto.Set(block, new AnimationParams { ["Warp"] = 0, ["WarpPhase"] = 2 });
        Frames.Advance(16);

        Assert.Equal(outlines, Outlines(block));
        Assert.Equal(0d, PrestoAvalonia.Get(block, "Warp"));
        Assert.Equal(new Point(block.Bounds.Width / 2, block.Bounds.Height / 2), PrestoAvalonia.Get(block, "WarpOrigin"));
        Assert.Equal(100d, PrestoAvalonia.Get(block, "WarpRadius"));
    }

    [AvaloniaFact]
    public void ReturningToZeroOrRevertingGivesTheOutlinesBack()
    {
        var block = Split(TextWarpKind.Bulge);
        var outlines = Outlines(block);

        Presto.Set(block, new AnimationParams { ["Warp"] = 1 });
        Frames.Advance(16);
        Assert.NotSame(outlines[0], Piece(block, 0).Data);
        Presto.Set(block, new AnimationParams { ["Warp"] = 0 });
        Frames.Advance(16);
        Assert.Equal(outlines, Outlines(block));

        var animation = Seeked(block, new AnimationParams { ["Warp"] = 1, ["WarpPhase"] = 1 });
        Frames.Advance(16);
        Assert.NotSame(outlines[0], Piece(block, 0).Data);
        animation.Revert();
        Frames.Advance(16);
        Assert.Equal(outlines, Outlines(block));
        Assert.Equal(0d, PrestoAvalonia.Get(block, "Warp"));
    }

    [AvaloniaFact]
    public void AReSplitWarpsTheNewPieces()
    {
        var block = Split(TextWarpKind.Wave);
        Presto.Set(block, new AnimationParams { ["Warp"] = 1 });
        Frames.Advance(16);

        TextSplitter.Of(block)!.Refresh();
        var fresh = Outlines(block);
        Frames.Advance(16);

        Assert.NotSame(fresh[0], Piece(block, 0).Data);
    }

    [AvaloniaFact]
    public void SeekingAPureKindBackReproducesTheFrame()
    {
        var block = Split(TextWarpKind.Twist);
        var animation = Seeked(block, new AnimationParams { ["Warp"] = 2, ["WarpOrigin"] = TweenValue.Of(new Point(10, 10)) }, 0.3);
        Frames.Advance(16);
        var first = Points(Piece(block, 1).Data!);

        animation.Seek(90);
        Frames.Advance(16);
        Assert.NotEqual(first, Points(Piece(block, 1).Data!));
        animation.Seek(30);
        Frames.Advance(16);

        Assert.Equal(first, Points(Piece(block, 1).Data!));
    }

    [AvaloniaFact]
    public void ReversingAPureKindPastAFrameReproducesIt()
    {
        var block = Split(TextWarpKind.Wave);
        var animation = Presto.Animate(block, new AnimationParams { ["Warp"] = 1, ["WarpPhase"] = 1, Duration = 320, Ease = "linear" });
        var forward = new Dictionary<double, Point[]>();
        for (var i = 0; i < 10; i++)
        {
            Frames.Advance(16);
            forward[animation.IterationTime] = Points(Piece(block, 1).Data!);
        }

        animation.Reverse();
        var matched = 0;
        for (var i = 0; i < 10; i++)
        {
            Frames.Advance(16);
            if (!forward.TryGetValue(animation.IterationTime, out var points)) continue;
            Assert.Equal(points, Points(Piece(block, 1).Data!));
            matched++;
        }

        Assert.True(matched > 2, $"only {matched} reversed frames landed on a forward frame's time");
    }

    [AvaloniaFact]
    public void JellyWobblesWhileTheOriginMovesAndSettlesBackToTheOutlines()
    {
        var block = Split(TextWarpKind.Jelly);
        var outlines = Outlines(block);
        Presto.Set(block, new AnimationParams { ["WarpOrigin"] = TweenValue.Of(new Point(0, 20)) });
        Presto.Set(block, new AnimationParams { ["Warp"] = 1, ["WarpRadius"] = 150 });
        Frames.Advance(16);
        Assert.Equal(outlines, Outlines(block));

        Presto.Animate(block, new AnimationParams { ["WarpOrigin"] = TweenValue.Of(new Point(150, 20)), Duration = 200, Ease = "linear" });
        for (var i = 0; i < 10; i++) Frames.Advance(16);
        Assert.NotSame(outlines[1], Piece(block, 1).Data);
        var during = Piece(block, 1).Data!.Bounds;
        Assert.NotEqual(outlines[1]!.Bounds, during);

        for (var i = 0; i < 300; i++) Frames.Advance(16);

        Assert.Equal(outlines, Outlines(block));
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheShorthandsHoldTheirIdentities()
    {
        var block = Split(TextWarpKind.Bulge);
        var outlines = Outlines(block);
        Presto.Defaults.ReducedMotion = true;

        Presto.Set(block, new AnimationParams { ["Warp"] = 1, ["WarpOrigin"] = TweenValue.Of(new Point(5, 5)) });
        Frames.Advance(16);

        Assert.Equal(outlines, Outlines(block));
        Assert.Equal(0d, PrestoAvalonia.Get(block, "Warp"));
        Assert.Equal(new Point(block.Bounds.Width / 2, block.Bounds.Height / 2), PrestoAvalonia.Get(block, "WarpOrigin"));
    }

    [AvaloniaFact]
    public void AMotionValueDrivesTheOriginThroughAnAnimatable()
    {
        var block = Split(TextWarpKind.Bulge);
        Presto.Set(block, new AnimationParams { ["Warp"] = 1, ["WarpRadius"] = 30 });
        var x = Presto.CreateMotionValue(10);
        var origin = block.CreateAnimatable(new() { ["WarpOrigin"] = 0 });
        x.Changed += value => origin.Set("WarpOrigin", [value.Value, 20]);

        x.Set(60);
        Frames.Advance(16);
        Frames.Advance(16);
        Frames.Advance(16);

        Assert.Equal(new Point(60, 20), PrestoAvalonia.Get(block, "WarpOrigin"));
        var piece = Piece(block, 1);
        var expected = WarpComposer.Warp(TextWarpKind.Bulge, TextSplitter.ContoursOf(piece)!.Contours[0].Points[0] + TextSplitter.Of(block)!.SlotOf(piece).Position,
            1, 0, new Point(60, 20), 30, block.Bounds.Size, 40);
        Assert.Contains(Points(piece.Data!), p => Point.Distance(p + TextSplitter.Of(block)!.SlotOf(piece).Position, expected) < 0.01);
    }

    [AvaloniaFact]
    public void TheShorthandsResolveOnlyOnATextBlock()
    {
        Assert.Equal("Warp", Presto.Canonicalize(new TextBlock(), "Warp"));
        Assert.Throws<ArgumentException>(() => Presto.Set(new Border(), new AnimationParams { ["Warp"] = 1 }));
    }
}
