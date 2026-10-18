using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using MonacoLabs.Presto.Text;
using SkiaSharp;

namespace MonacoLabs.Presto.Tests;

/// <summary>Covers <c>PrestoAvalonia.GlyphOutline</c>, <c>PrestoAvalonia.TextOutline</c> and the contour model behind them.</summary>
public sealed class GlyphOutlineTests : BridgeTestBase
{
    private static readonly Typeface Font = new(FontFamily.Default);

    private static Contour[] ContoursOf(string text, double fontSize = 100)
    {
        using var path = GlyphOutlines.Path(text, Font, fontSize);
        return GlyphContours.From(path).Contours;
    }

    private static Rect InkOf(TextLayout layout) =>
        layout.TextLines[0].TextRuns.OfType<ShapedTextRun>().Single().GlyphRun.BuildGeometry().Bounds;

    private static void AssertNear(Rect expected, Rect actual, double tolerance = 0.05)
    {
        Assert.Equal(expected.X, actual.X, tolerance);
        Assert.Equal(expected.Y, actual.Y, tolerance);
        Assert.Equal(expected.Width, actual.Width, tolerance);
        Assert.Equal(expected.Height, actual.Height, tolerance);
    }

    [AvaloniaFact]
    public void EachCharOutlineLandsOnItsPieceInsideItsSlot()
    {
        var block = new TextBlock { Text = "Hello world", FontSize = 40 };
        Show(block);
        var split = PrestoAvalonia.SplitText(block, new SplitTextParams { Words = null, Chars = new() });

        for (var i = 0; i < split.Chars.Count; i++)
        {
            var piece = Assert.IsType<TextBlock>(split.Chars[i]);
            var outline = PrestoAvalonia.GlyphOutline(block, i).Bounds;
            var slot = split.SlotOf(piece);

            AssertNear(InkOf(piece.TextLayout), outline);
            Assert.True(slot.Inflate(0.5).Contains(outline.Translate(slot.Position)), $"char {i}: {outline} outside {slot}");
        }
    }

    [AvaloniaFact]
    public void GlyphOutlineCountsCharactersWithoutWhitespace()
    {
        var block = new TextBlock { Text = "a b", FontSize = 40 };

        AssertNear(PrestoAvalonia.TextOutline("b", Font, 40).Bounds, PrestoAvalonia.GlyphOutline(block, 1).Bounds);
        Assert.Throws<ArgumentOutOfRangeException>(() => PrestoAvalonia.GlyphOutline(block, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrestoAvalonia.GlyphOutline(block, -1));
    }

    [AvaloniaFact]
    public void TextOutlineOriginIsTheTopLeftOfTheFirstLineBox()
    {
        var outline = PrestoAvalonia.TextOutline("Hi", Font, 50).Bounds;

        AssertNear(InkOf(new TextLayout("Hi", Font, 50, null)), outline);
        Assert.True(outline.Top > 0 && outline.Left >= 0 && outline.Left < 10);
    }

    [AvaloniaFact]
    public void OutlinesShapeWithTheBlocksFontFeatures()
    {
        var proportional = new TextBlock { Text = "1", FontFamily = Inter, FontSize = 40 };
        var tabular = new TextBlock { Text = "1", FontFamily = Inter, FontSize = 40, FontFeatures = [FontFeature.Parse("+tnum")] };

        Assert.NotEqual(PrestoAvalonia.GlyphOutline(proportional, 0).Bounds.X, PrestoAvalonia.GlyphOutline(tabular, 0).Bounds.X, 0.5);
    }

    [AvaloniaFact]
    public void TextOutlineTakesFontFeatures()
    {
        var inter = new Typeface(Inter);

        var proportional = PrestoAvalonia.TextOutline("11", inter, 40).Bounds;
        var tabular = PrestoAvalonia.TextOutline("11", inter, 40, [FontFeature.Parse("+tnum")]).Bounds;

        Assert.NotEqual(proportional.Width, tabular.Width, 0.5);
    }

    [AvaloniaFact]
    public void AGraphemesOutlineInContextIsTheGlyphTheBlockDraws()
    {
        var block = new TextBlock { Text = "12:30", FontFamily = Inter, FontSize = 40, Padding = new Thickness(7, 5) };
        Show(block);
        var chars = TextSplitter.CharRangesOf("12:30").ToArray();
        var inter = new Typeface(Inter);

        var colon = BlockGlyphs.OutlineOf(block, chars[2], BlockGlyphs.SlotOf(block, chars[2]))!.Bounds;
        var lone = PrestoAvalonia.TextOutline(":", inter, 40).Bounds;
        Assert.True(colon.Top < lone.Top - 1, $"{colon} is not raised above {lone}");

        AssertNear(PrestoAvalonia.TextOutline("3", inter, 40).Bounds, BlockGlyphs.OutlineOf(block, chars[3], BlockGlyphs.SlotOf(block, chars[3]))!.Bounds);
        var slot = BlockGlyphs.SlotOf(block, chars[3]);
        Assert.Equal(block.Padding.Top, slot.Top, 0.05);
        Assert.True(slot.Left > BlockGlyphs.SlotOf(block, chars[2]).Right - 0.05);
    }

    [AvaloniaFact]
    public void TextOutlineStacksLinesDownward()
    {
        var single = PrestoAvalonia.TextOutline("H", Font, 50).Bounds;
        var lineHeight = new TextLayout("H", Font, 50, null).TextLines[0].Height;

        var twoLines = PrestoAvalonia.TextOutline("H\nH", Font, 50).Bounds;

        Assert.Equal(single.Top, twoLines.Top, 0.05);
        Assert.Equal(single.Bottom + lineHeight, twoLines.Bottom, 0.05);
    }

    [AvaloniaFact]
    public void OHasAnOuterContourAndAHole()
    {
        var contours = ContoursOf("O");

        Assert.Equal(2, contours.Length);
        var hole = Assert.Single(contours, c => c.IsHole);
        var outer = Assert.Single(contours, c => !c.IsHole);
        Assert.True(outer.Contains(hole));
    }

    [AvaloniaFact]
    public void BHasOneOuterContourAndTwoHoles()
    {
        var contours = ContoursOf("B");

        Assert.Equal(3, contours.Length);
        Assert.Equal(2, contours.Count(c => c.IsHole));
    }

    [AvaloniaTheory]
    [InlineData(SKPathDirection.Clockwise)]
    [InlineData(SKPathDirection.CounterClockwise)]
    public void ClassifiesHolesWhicheverWayTheOuterContourWinds(SKPathDirection outerDirection)
    {
        var innerDirection = outerDirection == SKPathDirection.Clockwise ? SKPathDirection.CounterClockwise : SKPathDirection.Clockwise;
        using var path = new SKPath();
        path.AddRect(new SKRect(0, 0, 100, 100), outerDirection);
        path.AddRect(new SKRect(20, 20, 40, 40), innerDirection);
        path.AddRect(new SKRect(60, 60, 80, 80), outerDirection);

        var contours = GlyphContours.From(path).Contours;

        Assert.Equal([false, true, false], contours.Select(c => c.IsHole));
    }

    [AvaloniaFact]
    public void WhitespaceHasAnEmptyOutline()
    {
        Assert.Equal(default, PrestoAvalonia.TextOutline(" \t", Font, 40).Bounds);
        Assert.Empty(ContoursOf(" "));
    }

    [AvaloniaFact]
    public void EmojiHasAnEmptyOutline()
    {
        var emoji = new TextLayout("😀", Font, 40, null).TextLines[0].TextRuns.OfType<ShapedTextRun>().Single().GlyphRun;
        Assert.NotEqual(0, emoji.GlyphInfos[0].GlyphIndex);

        Assert.Equal(default, PrestoAvalonia.TextOutline("😀", Font, 40).Bounds);
    }

    // Impact has no bold or italic face, so Avalonia simulates either, though not on the first lookup of the family.
    [AvaloniaTheory]
    [InlineData(FontSimulations.Bold)]
    [InlineData(FontSimulations.Oblique)]
    public void ASimulatedBoldOrObliqueOutlineCoversTheInkAvaloniaDraws(FontSimulations simulation)
    {
        var plain = new Typeface("Impact");
        Assert.Equal(FontSimulations.None, plain.GlyphTypeface.FontSimulations);
        var simulated = simulation == FontSimulations.Bold ? new Typeface("Impact", weight: FontWeight.Bold) : new Typeface("Impact", FontStyle.Italic);
        Assert.Equal(simulation, simulated.GlyphTypeface.FontSimulations);

        var outline = PrestoAvalonia.TextOutline("I", simulated, 100).Bounds;
        var ink = InkDrawn(new TextBlock { Text = "I", FontFamily = simulated.FontFamily, FontWeight = simulated.Weight, FontStyle = simulated.Style, FontSize = 100 });

        AssertNear(ink, outline, 1.5);
        Assert.True(outline.Width > PrestoAvalonia.TextOutline("I", plain, 100).Bounds.Width + 3, $"{simulation} left the outline at {outline}");
    }

    private static Rect InkDrawn(TextBlock block)
    {
        block.Measure(Size.Infinity);
        // Stretched wider than its text, since an oblique glyph leans past its advance and a block clips to its bounds.
        var stage = new Border { Width = block.DesiredSize.Width * 3, Height = block.DesiredSize.Height, Child = block };
        stage.Measure(Size.Infinity);
        stage.Arrange(new Rect(stage.DesiredSize));
        var size = new PixelSize((int)Math.Ceiling(stage.Bounds.Width), (int)Math.Ceiling(stage.Bounds.Height));
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(size);
        bitmap.Render(stage);
        var pixels = new byte[size.Width * size.Height * 4];
        var pinned = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), pinned.AddrOfPinnedObject(), pixels.Length, size.Width * 4);
        }
        finally
        {
            pinned.Free();
        }
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                if (pixels[((y * size.Width) + x) * 4 + 3] < 128) continue;
                (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x + 1), Math.Max(bottom, y + 1));
            }
        }
        return new Rect(left, top, right - left, bottom - top);
    }

    [AvaloniaFact]
    public void FlatteningCapsEverySegmentStraightEdgesIncluded()
    {
        var contours = ContoursOf("IO", 200);

        Assert.Equal(3, contours.Length);
        foreach (var points in contours.Select(c => c.Points))
        {
            for (var i = 0; i < points.Length; i++)
                Assert.True(Point.Distance(points[i], points[(i + 1) % points.Length]) <= GlyphContours.MaxSegment + 1e-9);
        }
    }
}
