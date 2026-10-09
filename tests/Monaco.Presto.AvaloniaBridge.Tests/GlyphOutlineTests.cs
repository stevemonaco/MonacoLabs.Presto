using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Text;
using SkiaSharp;

namespace Monaco.Presto.AvaloniaBridge.Tests;

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
