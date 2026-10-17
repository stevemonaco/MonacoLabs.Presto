using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
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
        var pieces = Pieces(block).Cast<Path>().ToArray();
        morph.Pause();
        morph.Seek(0);

        Assert.Equal(2, pieces.Length);
        AssertNear(old, pieces[0].Data!.Bounds);
        morph.Seek(Recipes.Tokens.Long - 0.01);
        AssertNear(Outline("c", block), pieces[0].Data!.Bounds);
        morph.Resume();
        RunUntil(() => morph.Completed);
        AssertPlain(block, "cd");
        Assert.Empty(Pieces(block));
    }

    [AvaloniaFact]
    public void MorphGlyphsEqualCharactersInPlaceStayAsTheBlocksText()
    {
        var block = Shown("ab");
        var morph = Recipes.MorphGlyphs(block, "ac");

        Assert.Single(Pieces(block));
        var runs = block.Inlines!.Cast<Run>().ToArray();
        Assert.Equal(["a", "c"], runs.Select(r => r.Text));
        Assert.NotEqual(0, ((ISolidColorBrush)runs[0].Foreground!).Color.A);
        Assert.Equal(0, ((ISolidColorBrush)runs[1].Foreground!).Color.A);
        RunUntil(() => morph.Completed);
        AssertPlain(block, "ac");
    }

    [AvaloniaFact]
    public void MorphGlyphsAnEqualCharacterWhoseSlotMovedSlides()
    {
        var block = Shown("a");
        block.TextAlignment = TextAlignment.Center;
        block.UpdateLayout();
        var old = Slot(block, 0).Position;
        var morph = Recipes.MorphGlyphs(block, "aa");
        var slide = Assert.IsType<Path>(Pieces(block)[0]);
        var shape = slide.Data;
        morph.Pause();
        morph.Seek(0);

        AssertAt(old, Drawn(slide, block));
        morph.Seek(Recipes.Tokens.Long - 0.01);
        AssertAt(Slot(block, 0).Position, Drawn(slide, block));
        Assert.Same(shape, slide.Data);
        morph.Resume();
        RunUntil(() => morph.Completed);
        AssertPlain(block, "aa");
    }

    [AvaloniaFact]
    public void MorphGlyphsNoChangeAddsNoHostAndCompletesAtOnce()
    {
        var block = Shown("12");
        var adorners = AdornerLayer.GetAdornerLayer(block)!.Children.Count;
        var overlays = OverlayLayer.GetOverlayLayer(block)!.Children.Count;

        var morph = Recipes.MorphGlyphs(block, "12");

        Assert.True(morph.Completed);
        Assert.Equal(adorners, AdornerLayer.GetAdornerLayer(block)!.Children.Count);
        Assert.Equal(overlays, OverlayLayer.GetOverlayLayer(block)!.Children.Count);
        Assert.True(double.IsNaN(block.Width));
        AssertPlain(block, "12");
    }

    [AvaloniaFact]
    public void MorphGlyphsOnlyTheChangedDigitsGetPieces()
    {
        var block = new TextBlock { Text = "24:10", FontFamily = Inter, FontSize = 40, FontFeatures = [FontFeature.Parse("+tnum")] };
        Show(block);
        var morph = Recipes.MorphGlyphs(block, "24:09");

        Assert.Equal(2, Pieces(block).OfType<Path>().Count());
        var runs = block.Inlines!.Cast<Run>().ToArray();
        Assert.Equal(["24:", "0", "9"], runs.Select(r => r.Text));
        Assert.NotEqual(0, ((ISolidColorBrush)runs[0].Foreground!).Color.A);
        RunUntil(() => morph.Completed);
        AssertPlain(block, "24:09");
    }

    [AvaloniaFact]
    public void MorphGlyphsPiecesFollowAScaledViewbox()
    {
        var block = new TextBlock { Text = "ab", FontSize = 40 };
        var window = Show(new Viewbox { Child = block });
        var morph = Recipes.MorphGlyphs(block, "cd");
        var second = Pieces(block)[1];
        morph.Pause();
        morph.Seek(Recipes.Tokens.Long - 0.01);

        Assert.True(block.TranslatePoint(new Point(10, 0), window)!.Value.X - block.TranslatePoint(default, window)!.Value.X > 20);
        AssertAt(block.TranslatePoint(Slot(block, 1).Position, window)!.Value, second.TranslatePoint(default, window)!.Value);
        morph.Resume();
        RunUntil(() => morph.Completed);
    }

    [AvaloniaFact]
    public void ANumberKeepsItsUnitsColumnAndAnExtraDigitGrowsFromItsSlotCenter()
    {
        var block = Shown("9");
        block.TextAlignment = TextAlignment.Right;
        block.UpdateLayout();
        var nine = Slot(block, 0).Position;
        var morph = Recipes.MorphGlyphs(block, "10");
        var pieces = Pieces(block);
        morph.Pause();
        morph.Seek(0);

        AssertAt(nine, Drawn(pieces[1], block));
        AssertAt(Slot(block, 0).Center, Drawn(pieces[0], block));
        morph.Seek(Recipes.Tokens.Long - 0.01);
        AssertAt(nine, Drawn(pieces[1], block));
        morph.Resume();
        RunUntil(() => morph.Completed);
        AssertPlain(block, "10");
    }

    [AvaloniaFact]
    public void AClockLosingAMinuteDigitPairsFromTheEnd()
    {
        var block = new TextBlock { Text = "100:00", FontFamily = Inter, FontSize = 40, FontFeatures = [FontFeature.Parse("+tnum")], TextAlignment = TextAlignment.Right };
        Show(block);
        var (one, units) = (Slot(block, 0).Position, Slot(block, 5).Position);
        var morph = Recipes.MorphGlyphs(block, "99:59");
        var pieces = Pieces(block);
        morph.Pause();
        morph.Seek(0);

        Assert.Equal(5, pieces.Length);
        AssertAt(units, Drawn(pieces[3], block));
        AssertAt(one, Drawn(pieces[4], block));
        morph.Seek(Recipes.Tokens.Long - 0.01);
        Assert.Equal(0, Convert.ToDouble(PrestoAvalonia.Get(pieces[4], "Scale")), 0.05);
        morph.Resume();
        RunUntil(() => morph.Completed);
        AssertPlain(block, "99:59");
    }

    [AvaloniaFact]
    public void ALabelAndANumeralPairFromTheStart()
    {
        var block = Shown("10:00");
        var last = Slot(block, 4).Position;
        var morph = Recipes.MorphGlyphs(block, "DONE");
        var collapsing = Pieces(block)[^1];
        morph.Pause();
        morph.Seek(0);

        AssertAt(last, Drawn(collapsing, block));
        morph.Resume();
        RunUntil(() => morph.Completed);
        AssertPlain(block, "DONE");
    }

    [AvaloniaFact]
    public void AnExtraOldDigitCollapsesIntoItsSlotCenterAndIsDismissed()
    {
        var block = Shown("10");
        var adorners = AdornerLayer.GetAdornerLayer(block)!.Children.Count;
        var morph = Recipes.MorphGlyphs(block, "9");
        var collapsing = Pieces(block)[1];
        morph.Pause();
        morph.Seek(Recipes.Tokens.Long - 0.01);

        Assert.Equal(0, Convert.ToDouble(PrestoAvalonia.Get(collapsing, "Scale")), 0.05);
        morph.Resume();
        RunUntil(() => morph.Completed);
        Assert.Equal(adorners, AdornerLayer.GetAdornerLayer(block)!.Children.Count);
        AssertPlain(block, "9");
    }

    [AvaloniaFact]
    public void MorphGlyphsALeavingCharacterIsAnOutlineInTheForeground()
    {
        var block = Shown("ab");
        var overlays = OverlayLayer.GetOverlayLayer(block)!.Children.Count;
        var morph = Recipes.MorphGlyphs(block, "a");

        var leaving = Assert.IsType<Path>(Assert.Single(Pieces(block)));
        Assert.Same(block.Foreground, leaving.Fill);
        Assert.Equal(overlays, OverlayLayer.GetOverlayLayer(block)!.Children.Count);
        RunUntil(() => morph.Completed);
        AssertPlain(block, "a");
    }

    [AvaloniaFact]
    public void APairWithoutAnOutlineCrossfades()
    {
        var block = Shown("a😀");
        var morph = Recipes.MorphGlyphs(block, "a😎");
        var pieces = Pieces(block);
        var (entering, leaving) = (Assert.IsType<TextBlock>(pieces[0]), Assert.IsType<TextBlock>(pieces[1]));
        morph.Pause();
        morph.Seek(0);

        Assert.Equal(0, entering.Opacity);
        Assert.Equal(1, leaving.Opacity);
        morph.Seek(Recipes.Tokens.Long);
        Assert.Equal(1, entering.Opacity);
        Assert.Equal(0, leaving.Opacity);
        morph.Resume();
        RunUntil(() => morph.Completed);
        AssertPlain(block, "a😎");
    }

    [AvaloniaFact]
    public void MorphGlyphsASetTextMidMorphCompletesAndKeepsIt()
    {
        var source = new TextBlock { Text = "12" };
        var block = new TextBlock { FontSize = 40, [!TextBlock.TextProperty] = source[!TextBlock.TextProperty] };
        Show(block);
        var morph = Recipes.MorphGlyphs(block, "34");
        Frames.Advance(100, 10);

        source.Text = "56";

        Assert.True(morph.Completed);
        AssertPlain(block, "56");
        Assert.Empty(Pieces(block));
        Assert.True(double.IsNaN(block.Width));
        source.Text = "78";
        Assert.Equal("78", block.Text);
    }

    [AvaloniaFact]
    public void MorphGlyphsABlockWithoutAnAdornerLayerCompletesAtOnce()
    {
        var block = new TextBlock { Text = "12", FontSize = 40 };
        var window = new Window
        {
            Content = block,
            Template = new FuncControlTemplate<Window>((w, _) => new ContentPresenter { Name = "PART_ContentPresenter", [!ContentPresenter.ContentProperty] = w[!ContentControl.ContentProperty] }),
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Null(AdornerLayer.GetAdornerLayer(block));

        var morph = Recipes.MorphGlyphs(block, "9");

        Assert.True(morph.Completed);
        Assert.Equal(1, block.Opacity);
        AssertPlain(block, "9");
    }

    [AvaloniaFact]
    public void MorphingGlyphsAgainMidMorphCompletesTheFirst()
    {
        var block = Shown("12");
        var first = Recipes.MorphGlyphs(block, "34");
        Frames.Advance(100, 10);
        var second = Recipes.MorphGlyphs(block, "56");

        Assert.True(first.Completed);
        Assert.Equal(2, Pieces(block).Length);
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
        Assert.Empty(Pieces(block));
        Assert.Equal(2 * tokens.Short, morph.Duration, 1);
        morph.Pause();
        morph.Seek(tokens.Short + 1);
        Assert.Equal("two", block.Text);
        morph.Seek(morph.Duration);
        Assert.Equal(1, block.Opacity);
    }

    [AvaloniaFact]
    public void ReducedInstantSetsTheTextAtOnce()
    {
        var source = new TextBlock { Text = "12:30" };
        var block = new TextBlock { FontSize = 40, [!TextBlock.TextProperty] = source[!TextBlock.TextProperty] };
        Show(block);
        Presto.Defaults.ReducedMotion = true;

        var morph = Recipes.MorphGlyphs(block, "12:29", ReducedMorph.Instant);

        Assert.True(morph.Completed);
        AssertPlain(block, "12:29");
        Assert.Empty(Pieces(block));
        Frames.Advance(100, 10);
        Assert.Equal(1, block.Opacity);
        source.Text = "12:28";
        Assert.Equal("12:28", block.Text);
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

    [AvaloniaTheory]
    [InlineData("AfterDetach")]
    [InlineData("DuringDetach")]
    [InlineData("BeforeClose")]
    public void AMorphStartedAsItsBlockLeavesASecondWindowCompletes(string when)
    {
        Show(new Border());
        var block = new TextBlock { Text = "12", FontSize = 40 };
        var parent = new Border { Child = block };
        var window = Show(parent);
        Timeline? morph = null;
        if (when == "DuringDetach") parent.DetachedFromVisualTree += (_, _) => morph = Recipes.MorphGlyphs(block, "9");

        if (when == "BeforeClose") morph = Recipes.MorphGlyphs(block, "9");
        if (when == "BeforeClose") window.Close();
        else window.Content = null;
        if (when == "AfterDetach") morph = Recipes.MorphGlyphs(block, "9");
        Frames.Advance(100, 10);

        Assert.True(morph!.Completed);
        Assert.True(double.IsNaN(block.Width));
        Assert.Equal("9", block.Text);
        Assert.Empty(block.Inlines ?? []);
        Assert.Null(TextSplitter.Of(block));
        Assert.Equal(1, block.Opacity);
    }

    [AvaloniaFact]
    public void HidingACharacterKeepsTheBlocksLayout()
    {
        var block = new TextBlock { Text = "12:30", FontFamily = Inter, FontSize = 40 };
        Show(block);
        var plain = Glyphs(block);

        BlockGlyphs.Hide(block, "12:30", new HashSet<int> { 3 });

        Assert.Equal(plain, Glyphs(block));
        Assert.Contains(block.Inlines!, i => i.Foreground is ISolidColorBrush { Color.A: 0 });
    }

    [AvaloniaFact]
    public void HideThenRestoreKeepsABoundTextUpdating()
    {
        var source = new TextBlock { Text = "12:30" };
        var block = new TextBlock { FontSize = 40, [!TextBlock.TextProperty] = source[!TextBlock.TextProperty] };
        Show(block);

        BlockGlyphs.Hide(block, "12:30", new HashSet<int> { 3 });
        BlockGlyphs.Restore(block, "12:31");
        AssertPlain(block, "12:31");
        source.Text = "12:32";

        Assert.Equal("12:32", block.Text);
    }

    private static TextBlock Shown(string text)
    {
        var block = new TextBlock { Text = text, FontSize = 40 };
        Show(block);
        return block;
    }

    private static Rect Outline(string grapheme, TextBlock block) =>
        PrestoAvalonia.TextOutline(grapheme, new Typeface(block.FontFamily), block.FontSize).Bounds;

    private static Control[] Pieces(TextBlock block)
    {
        block.UpdateLayout();
        return AdornerLayer.GetAdornerLayer(block)!.Children.Where(c => AdornerLayer.GetAdornedElement(c) == block).SelectMany(c => ((Canvas)c).Children).ToArray();
    }

    private static Rect Slot(TextBlock block, int index) => BlockGlyphs.SlotOf(block, (index, 1));

    private static Point Drawn(Control piece, TextBlock block) => piece.TranslatePoint(default, block)!.Value;

    private static List<(ushort Glyph, double X, double Y)> Glyphs(TextBlock block)
    {
        var glyphs = new List<(ushort, double, double)>();
        var top = 0d;
        foreach (var line in block.TextLayout.TextLines)
        {
            var x = line.Start;
            foreach (var run in line.TextRuns.OfType<ShapedTextRun>())
            {
                foreach (var glyph in run.GlyphRun.GlyphInfos)
                {
                    glyphs.Add((glyph.GlyphIndex, Math.Round(x + glyph.GlyphOffset.X, 3), Math.Round(top + line.Baseline + glyph.GlyphOffset.Y, 3)));
                    x += glyph.GlyphAdvance;
                }
            }
            top += line.Height;
        }
        return glyphs;
    }

    private static void AssertPlain(TextBlock block, string text)
    {
        Assert.Equal(text, block.Text);
        Assert.True(block.Inlines is null or []);
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
