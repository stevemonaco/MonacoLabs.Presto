using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Monaco.Presto.AvaloniaBridge.Controls;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>A <see cref="FilteredImage"/> laid out as <see cref="Image"/> is, drawn plain at rest and through its color matrix otherwise.</summary>
public sealed class FilteredImageTests : CompositorTestBase
{
    public static TheoryData<Stretch, StretchDirection> Stretches()
    {
        var data = new TheoryData<Stretch, StretchDirection>();
        foreach (var stretch in Enum.GetValues<Stretch>())
            foreach (var direction in Enum.GetValues<StretchDirection>())
                data.Add(stretch, direction);
        return data;
    }

    [AvaloniaTheory]
    [MemberData(nameof(Stretches))]
    public void MeasureAndArrangeMatchImage(Stretch stretch, StretchDirection direction)
    {
        var source = Picture();
        var filtered = new FilteredImage { Source = source, Stretch = stretch, StretchDirection = direction };
        var image = new Image { Source = source, Stretch = stretch, StretchDirection = direction };

        foreach (var available in (Size[])[Size.Infinity, new(50, 30), new(8, 8)])
        {
            filtered.Measure(available);
            image.Measure(available);
            Assert.Equal(image.DesiredSize, filtered.DesiredSize);

            var slot = new Rect(available.Width is double.PositiveInfinity ? new Size(70, 45) : available);
            filtered.Arrange(slot);
            image.Arrange(slot);
            Assert.Equal(image.Bounds, filtered.Bounds);
        }
    }

    [AvaloniaFact]
    public void DefaultFiltersDrawExactlyAsImage()
    {
        var source = Picture();
        var image = Place(new Image { Source = source, Stretch = Stretch.Uniform, Width = 60, Height = 50 });
        var expected = Capture(image);

        var filtered = Place(new FilteredImage { Source = source, Stretch = Stretch.Uniform, Width = 60, Height = 50 });

        Assert.Equal(expected, Capture(filtered));
    }

    [AvaloniaFact]
    public void GrayscaleRendersTheSpecLuminance() =>
        AssertNear(Color.FromRgb(54, 54, 54), Filtered(Colors.Red, image => image.Grayscale = 1));

    [AvaloniaFact]
    public void InvertTurnsRedCyan() => AssertNear(Colors.Cyan, Filtered(Colors.Red, image => image.Invert = 1));

    // hue-rotate(180deg) on red: R clamps from 0.213 - 0.787, G and B are 0.213 + 0.213.
    [AvaloniaFact]
    public void HueRotateMapsRedToItsSpecValue() =>
        AssertNear(Color.FromRgb(0, 109, 109), Filtered(Colors.Red, image => image.HueRotate = 180));

    [AvaloniaFact]
    public void TintMapsLuminanceOntoTheColor() =>
        AssertNear(Color.FromRgb(0, 0, 54), Filtered(Colors.Red, image => (image.Tint, image.TintColor) = (1, Colors.Blue)));

    [AvaloniaFact]
    public void AncestorOpacityClipAndScaleStillApplyToTheFilteredLayer()
    {
        var image = new FilteredImage
        {
            Source = Solid(Colors.Red), Invert = 1, Stretch = Stretch.None, RenderTransform = new ScaleTransform(2, 2),
            RenderTransformOrigin = RelativePoint.TopLeft, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        var host = Place(new Border { Child = image, Opacity = 0.5, Width = 15, Height = 30, ClipToBounds = true });

        AssertNear(Color.FromRgb(0, 128, 128), Row(host, 5)[5]);
        AssertNear(Color.FromRgb(0, 128, 128), Row(host, 18)[12]);
        Assert.Equal(Colors.Black, Row(host, 18)[17]);
        Assert.Equal(Colors.Black, Row(host, 22)[5]);
    }

    [AvaloniaFact]
    public void AnAnimatedFilterRendersBetweenFramesAndRevertsToTheLocalValue()
    {
        var image = Place(new FilteredImage { Source = Solid(Colors.Red), Grayscale = 0.25 });
        var animation = image.Animate(b => b.Property(FilteredImage.GrayscaleProperty, from: 1.0, to: 0.0).Duration(100).Ease(Eases.None));

        Frames.Advance(50, 10);
        Tick();
        Assert.InRange(Row(image, 5)[5].R, 120, 190);

        Frames.Advance(100, 10);
        Tick();
        Assert.False(image.IsFiltering);
        Assert.Equal(Colors.Red, Row(image, 5)[5]);

        animation.Revert();
        Assert.Equal(0.25, image.Grayscale);
    }

    [AvaloniaFact]
    public void AHoverOnSaturateReturnsToTheBaseValue()
    {
        var image = new FilteredImage
        {
            Source = Solid(Colors.Red), Width = 50, Height = 50, Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        var window = Show(image);
        Motion.SetHover(image, Presto.Build().Property(FilteredImage.SaturateProperty, 0.0).Duration(100).Ease(Eases.None));

        window.MouseMove(new Point(25, 25));
        Frames.Advance(150, 10);
        Assert.Equal(0, image.Saturate, 6);

        window.MouseMove(new Point(150, 150));
        Frames.Advance(150, 10);
        Assert.Equal(1, image.Saturate, 6);
        Assert.False(image.IsFiltering);
    }

    [AvaloniaFact]
    public void ADrawingIsRasterizedOnceWhileFilteringAndAgainAfterAResize()
    {
        var drawing = new DrawingImage(new GeometryDrawing { Brush = Brushes.Red, Geometry = new RectangleGeometry(new Rect(0, 0, 20, 20)) });
        var image = Place(new FilteredImage { Source = drawing, Stretch = Stretch.Fill, Width = 40, Height = 40 });
        Assert.Equal(0, image.Rasterizations);

        image.Grayscale = 1;
        Tick();
        AssertNear(Color.FromRgb(54, 54, 54), Row(image, 30)[30]);
        image.Grayscale = 0.9;
        Tick();
        Assert.Equal(1, image.Rasterizations);

        image.Width = 60;
        Tick();
        Assert.Equal(2, image.Rasterizations);
        AssertNear(Color.FromRgb(74, 49, 49), Row(image, 30)[50]);
    }

    [AvaloniaFact]
    public void ADrawingIsRasterizedAgainWhenTheDrawingChanges()
    {
        var geometry = new RectangleGeometry(new Rect(0, 0, 20, 20));
        var drawing = new DrawingImage(new GeometryDrawing { Brush = Brushes.Red, Geometry = geometry });
        var image = Place(new FilteredImage { Source = drawing, Stretch = Stretch.Fill, Width = 40, Height = 40, Invert = 1 });
        Assert.Equal(1, image.Rasterizations);
        AssertNear(Colors.Cyan, Row(image, 20)[20]);

        drawing.Drawing = new GeometryDrawing { Brush = Brushes.Blue, Geometry = geometry };
        Tick();

        Assert.Equal(2, image.Rasterizations);
        AssertNear(Colors.Yellow, Row(image, 20)[20]);
    }

    [AvaloniaFact]
    public void ACroppedBitmapIsFilteredFromItsOwnRegionOnly()
    {
        var cropped = new CroppedBitmap(Picture(), new PixelRect(10, 0, 10, 5));
        var image = Place(new FilteredImage { Source = cropped, Stretch = Stretch.Fill, Width = 40, Height = 20, Invert = 1 });

        for (var y = 0; y < 20; y++)
        {
            var row = Row(image, y);
            for (var x = 0; x < 40; x++) AssertNear(Colors.Magenta, row[x]);
        }
    }

    [AvaloniaFact]
    public void AnimatingAFilterOnTheCompositorIsNotSupported() =>
        Assert.Throws<NotSupportedException>(() => new FilteredImage().AnimateOnCompositor(b => b.Property(FilteredImage.GrayscaleProperty, 1.0)));

    [AvaloniaFact]
    public void IdentityEffectsDrawExactlyAsImageWhateverTheirNoise()
    {
        var source = Picture();
        var expected = Capture(Place(new Image { Source = source, Stretch = Stretch.Uniform, Width = 60, Height = 50 }));

        var filtered = Place(new FilteredImage
        {
            Source = source, Stretch = Stretch.Uniform, Width = 60, Height = 50,
            DissolveScale = 8, DissolveSoftness = 0.4, Pixelate = 1, Ripple = 0, RippleScale = 10, Seed = 7,
        });

        Assert.False(filtered.IsFiltering);
        Assert.Equal(expected, Capture(filtered));
    }

    [AvaloniaFact]
    public void AFullDissolveIsTransparent() =>
        Assert.All(Pixels(Dissolved(image => image.Dissolve = 1)), pixel => Assert.Equal(Colors.Black, pixel));

    [AvaloniaFact]
    public void AHalfDissolveKeepsSomePixelsAndClearsOthers()
    {
        var pixels = Pixels(Dissolved(image => image.Dissolve = 0.5));

        Assert.Contains(Colors.Red, pixels);
        Assert.Contains(Colors.Black, pixels);
    }

    [AvaloniaFact]
    public void TheSameSeedDissolvesTheSamePixels()
    {
        var first = Pixels(Dissolved(image => (image.Dissolve, image.Seed) = (0.5, 1)));

        Assert.Equal(first, Pixels(Dissolved(image => (image.Dissolve, image.Seed) = (0.5, 1))));
        Assert.NotEqual(first, Pixels(Dissolved(image => (image.Dissolve, image.Seed) = (0.5, 2))));
    }

    [AvaloniaFact]
    public void ColorFiltersApplyToThePixelsADissolveKeeps()
    {
        var pixels = Pixels(Dissolved(image => (image.Dissolve, image.Grayscale) = (0.5, 1)));

        Assert.Contains(Colors.Black, pixels);
        Assert.Contains(pixels, pixel => pixel.R is >= 52 and <= 56);
        Assert.All(pixels, pixel => Assert.True(Math.Abs(pixel.R - pixel.G) <= 2 && Math.Abs(pixel.R - pixel.B) <= 2, pixel.ToString()));
    }

    [AvaloniaFact]
    public void AnAnimatedDissolveBackToZeroDrawsPlainAgain()
    {
        var image = Dissolved(_ => { });
        Assert.False(image.IsFiltering);
        image.Animate(b => b.Dissolve(from: 1, to: 0).Duration(100).Ease(Eases.None));

        Frames.Advance(50, 10);
        Tick();
        Assert.True(image.IsFiltering);

        Frames.Advance(100, 10);
        Tick();
        Assert.False(image.IsFiltering);
        Assert.All(Pixels(image), pixel => Assert.Equal(Colors.Red, pixel));
    }

    [AvaloniaFact]
    public void ASpriteSheetFrameDissolves()
    {
        var sheet = Place(new SpriteSheet { Source = Solid(Colors.Red), Columns = 2, Rows = 2, Frame = 3, Stretch = Stretch.Fill, Width = 60, Height = 60, Dissolve = 0.5 });
        var pixels = Pixels(sheet);

        Assert.Contains(Colors.Red, pixels);
        Assert.Contains(Colors.Black, pixels);
    }

    [AvaloniaFact]
    public void PixelateDrawsUniformBlocksFromTheTopLeft()
    {
        var image = Place(new FilteredImage { Source = Gradient(), Stretch = Stretch.Fill, Width = 60, Height = 60, Pixelate = 8 });
        var pixels = Pixels(image);

        for (var y = 0; y < 60; y++)
            for (var x = 0; x < 60; x++)
                Assert.Equal(pixels[y / 8 * 8 * 60 + x / 8 * 8], pixels[y * 60 + x]);
        Assert.NotEqual(pixels[0], pixels[8]);
        Assert.NotEqual(pixels[0], pixels[8 * 60]);
        Assert.DoesNotContain(Colors.Black, pixels);
    }

    [AvaloniaTheory]
    [InlineData(16)]
    [InlineData(7)]
    public void PixelateDrawsPartialEdgeBlocksOpaque(double size)
    {
        var image = Place(new FilteredImage { Source = Solid(Colors.Red), Stretch = Stretch.Fill, Width = 60, Height = 60, Pixelate = size });

        Assert.All(Pixels(image), pixel => AssertNear(Colors.Red, pixel));
    }

    [AvaloniaFact]
    public void PixelateSamplesPartialEdgeBlocksFromInsideTheImage()
    {
        var plain = Pixels(Place(new FilteredImage { Source = Gradient(), Stretch = Stretch.Fill, Width = 60, Height = 60 }));
        var pixels = Pixels(Place(new FilteredImage { Source = Gradient(), Stretch = Stretch.Fill, Width = 60, Height = 60, Pixelate = 16 }));

        Assert.Contains(pixels[59 * 60 + 59], plain.GetRange(48 * 60, 12 * 60));
        Assert.Contains(pixels[59 * 60], plain);
        Assert.Contains(pixels[59], plain);
    }

    [AvaloniaFact]
    public void RippleKeepsTheEdgesOpaque()
    {
        var image = Place(new FilteredImage { Source = Solid(Colors.Red), Stretch = Stretch.Fill, Width = 60, Height = 60, Ripple = 8, RippleScale = 16 });

        Assert.All(Pixels(image), pixel => AssertNear(Colors.Red, pixel));
    }

    [AvaloniaFact]
    public void RippleMovesPixelsTheSameWayForTheSameSeedWithinTheDestination()
    {
        var plain = Pixels(Place(new FilteredImage { Source = Gradient(), Stretch = Stretch.Fill, Width = 60, Height = 60 }));
        var rippled = Rippled(1);
        var first = Pixels(rippled);

        Assert.True(first.Where((pixel, i) => pixel != plain[i]).Count() > 600);
        Assert.Equal(first, Pixels(Rippled(1)));
        Assert.NotEqual(first, Pixels(Rippled(2)));
        Assert.All(Row(rippled, 30)[60..70], pixel => Assert.Equal(Colors.Black, pixel));
        Assert.All(Row(rippled, 62)[..60], pixel => Assert.Equal(Colors.Black, pixel));
    }

    [AvaloniaFact]
    public void TheDissolveMaskIsNotPixelated()
    {
        var pixels = Pixels(Dissolved(image => (image.Pixelate, image.Dissolve, image.DissolveScale) = (12, 0.5, 4)));

        Assert.Contains(Enumerable.Range(0, 12 * 12), i => pixels[i / 12 * 60 + i % 12] != pixels[0]);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASubclassDrawsCellsThroughTheFilters(bool drawing)
    {
        var image = Place(new Cells
        {
            Source = drawing ? HalvesDrawing() : Picture(), Stretch = Stretch.Fill, Width = 40, Height = 20, Grayscale = 1,
        });

        var left = (byte)(drawing ? 18 : 54);
        AssertNear(Color.FromRgb(left, left, left), Row(image, 5)[10]);
        AssertNear(Color.FromRgb(182, 182, 182), Row(image, 5)[30]);
        var rasterizations = image.Rasterizations;
        Assert.Equal(drawing ? 2 : 0, rasterizations);

        image.InvalidateVisual();
        Tick();
        image.InvalidateVisual();
        Tick();

        Assert.Equal(rasterizations, image.Rasterizations);
    }

    private static FilteredImage Rippled(int seed) =>
        Place(new FilteredImage { Source = Gradient(), Stretch = Stretch.Fill, Width = 60, Height = 60, Ripple = 8, RippleScale = 16, Seed = seed });

    private static FilteredImage Dissolved(Action<FilteredImage> effect)
    {
        var image = new FilteredImage { Source = Solid(Colors.Red), Stretch = Stretch.Fill, Width = 60, Height = 60, DissolveScale = 8 };
        effect(image);
        return Place(image);
    }

    private static List<Color> Pixels(Control control)
    {
        var pixels = new List<Color>();
        for (var y = 0; y < control.Bounds.Height; y++) pixels.AddRange(Row(control, y)[..(int)control.Bounds.Width]);
        return pixels;
    }

    private static Color Filtered(Color color, Action<FilteredImage> filter)
    {
        var image = new FilteredImage { Source = Solid(color) };
        filter(image);
        return Row(Place(image), 5)[5];
    }

    private static RenderTargetBitmap Solid(Color color)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(10, 10));
        using (var context = bitmap.CreateDrawingContext()) context.FillRectangle(new SolidColorBrush(color), new Rect(0, 0, 10, 10));
        return bitmap;
    }

    private static T Place<T>(T control) where T : Control
    {
        control.HorizontalAlignment = HorizontalAlignment.Left;
        control.VerticalAlignment = VerticalAlignment.Top;
        new Window { Content = control, Background = Brushes.Black, Width = 100, Height = 100 }.Show();
        Tick();
        return control;
    }

    // Red grows to the right and blue downwards, so no two pixels match.
    private static RenderTargetBitmap Gradient()
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(60, 60));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(new LinearGradientBrush
            {
                StartPoint = RelativePoint.TopLeft, EndPoint = RelativePoint.Parse("100%,0%"),
                GradientStops = { new GradientStop(Color.FromRgb(20, 0, 0), 0), new GradientStop(Colors.Red, 1) },
            }, new Rect(0, 0, 60, 60));
            context.FillRectangle(new LinearGradientBrush
            {
                StartPoint = RelativePoint.TopLeft, EndPoint = RelativePoint.Parse("0%,100%"),
                GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 255), 0), new GradientStop(Color.FromArgb(128, 0, 0, 255), 1) },
            }, new Rect(0, 0, 60, 60));
        }
        return bitmap;
    }

    private static DrawingImage HalvesDrawing() => new(new DrawingGroup
    {
        Children =
        {
            new GeometryDrawing { Brush = Brushes.Blue, Geometry = new RectangleGeometry(new Rect(0, 0, 10, 10)) },
            new GeometryDrawing { Brush = Brushes.Lime, Geometry = new RectangleGeometry(new Rect(10, 0, 10, 10)) },
        },
    });

    // Draws the left half of the source over the left half of the control and the right half over the right.
    private sealed class Cells : PrestoImageBase
    {
        public override void Render(DrawingContext context)
        {
            if (Source is not { } source) return;
            var half = Bounds.Width / 2;
            for (var i = 0; i < 2; i++)
            {
                using (context.PushTransform(Matrix.CreateScale(0.5, 1) * Matrix.CreateTranslation(half * i, 0)))
                    DrawImage(context, source, new Rect(source.Size.Width / 2 * i, 0, source.Size.Width / 2, source.Size.Height));
            }
        }
    }

    // Four colored quadrants, so any misplaced or resampled pixel changes the frame.
    private static RenderTargetBitmap Picture()
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(20, 10));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.Red, new Rect(0, 0, 10, 5));
            context.FillRectangle(Brushes.Lime, new Rect(10, 0, 10, 5));
            context.FillRectangle(Brushes.Blue, new Rect(0, 5, 10, 5));
            context.FillRectangle(Brushes.Yellow, new Rect(10, 5, 10, 5));
        }
        return bitmap;
    }
}
