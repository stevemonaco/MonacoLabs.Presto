using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MonacoLabs.Presto.Controls;

namespace MonacoLabs.Presto.Tests;

/// <summary>An <see cref="ImageSequence"/> drawing the image its <see cref="ImageSequence.Frame"/> selects.</summary>
public sealed class ImageSequenceTests : CompositorTestBase
{
    private static readonly Color[] Fills = [Colors.Red, Colors.Lime, Colors.Blue, Colors.Yellow, Colors.Cyan];

    [AvaloniaFact]
    public void EachFrameDrawsItsImage()
    {
        var sequence = ShowSequence(Solid());

        for (var i = 0; i < Fills.Length; i++)
        {
            sequence.Frame = i;
            Tick();
            Assert.Equal(Fills[i], Row(sequence, 5)[5]);
        }
    }

    [AvaloniaFact]
    public void CurrentFrameTracksFrameAndFrames()
    {
        var sequence = new ImageSequence { Frame = 3.7 };
        Assert.Equal(0, sequence.CurrentFrame);

        sequence.Frames = Solid();
        Assert.Equal(3, sequence.CurrentFrame);

        sequence.Frame = 1.2;
        Assert.Equal(1, sequence.CurrentFrame);
    }

    [AvaloniaFact]
    public void MeasureReturnsTheFrameSizeWithStretchApplied()
    {
        var sequence = new ImageSequence { Frames = Solid(), Stretch = Stretch.None };
        sequence.Measure(Size.Infinity);
        Assert.Equal(new Size(10, 10), sequence.DesiredSize);

        sequence.Stretch = Stretch.Uniform;
        sequence.Measure(new Size(50, 30));
        Assert.Equal(new Size(30, 30), sequence.DesiredSize);

        var shown = ShowSequence(Solid(), Stretch.Fill, 40);
        Assert.Equal(new Rect(0, 0, 40, 40), Extent(shown, Fills[0]));
    }

    [AvaloniaFact]
    public void NoFramesRendersNothing()
    {
        var sequence = ShowSequence(null);
        Assert.Equal(default, sequence.DesiredSize);

        sequence.Frames = [];
        sequence.Frame = 3;
        Tick();

        Assert.Equal(0, sequence.CurrentFrame);
        Assert.Equal(Colors.Black, Row(sequence, 5)[5]);
    }

    private static ImageSequence ShowSequence(IReadOnlyList<IImage>? frames, Stretch stretch = Stretch.None, double size = double.NaN)
    {
        var sequence = new ImageSequence
        {
            Frames = frames, Stretch = stretch, Width = size, Height = size,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        new Window { Content = sequence, Background = Brushes.Black, Width = 100, Height = 100 }.Show();
        Tick();
        return sequence;
    }

    private static IImage[] Solid() => Array.ConvertAll(Fills, color =>
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(10, 10));
        using (var context = bitmap.CreateDrawingContext())
            context.FillRectangle(new SolidColorBrush(color), new Rect(0, 0, 10, 10));
        return (IImage)bitmap;
    });
}
