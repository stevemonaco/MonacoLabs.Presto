using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MonacoLabs.Presto.Controls;

namespace MonacoLabs.Presto.Tests;

/// <summary>A <see cref="SpriteSheet"/> drawing the grid cell its <see cref="SpriteSheet.Frame"/> selects.</summary>
public sealed class SpriteSheetTests : CompositorTestBase
{
    private static readonly Color[] Cells =
    [
        Colors.Red, Colors.Lime, Colors.Blue, Colors.Yellow,
        Colors.Cyan, Colors.Magenta, Colors.Orange, Colors.White,
    ];

    [AvaloniaFact]
    public void EachFrameDrawsItsCell()
    {
        var sheet = ShowSheet(Sheet());

        for (var i = 0; i < Cells.Length; i++)
        {
            sheet.Frame = i;
            Tick();
            Assert.Equal(Cells[i], Row(sheet, 5)[5]);
        }
    }

    [AvaloniaFact]
    public void APartialLastRowHonorsFrameCount()
    {
        var sheet = ShowSheet(Sheet());
        sheet.FrameCount = 7;

        sheet.Frame = 7.5;
        Tick();

        Assert.Equal(6, sheet.CurrentFrame);
        Assert.Equal(Cells[6], Row(sheet, 5)[5]);
    }

    [AvaloniaFact]
    public void FrameSizeDrawsFromTheTopLeftOfEachCellWithoutItsMargin()
    {
        var sheet = ShowSheet(Sheet(20, frame: 16));
        sheet.FrameSize = new Size(16, 16);

        sheet.Frame = 5;
        Tick();

        Assert.Equal(new Rect(0, 0, 16, 16), Extent(sheet, Cells[5]));
        Assert.Null(Extent(sheet, Colors.Gray));
    }

    [AvaloniaFact]
    public void ALinearTweenGivesEveryFrameEqualTimeAndEndsOnTheLast()
    {
        var sheet = new SpriteSheet { Columns = 4, Rows = 2 };
        var animation = Presto.Animate(sheet, new() { ["Frame"] = TweenValue.FromTo(0, 8), Duration = 800, Ease = "linear", Autoplay = false });
        var ticks = new int[8];

        // Seeking samples on a fixed 10 ms grid; ticking would start mid-frame, wherever the engine's first frame lands.
        for (var time = 0; time <= 800; time += 10)
        {
            animation.Seek(time);
            ticks[sheet.CurrentFrame]++;
        }

        Assert.All(ticks, count => Assert.InRange(count, 9, 11));
        Assert.Equal(7, sheet.CurrentFrame);
    }

    [AvaloniaTheory]
    [InlineData(8, false, 7)]
    [InlineData(8, true, 0)]
    [InlineData(9.5, true, 1)]
    [InlineData(-1, false, 0)]
    [InlineData(-1, true, 7)]
    public void WrapWrapsInsteadOfHoldingTheLastFrame(double frame, bool wrap, int shown)
    {
        var sheet = new SpriteSheet { Columns = 4, Rows = 2, Wrap = wrap, Frame = frame };

        Assert.Equal(shown, sheet.CurrentFrame);
    }

    [AvaloniaFact]
    public void PlayGivesEachFrameAThousandMillisecondsOverFps()
    {
        var sheet = new SpriteSheet { Columns = 4, Rows = 2, FrameCount = 6 };

        var animation = sheet.Play(fps: 12, loop: false);

        Assert.Equal(500, animation.Duration, 3);
        Assert.Equal(500, sheet.Play(fps: 12).IterationDuration, 3);
    }

    [AvaloniaFact]
    public void PlayReversedAndSoughtShowsTheExpectedFrames()
    {
        var sheet = new SpriteSheet { Columns = 4, Rows = 2 };

        var reversed = sheet.Play(fps: 10, loop: false, reverse: true);
        Frames.Advance(0);
        Frames.Advance(150);
        Assert.Equal(6, sheet.CurrentFrame);

        reversed.Cancel();
        var forward = sheet.Play(fps: 10, loop: false);
        forward.Seek(350);
        Assert.Equal(3, sheet.CurrentFrame);
    }

    [AvaloniaFact]
    public void RevertRestoresFrame()
    {
        var sheet = new SpriteSheet { Columns = 4, Rows = 2, Frame = 2 };
        var animation = sheet.Play(fps: 10);
        Frames.Advance(0);
        Frames.Advance(450);
        Assert.NotEqual(2, sheet.CurrentFrame);

        animation.Revert();

        Assert.Equal(2, sheet.Frame);
        Assert.Equal(2, sheet.CurrentFrame);
    }

    [AvaloniaFact]
    public void MeasureAndStretchApplyToOneFrameNotTheSheet()
    {
        var sheet = new SpriteSheet { Source = Sheet(), Columns = 4, Rows = 2, Stretch = Stretch.None };
        sheet.Measure(Size.Infinity);
        Assert.Equal(new Size(10, 10), sheet.DesiredSize);

        sheet.Stretch = Stretch.Uniform;
        sheet.Measure(new Size(50, 30));
        Assert.Equal(new Size(30, 30), sheet.DesiredSize);

        var shown = ShowSheet(Sheet(), Stretch.Fill, 40);
        Assert.Equal(new Rect(0, 0, 40, 40), Extent(shown, Cells[0]));
    }

    [AvaloniaFact]
    public void DefaultInterpolationKeepsNeighboringCellsOutOfTheFrameEdges()
    {
        var sheet = ShowSheet(Sheet(), Stretch.Fill, 40, BitmapInterpolationMode.Unspecified);

        sheet.Frame = 5;
        Tick();

        for (var y = 0; y < 40; y++)
            Assert.All(Row(sheet, y)[..40], pixel => Assert.Equal(Cells[5], pixel));
    }

    [AvaloniaFact]
    public void AFilteredFrameIsFilteredWithoutNeighboringCells()
    {
        var sheet = ShowSheet(Sheet(), Stretch.Fill, 40, BitmapInterpolationMode.Unspecified);

        sheet.Frame = 5;
        sheet.Invert = 1;
        Tick();

        for (var y = 0; y < 40; y++)
            Assert.All(Row(sheet, y)[..40], pixel => Assert.Equal(Colors.Lime, pixel));
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(-2)]
    public void ColumnsOrRowsOfZeroOrLessAreRejected(int count)
    {
        var sheet = new SpriteSheet();

        Assert.Throws<ArgumentException>(() => sheet.Columns = count);
        Assert.Throws<ArgumentException>(() => sheet.Rows = count);
        Assert.Equal((1, 1), (sheet.Columns, sheet.Rows));
    }

    [AvaloniaFact]
    public void WithoutColumnsAndRowsTheWholeSourceIsOneFrame()
    {
        var sheet = new SpriteSheet { Source = Sheet(), Stretch = Stretch.None };
        sheet.Measure(Size.Infinity);

        Assert.Equal(1, sheet.EffectiveFrameCount);
        Assert.Equal(new Size(40, 20), sheet.DesiredSize);
        sheet.Frame = 3;
        Assert.Equal(0, sheet.CurrentFrame);
    }

    [AvaloniaFact]
    public void AnUnsetFrameShowsTheFirstFrameAndAnUnsetWrapClamps()
    {
        var sheet = ShowSheet(Sheet());

        Assert.Equal(0, sheet.CurrentFrame);
        Assert.Equal(Cells[0], Row(sheet, 5)[5]);

        sheet.Frame = 12;
        Assert.Equal(7, sheet.CurrentFrame);
        sheet.Frame = -3;
        Assert.Equal(0, sheet.CurrentFrame);
    }

    [AvaloniaFact]
    public void PlayWithoutArgumentsLoopsForwardsAtTwentyFourFramesASecond()
    {
        var sheet = new SpriteSheet { Columns = 4, Rows = 2 };

        var animation = sheet.Play();
        Frames.Advance(0);
        Frames.Advance(100);

        Assert.Equal(8 * 1000 / 24d, animation.IterationDuration, 6);
        Assert.True(double.IsPositiveInfinity(animation.IterationCount));
        Assert.False(animation.Reversed);
        Assert.Equal(2, sheet.CurrentFrame);
    }

    private static SpriteSheet ShowSheet(IImage source, Stretch stretch = Stretch.None, double size = double.NaN,
        BitmapInterpolationMode interpolation = BitmapInterpolationMode.None)
    {
        var sheet = new SpriteSheet
        {
            Source = source, Columns = 4, Rows = 2, Stretch = stretch, Width = size, Height = size,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        RenderOptions.SetBitmapInterpolationMode(sheet, interpolation);
        new Window { Content = sheet, Background = Brushes.Black, Width = 100, Height = 100 }.Show();
        Tick();
        return sheet;
    }

    /// <summary>A 4x2 grid of <paramref name="cell"/>-sized cells, each holding a square of its <see cref="Cells"/> color <paramref name="frame"/> wide at its top left, gray elsewhere.</summary>
    private static RenderTargetBitmap Sheet(int cell = 10, int? frame = null)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(cell * 4, cell * 2));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.Gray, new Rect(0, 0, cell * 4, cell * 2));
            for (var i = 0; i < Cells.Length; i++)
                context.FillRectangle(new SolidColorBrush(Cells[i]), new Rect(i % 4 * cell, i / 4 * cell, frame ?? cell, frame ?? cell));
        }
        return bitmap;
    }
}
