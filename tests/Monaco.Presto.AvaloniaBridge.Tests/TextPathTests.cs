using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Text;
using Monaco.Presto.AvaloniaBridge.Transforms;
using Path = Avalonia.Controls.Shapes.Path;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary><see cref="TextPath"/>: where the characters land on the contour, how they turn, hide and wrap, and when they re-place.</summary>
public sealed class TextPathTests : BridgeTestBase
{
    private const double Tolerance = 0.5;

    private (TextBlock Block, Canvas Canvas) Laid(string data, Action<TextBlock>? configure = null)
    {
        var path = new Path { Data = Geometry.Parse(data), Stroke = Brushes.Black };
        var block = new TextBlock { Text = "Presto", FontSize = 16 };
        Canvas.SetLeft(block, 30);
        Canvas.SetTop(block, 20);
        var canvas = new Canvas { Width = 500, Height = 400, Children = { path, block } };
        new Window { Content = canvas, Width = 600, Height = 500 }.Show();
        Dispatcher.UIThread.RunJobs();
        configure?.Invoke(block);
        TextPath.SetPath(block, path);
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(16);
        return (block, canvas);
    }

    private static IReadOnlyList<Control> Chars(TextBlock block) => TextSplitter.Of(block)!.Chars;

    private static Point CenterIn(Control piece, Visual to) =>
        piece.TranslatePoint(new Point(piece.Bounds.Width / 2, piece.Bounds.Height / 2), to)!.Value;

    private static double Rotation(Control piece) => RenderTransformComposer.Get(piece, TransformShorthand.Rotate);

    [AvaloniaFact]
    public void CharactersSitOnAStraightPathAtTheirAdvances()
    {
        var (block, canvas) = Laid("M0,50 L400,50");

        var chars = Chars(block);
        var advance = 0.0;
        foreach (var piece in chars)
        {
            var center = CenterIn(piece, canvas);
            Assert.Equal(50, center.Y, Tolerance);
            Assert.Equal(advance + piece.Bounds.Width / 2, center.X, Tolerance);
            Assert.Equal(0, Rotation(piece), 3);
            advance += piece.Bounds.Width;
        }
    }

    [AvaloniaFact]
    public void RotationFollowsTheTangentOnAnArc()
    {
        var (block, canvas) = Laid("M0,150 A100,100 0 0 1 200,150");

        foreach (var piece in Chars(block))
        {
            var radius = CenterIn(piece, canvas) - new Point(100, 150);
            Assert.Equal(100, Math.Sqrt(radius.X * radius.X + radius.Y * radius.Y), 1);
            Assert.Equal(Math.Atan2(radius.X, -radius.Y) * 180 / Math.PI, Rotation(piece), 1.5);
        }
    }

    [AvaloniaFact]
    public void AlignAnchorsTheTextAtTheOffsetAndShiftLiftsItOffTheContour()
    {
        var (block, canvas) = Laid("M0,50 L400,50", b =>
        {
            TextPath.SetOffset(b, 0.5);
            TextPath.SetAlign(b, TextPathAlign.Center);
        });
        var chars = Chars(block);
        var total = chars.Sum(c => c.Bounds.Width);
        Assert.Equal(200 - total / 2, CenterIn(chars[0], canvas).X - chars[0].Bounds.Width / 2, Tolerance);

        TextPath.SetOffset(block, 1);
        TextPath.SetAlign(block, TextPathAlign.End);
        TextPath.SetShift(block, 10);
        Frames.Advance(16);

        Assert.Equal(400, CenterIn(chars[^1], canvas).X + chars[^1].Bounds.Width / 2, Tolerance);
        Assert.All(chars, c => Assert.Equal(40, CenterIn(c, canvas).Y, Tolerance));
    }

    [AvaloniaFact]
    public void CharactersPastTheEndOfAnOpenPathHide()
    {
        var (block, _) = Laid("M0,50 L20,50");

        var chars = Chars(block);
        Assert.Equal(1, chars[0].Opacity);
        Assert.Equal(0, chars[^1].Opacity);

        TextPath.SetOffset(block, -3);
        Frames.Advance(16);
        Assert.All(chars, c => Assert.Equal(0, c.Opacity));
    }

    [AvaloniaFact]
    public void AClosedContourWraps()
    {
        var (block, canvas) = Laid("M0,0 L100,0 L100,100 L0,100 Z", b => TextPath.SetOffset(b, 0.98));

        var chars = Chars(block);
        Assert.All(chars, c => Assert.Equal(1, c.Opacity));
        var last = CenterIn(chars[^1], canvas);
        Assert.Equal(0, last.Y, Tolerance);
        Assert.True(last.X > 0, $"{last} has not wrapped onto the top edge");
    }

    [AvaloniaFact]
    public void ADataChangeRePlacesOnceOnTheNextFrame()
    {
        var (block, canvas) = Laid("M0,50 L400,50");
        var path = (Path)canvas.Children[0];
        var piece = Chars(block)[0];
        var translate = ((TransformGroup)piece.RenderTransform!).Children.OfType<TranslateTransform>().Single();
        var placements = 0;
        translate.PropertyChanged += (_, e) =>
        {
            if (e.Property == TranslateTransform.YProperty) placements++;
        };

        path.Data = Geometry.Parse("M0,60 L400,60");
        path.Data = Geometry.Parse("M0,70 L400,70");
        path.Data = Geometry.Parse("M0,80 L400,80");
        Assert.Equal(50, CenterIn(piece, canvas).Y, Tolerance);
        Frames.Advance(16);

        Assert.Equal(80, CenterIn(piece, canvas).Y, Tolerance);
        Assert.Equal(1, placements);
    }

    [AvaloniaFact]
    public void AYAnimationLayersOverThePlacementAndRevertsToIt()
    {
        var (block, _) = Laid("M0,50 L400,50");
        var piece = Chars(block)[0];
        var placed = RenderTransformComposer.Get(piece, TransformShorthand.Y);

        var animation = Seeked(piece, new() { ["Y"] = placed - 30 });
        Assert.Equal(placed - 30, RenderTransformComposer.Get(piece, TransformShorthand.Y), 3);

        animation.Revert();
        Assert.Equal(placed, RenderTransformComposer.Get(piece, TransformShorthand.Y), 3);
    }

    [AvaloniaFact]
    public void ClearingThePathRevertsTheText()
    {
        var (block, _) = Laid("M0,50 L400,50");

        TextPath.SetPath(block, null);

        Assert.Equal("Presto", block.Text);
        Assert.Null(TextSplitter.Of(block));
        Assert.True(block.Inlines is null or { Count: 0 });
    }
}
