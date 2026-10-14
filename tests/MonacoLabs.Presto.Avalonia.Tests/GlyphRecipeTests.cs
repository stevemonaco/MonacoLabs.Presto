using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MonacoLabs.Presto.Text;
using Path = Avalonia.Controls.Shapes.Path;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="Recipes.WriteIn"/> and <see cref="Recipes.MorphGlyphs"/>: what they split, play and end as.</summary>
public sealed class GlyphRecipeTests : BridgeTestBase
{
    [AvaloniaFact]
    public void WriteInTracesUnfilledOutlinesInReadingOrderThenEndsAsPlainText()
    {
        var block = Shown("Hi");
        var write = Recipes.WriteIn(block);
        var pieces = TextSplitter.Of(block)!.Chars.Cast<Path>().ToArray();
        write.Pause();
        write.Seek(Recipes.Tokens.StaggerStep / 2);

        Assert.All(pieces, p => Assert.Equal(0, ((ISolidColorBrush)p.Fill!).Color.A));
        Assert.Same(block.Foreground, pieces[0].Stroke);
        Assert.True(pieces[0].StrokeDashArray![0] > 0);
        Assert.Equal(0, pieces[1].StrokeDashArray![0]);

        write.Seek(Recipes.Tokens.StaggerStep + Recipes.Tokens.Long + Recipes.Tokens.Medium / 2);
        Assert.InRange(((ISolidColorBrush)pieces[0].Fill!).Color.A, 1, 254);
        write.Resume();
        RunUntil(() => write.Completed);
        AssertPlain(block, "Hi");
    }

    [AvaloniaFact]
    public void WritingInAgainMidWriteCompletesTheFirst()
    {
        var block = Shown("Hi");
        var first = Recipes.WriteIn(block);
        Frames.Advance(100, 10);
        var second = Recipes.WriteIn(block);

        Assert.True(first.Completed);
        Assert.All(TextSplitter.Of(block)!.Chars, c => Assert.IsType<Path>(c));
        RunUntil(() => second.Completed);
        AssertPlain(block, "Hi");
    }

    [AvaloniaFact]
    public void ReducedWriteInFadesTheBlockInWithoutSplitting()
    {
        var block = Shown("Hi");
        Presto.Defaults.ReducedMotion = true;
        var write = Recipes.WriteIn(block);

        Assert.Null(TextSplitter.Of(block));
        Assert.Equal(Recipes.Tokens.Short, write.Duration, 1);
        write.Pause();
        write.Seek(0);
        Assert.Equal(0, block.Opacity);
        write.Seek(write.Duration);
        Assert.Equal(1, block.Opacity);
    }

    [AvaloniaFact]
    public void MorphGlyphsMorphsEachOutlineIntoTheCharacterAtItsPositionAndEndsAsPlainText()
    {
        var block = Shown("ab");
        var old = Outline("a", block);
        var morph = Recipes.MorphGlyphs(block, "cd");
        var pieces = TextSplitter.Of(block)!.Chars.Cast<Path>().ToArray();
        morph.Pause();
        morph.Seek(0);

        AssertNear(old, pieces[0].Data!.Bounds);
        morph.Seek(Recipes.Tokens.Long - 0.01);
        AssertNear(Outline("c", block), pieces[0].Data!.Bounds);
        morph.Resume();
        RunUntil(() => morph.Completed);
        AssertPlain(block, "cd");
    }

    [AvaloniaFact]
    public void ANumberKeepsItsUnitsColumnAndAnExtraDigitGrowsFromItsSlotCenter()
    {
        var block = Shown("9");
        block.TextAlignment = TextAlignment.Right;
        block.UpdateLayout();
        var nine = Place(block, 0);
        var morph = Recipes.MorphGlyphs(block, "10");
        var split = TextSplitter.Of(block)!;
        morph.Pause();
        morph.Seek(0);

        AssertAt(nine, Place(block, 1));
        var one = split.Chars[0];
        AssertAt(split.SlotOf(one).Center, one.TranslatePoint(default, block)!.Value);
        morph.Seek(Recipes.Tokens.Long - 0.01);
        AssertAt(nine, Place(block, 1));
        morph.Resume();
        RunUntil(() => morph.Completed);
        AssertPlain(block, "10");
    }

    [AvaloniaFact]
    public void AnExtraOldDigitCollapsesIntoItsSlotCenterAndIsDismissed()
    {
        var block = Shown("10");
        var morph = Recipes.MorphGlyphs(block, "9");
        var ghost = Assert.Single(OverlayLayer.GetOverlayLayer(block)!.Children.SelectMany(c => ((Canvas)c).Children));
        morph.Pause();
        morph.Seek(Recipes.Tokens.Long - 0.01);

        Assert.Equal(0, Convert.ToDouble(PrestoAvalonia.Get(ghost, "Scale")), 0.05);
        morph.Resume();
        RunUntil(() => morph.Completed);
        Assert.Empty(OverlayLayer.GetOverlayLayer(block)!.Children);
        AssertPlain(block, "9");
    }

    [AvaloniaFact]
    public void APairWithoutAnOutlineCrossfades()
    {
        var block = Shown("a😀");
        var morph = Recipes.MorphGlyphs(block, "b😀");
        var ghost = Assert.Single(OverlayLayer.GetOverlayLayer(block)!.Children.SelectMany(c => ((Canvas)c).Children));
        var emoji = Assert.IsType<TextBlock>(TextSplitter.Of(block)!.Chars[1]);
        morph.Pause();
        morph.Seek(0);

        Assert.Equal(0, emoji.Opacity);
        morph.Seek(Recipes.Tokens.Long);
        Assert.Equal(1, emoji.Opacity);
        Assert.Equal(0, ghost.Opacity);
        morph.Resume();
        RunUntil(() => morph.Completed);
        AssertPlain(block, "b😀");
    }

    [AvaloniaFact]
    public void MorphingGlyphsAgainMidMorphCompletesTheFirst()
    {
        var block = Shown("12");
        var first = Recipes.MorphGlyphs(block, "34");
        Frames.Advance(100, 10);
        var second = Recipes.MorphGlyphs(block, "56");

        Assert.True(first.Completed);
        Assert.Equal(2, TextSplitter.Of(block)!.Chars.Count);
        RunUntil(() => second.Completed);
        AssertPlain(block, "56");
    }

    [AvaloniaFact]
    public void ReducedMorphGlyphsCrossfadesWithoutSplitting()
    {
        var block = Shown("one");
        Presto.Defaults.ReducedMotion = true;
        var morph = Recipes.MorphGlyphs(block, "two");
        var tokens = Recipes.Tokens;

        Assert.Null(TextSplitter.Of(block));
        Assert.Equal(2 * tokens.Short, morph.Duration, 1);
        morph.Pause();
        morph.Seek(tokens.Short + 1);
        Assert.Equal("two", block.Text);
        morph.Seek(morph.Duration);
        Assert.Equal(1, block.Opacity);
    }

    [AvaloniaTheory]
    [InlineData("MorphGlyphs")]
    [InlineData("MorphText")]
    [InlineData("WriteIn")]
    public void ATextMorphWhoseBlockDetachesMidPlayCompletes(string recipe)
    {
        var block = Shown("12");
        var window = (Window)TopLevel.GetTopLevel(block)!;
        var overlay = OverlayLayer.GetOverlayLayer(window)!;
        var before = overlay.Children.Count;
        var morph = recipe switch
        {
            "MorphGlyphs" => Recipes.MorphGlyphs(block, "9"),
            "MorphText" => Recipes.MorphText(block, "9"),
            _ => Recipes.WriteIn(block),
        };
        Frames.Advance(100, 10);

        window.Content = null;

        Assert.True(morph.Completed);
        Assert.Equal(before, overlay.Children.Count);
        Assert.True(double.IsNaN(block.Width));
        AssertPlain(block, recipe == "WriteIn" ? "12" : "9");
    }

    private static TextBlock Shown(string text)
    {
        var block = new TextBlock { Text = text, FontSize = 40 };
        Show(block);
        return block;
    }

    private static Rect Outline(string grapheme, TextBlock block) =>
        PrestoAvalonia.TextOutline(grapheme, new Typeface(block.FontFamily), block.FontSize).Bounds;

    /// <summary>Where the character piece at <paramref name="index"/> is drawn in the block, its <c>X</c>/<c>Y</c> included.</summary>
    private static Point Place(TextBlock block, int index)
    {
        var split = TextSplitter.Of(block) ?? PrestoAvalonia.SplitText(block, new() { Words = null, Chars = new() });
        var piece = split.Chars[index];
        var at = split.SlotOf(piece).Position + new Vector(Convert.ToDouble(PrestoAvalonia.Get(piece, "X")), Convert.ToDouble(PrestoAvalonia.Get(piece, "Y")));
        if (piece is TextBlock) split.Revert();
        return at;
    }

    private static void AssertPlain(TextBlock block, string text)
    {
        Assert.Equal(text, block.Text);
        Assert.Null(block.Inlines);
        Assert.Null(TextSplitter.Of(block));
    }

    private static void AssertAt(Point expected, Point actual)
    {
        Assert.Equal(expected.X, actual.X, 0.5);
        Assert.Equal(expected.Y, actual.Y, 0.5);
    }

    private static void AssertNear(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, 1.0);
        Assert.Equal(expected.Y, actual.Y, 1.0);
        Assert.Equal(expected.Right, actual.Right, 1.0);
        Assert.Equal(expected.Bottom, actual.Bottom, 1.0);
    }
}
