using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary><see cref="FilteredImage.TransitionTo(IImage?, ImageTransition)"/> composites the outgoing and incoming images by its progress.</summary>
public sealed class ImageTransitionTests : CompositorTestBase
{
    public static TheoryData<ImageTransitionKind> Kinds() => new(Enum.GetValues<ImageTransitionKind>());

    [AvaloniaTheory]
    [MemberData(nameof(Kinds))]
    public void ProgressZeroDrawsTheOldImageAndOneTheNew(ImageTransitionKind kind)
    {
        var (image, animation) = RedToBlue(kind);

        Seek(animation, 0);
        Assert.Equal(0, image.TransitionProgress);
        Assert.All(Pixels(image), pixel => AssertNear(Colors.Red, pixel));

        Seek(animation, 100);
        Assert.All(Pixels(image), pixel => AssertNear(Colors.Blue, pixel));
    }

    [AvaloniaFact]
    public void AHalfCrossfadeOfAnOpaquePairDoesNotDarken()
    {
        var (image, animation) = RedToBlue(ImageTransitionKind.Crossfade);

        Seek(animation, 50);

        Assert.All(Pixels(image), pixel => AssertNear(Color.FromRgb(128, 0, 128), pixel));
    }

    [AvaloniaTheory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(75)]
    public void AnOpaqueRipplePairNeverDarkens(double time)
    {
        var (image, animation) = RedToBlue(ImageTransitionKind.Ripple);

        Seek(animation, time);

        Assert.All(Pixels(image), pixel => Assert.True(pixel.R + pixel.B >= 250, $"{pixel} darkened"));
    }

    public static TheoryData<ImageTransitionKind, Stretch> KindsAndStretches()
    {
        var data = new TheoryData<ImageTransitionKind, Stretch>();
        foreach (var kind in Enum.GetValues<ImageTransitionKind>())
        foreach (var stretch in (Stretch[])[Stretch.None, Stretch.Fill, Stretch.Uniform, Stretch.UniformToFill])
            data.Add(kind, stretch);
        return data;
    }

    [AvaloniaTheory]
    [MemberData(nameof(KindsAndStretches))]
    public void TheOldImageNeverShowsBesideANewOneOfAnotherAspect(ImageTransitionKind kind, Stretch stretch)
    {
        var image = Place(new FilteredImage { Source = Solid(Colors.Red, 20, 10), Stretch = stretch, Width = 100, Height = 100 });
        image.HorizontalAlignment = HorizontalAlignment.Center;
        var animation = image.TransitionTo(Solid(Colors.Blue, 10, 20), kind, Linear);

        foreach (var time in (double[])[0, 25, 50, 90])
        {
            Seek(animation, time);
            if (stretch == Stretch.Uniform) Assert.Equal(new Rect(25, 0, 50, 100), image.Bounds);
            Assert.Equal(0, ShownOutside(image));
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Kinds))]
    public void AnInterruptedTransitionNeverShowsBesideANewImageOfAnotherAspect(ImageTransitionKind kind)
    {
        var image = Place(new FilteredImage { Source = Solid(Colors.Red, 20, 10), Width = 100, Height = 100 });
        image.HorizontalAlignment = HorizontalAlignment.Center;
        Seek(image.TransitionTo(Solid(Colors.Lime, 20, 10), ImageTransitionKind.Crossfade, Linear), 50);
        var animation = image.TransitionTo(Solid(Colors.Blue, 10, 20), kind, Linear);

        foreach (var time in (double[])[0, 25, 50, 90])
        {
            Seek(animation, time);
            Assert.Equal(new Rect(25, 0, 50, 100), image.Bounds);
            Assert.Equal(0, ShownOutside(image));
        }
    }

    [AvaloniaFact]
    public void AHalfDissolveShowsRedAndBluePixels()
    {
        var (image, animation) = RedToBlue(ImageTransitionKind.Dissolve);

        Seek(animation, 50);
        var pixels = Pixels(image);

        Assert.Contains(Colors.Red, pixels);
        Assert.Contains(Colors.Blue, pixels);
    }

    [AvaloniaTheory]
    [InlineData(ImageTransitionKind.Ripple)]
    [InlineData(ImageTransitionKind.Wipe)]
    public void BothImagesContributeMidway(ImageTransitionKind kind)
    {
        var (image, animation) = RedToBlue(kind);

        Seek(animation, 50);
        var pixels = Pixels(image);

        Assert.Contains(pixels, pixel => pixel.R > 30);
        Assert.Contains(pixels, pixel => pixel.B > 30);
    }

    [AvaloniaFact]
    public void PixelateBlocksTheOldImageThenTheNewOne()
    {
        var image = Place(new FilteredImage { Source = Solid(Colors.Red), Stretch = Stretch.Fill, Width = 60, Height = 60 });
        var animation = image.TransitionTo(Halves(), ImageTransitionKind.Pixelate, Linear);

        Seek(animation, 25);
        Assert.All(Row(image, 45)[..60], pixel => AssertNear(Colors.Red, pixel));

        // Blocks of 12.5 DIPs put x = 29 and 31 in the block sampled at x = 25, either side of the halves' seam.
        Seek(animation, 75);
        Assert.Equal(Row(image, 5)[29], Row(image, 5)[31]);

        Seek(animation, 100);
        Assert.NotEqual(Row(image, 5)[29], Row(image, 5)[31]);
    }

    [AvaloniaTheory]
    [InlineData(25)]
    [InlineData(40)]
    [InlineData(75)]
    public void APixelateTransitionNeverDarkensTheEdgeBlocks(double time)
    {
        var (image, animation) = RedToBlue(ImageTransitionKind.Pixelate);

        Seek(animation, time);

        Assert.All(Pixels(image), pixel => AssertNear(time < 50 ? Colors.Red : Colors.Blue, pixel));
    }

    [AvaloniaFact]
    public void TwoDrawingsAreEachRasterizedOnceThroughATransition()
    {
        var image = Place(new FilteredImage { Source = Drawing(Brushes.Red), Stretch = Stretch.Fill, Width = 60, Height = 60, Grayscale = 1 });
        var animation = image.TransitionTo(Drawing(Brushes.Blue), ImageTransitionKind.Crossfade, Linear);
        Seek(animation, 10);
        Assert.Equal(2, image.Rasterizations);

        foreach (var time in (double[])[30, 50, 70, 90]) Seek(animation, time);

        Assert.Equal(2, image.Rasterizations);
    }

    [AvaloniaFact]
    public void CompletingReleasesTheOutgoingImage()
    {
        var (image, animation) = RedToBlue(ImageTransitionKind.Dissolve);
        Assert.NotNull(image.Outgoing);
        var done = animation.Then();

        Frames.Advance(150, 10);
        Tick();

        Assert.True(done.IsCompletedSuccessfully);
        Assert.Null(image.Outgoing);
        Assert.Equal(1, image.TransitionProgress);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnInterruptionStartsFromTheCompositeOnScreenAndEndsOnTheNewSource(bool viaSource)
    {
        var (image, animation) = RedToBlue(ImageTransitionKind.Crossfade);
        var dissolve = new ImageTransition { Kind = ImageTransitionKind.Dissolve, Builder = Linear };
        Frames.Advance(50, 10);
        Tick();
        var before = Capture(image);

        if (viaSource)
        {
            image.SourceTransition = dissolve;
            image.Source = Solid(Colors.Lime);
        }
        else image.TransitionTo(Solid(Colors.Lime), dissolve);
        Assert.IsType<RenderTargetBitmap>(image.Outgoing);
        Tick();
        Assert.True(Difference(before, Capture(image)) < 1);
        Assert.True(animation.Cancelled);

        Frames.Advance(150, 10);
        Tick();
        Assert.Null(image.Outgoing);
        Assert.All(Pixels(image), pixel => AssertNear(Colors.Lime, pixel));
    }

    [AvaloniaFact]
    public void ColourFiltersApplyToBothImagesMidTransition()
    {
        var (image, animation) = RedToBlue(ImageTransitionKind.Crossfade);
        image.Grayscale = 1;

        Seek(animation, 50);

        Assert.All(Pixels(image), pixel =>
        {
            Assert.True(pixel.R > 10);
            Assert.InRange(pixel.G, pixel.R - 3, pixel.R + 3);
            Assert.InRange(pixel.B, pixel.R - 3, pixel.R + 3);
        });
    }

    [AvaloniaFact]
    public void RevertSnapsToTheNewSource()
    {
        var (image, animation) = RedToBlue(ImageTransitionKind.Wipe);
        Seek(animation, 50);

        animation.Revert();
        Tick();

        Assert.Null(image.Outgoing);
        Assert.Equal(1, image.TransitionProgress);
        Assert.All(Pixels(image), pixel => AssertNear(Colors.Blue, pixel));
    }

    [AvaloniaFact]
    public void TheSameSourceWithNoTransitionRunningChangesNothingButStillRunsTheTiming()
    {
        var image = Place(new FilteredImage { Source = Solid(Colors.Red), Stretch = Stretch.Fill, Width = 60, Height = 60 });
        var animation = image.TransitionTo(image.Source, ImageTransitionKind.Crossfade, Linear);

        Frames.Advance(50, 10);
        Tick();
        Assert.Null(image.Outgoing);
        Assert.Equal(1, image.TransitionProgress);
        Assert.All(Pixels(image), pixel => Assert.Equal(Colors.Red, pixel));

        Frames.Advance(100, 10);
        Assert.True(animation.Completed);
    }

    [AvaloniaFact]
    public void WithSourceTransitionAssigningSourcePlaysATransition()
    {
        var image = Place(new FilteredImage { Source = Solid(Colors.Red), SourceTransition = Crossfade, Stretch = Stretch.Fill, Width = 60, Height = 60 });

        image.Source = Solid(Colors.Blue);
        Frames.Advance(50, 10);
        Tick();
        Assert.All(Pixels(image), pixel => Assert.True(pixel.R > 30 && pixel.B > 30));

        Frames.Advance(150, 10);
        Tick();
        Assert.Null(image.Outgoing);
        Assert.All(Pixels(image), pixel => AssertNear(Colors.Blue, pixel));
    }

    [AvaloniaFact]
    public void WithoutSourceTransitionSourceSnaps()
    {
        var image = Place(new FilteredImage { Source = Solid(Colors.Red), Stretch = Stretch.Fill, Width = 60, Height = 60 });

        image.Source = Solid(Colors.Blue);
        Tick();

        Assert.Null(image.Outgoing);
        Assert.Equal(1, image.TransitionProgress);
        Assert.All(Pixels(image), pixel => AssertNear(Colors.Blue, pixel));
    }

    [AvaloniaFact]
    public void SettingSourceBeforeAttachDoesNotTransition()
    {
        var image = new FilteredImage { Source = Solid(Colors.Red), SourceTransition = Crossfade, Stretch = Stretch.Fill, Width = 60, Height = 60 };
        image.Source = Solid(Colors.Blue);

        Place(image);

        Assert.Null(image.Outgoing);
        Assert.All(Pixels(image), pixel => AssertNear(Colors.Blue, pixel));
    }

    [AvaloniaFact]
    public void ABoundSourceChangeTransitions()
    {
        var holder = new Border { Tag = Solid(Colors.Red) };
        var image = new FilteredImage { SourceTransition = Crossfade, Stretch = Stretch.Fill, Width = 60, Height = 60 };
        image.Bind(PrestoImageBase.SourceProperty, new Binding(nameof(Border.Tag)) { Source = holder });
        Place(image);

        holder.Tag = Solid(Colors.Blue);
        Assert.NotNull(image.Outgoing);
    }

    [AvaloniaFact]
    public void DetachingMidTransitionReleasesTheOutgoingImage()
    {
        var image = Place(new FilteredImage { Source = Solid(Colors.Red), SourceTransition = Crossfade, Stretch = Stretch.Fill, Width = 60, Height = 60 });
        image.Source = Solid(Colors.Blue);
        Frames.Advance(50, 10);
        Tick();
        image.Source = Solid(Colors.Lime);
        var snapshot = Assert.IsType<RenderTargetBitmap>(image.Outgoing);

        ((Window)image.Parent!).Content = null;
        Tick();

        Assert.Null(image.Outgoing);
        Assert.ThrowsAny<ObjectDisposedException>(() => snapshot.CreateDrawingContext());
    }

    [AvaloniaFact]
    public void XamlGivesSourceTransitionAsAStringAStyleSetterOrAnElement()
    {
        var view = Place(new ImageTransitionXamlView());

        Assert.Equal(ImageTransitionKind.Wipe, view.Parsed.SourceTransition!.Kind);
        Assert.Equal(800, view.Parsed.TransitionTo(Solid(Colors.Blue), view.Parsed.SourceTransition).Duration);
        Assert.Equal(ImageTransitionKind.Pixelate, view.Styled.SourceTransition!.Kind);
        Assert.Equal(new ImageTransition { Kind = ImageTransitionKind.Ripple, PeakRipple = 8 }, view.Element.SourceTransition);
    }

    private static AnimationBuilder Linear => Presto.Build().Duration(100).Ease(Eases.None);

    private static ImageTransition Crossfade => new() { Builder = Linear };

    private static (FilteredImage Image, Animation Animation) RedToBlue(ImageTransitionKind kind)
    {
        var image = Place(new FilteredImage { Source = Solid(Colors.Red), Stretch = Stretch.Fill, Width = 60, Height = 60, DissolveScale = 8 });
        return (image, image.TransitionTo(Solid(Colors.Blue), kind, Linear));
    }

    private static void Seek(Animation animation, double time)
    {
        animation.Pause();
        animation.Seek(time);
        Tick();
    }

    private static List<Color> Pixels(Control control)
    {
        var pixels = new List<Color>();
        for (var y = 0; y < control.Bounds.Height; y++) pixels.AddRange(Row(control, y)[..(int)control.Bounds.Width]);
        return pixels;
    }

    private static void AssertNear(Color expected, Color actual)
    {
        Assert.InRange(actual.R, expected.R - 3, expected.R + 3);
        Assert.InRange(actual.G, expected.G - 3, expected.G + 3);
        Assert.InRange(actual.B, expected.B - 3, expected.B + 3);
    }

    private static int ShownOutside(Control control)
    {
        var slot = SlotOf(control);
        var count = 0;
        for (var y = 0; y < 100; y++)
        {
            var row = Row(control, y);
            for (var x = 0; x < 100; x++)
                if (!slot.Contains(new Point(x + 0.5, y + 0.5)) && row[x] != Colors.Black) count++;
        }
        return count;
    }

    private static RenderTargetBitmap Solid(Color color, int width = 10, int height = 10)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
        using (var context = bitmap.CreateDrawingContext()) context.FillRectangle(new SolidColorBrush(color), new Rect(0, 0, width, height));
        return bitmap;
    }

    private static DrawingImage Drawing(IBrush brush) =>
        new(new GeometryDrawing { Brush = brush, Geometry = new RectangleGeometry(new Rect(0, 0, 10, 10)) });

    private static RenderTargetBitmap Halves()
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(20, 10));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.Blue, new Rect(0, 0, 10, 10));
            context.FillRectangle(Brushes.Lime, new Rect(10, 0, 10, 10));
        }
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
}
