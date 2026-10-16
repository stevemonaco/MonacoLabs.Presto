using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Text;
using Path = Avalonia.Controls.Shapes.Path;

namespace MonacoLabs.Presto.Tests;

/// <summary>Covers <c>PrestoAvalonia.SplitText</c>: the <c>splitText</c> half of tests/suites/text.test.js, adapted to a laid-out <see cref="TextBlock"/>.</summary>
public sealed class TextSplitTests : BridgeTestBase
{
    private const string Sentence = "The quick brown fox jumps over the lazy dog near the river bank at dawn";
    private static readonly string[] SentenceWords = Sentence.Split(' ');

    /// <summary>Line splitting reads real text metrics, so the block has to be in a shown window and laid out first.</summary>
    private static TextBlock ShowText(double width = 260)
    {
        var block = new TextBlock { Text = Sentence, TextWrapping = TextWrapping.Wrap, FontSize = 20 };
        new Window { Content = new Decorator { Child = block }, Width = width, Height = 400 }.Show();
        Dispatcher.UIThread.RunJobs();
        return block;
    }

    private static StackPanel Root(TextBlock block) =>
        Assert.IsType<StackPanel>(Assert.IsType<InlineUIContainer>(block.Inlines![0]).Child);

    private static string TextOf(Control piece) => piece switch
    {
        TextBlock text => text.Text ?? "",
        Border border => TextOf((Control)border.Child!),
        Panel panel => string.Concat(panel.Children.Select(TextOf)),
        _ => "",
    };

    [AvaloniaFact]
    public void DefaultsToWordsOnly()
    {
        var split = PrestoAvalonia.SplitText(ShowText());

        Assert.Empty(split.Lines);
        Assert.Empty(split.Chars);
        Assert.Equal(SentenceWords, split.Words.Select(TextOf));
    }

    [AvaloniaFact]
    public void SplitsCharsOnly()
    {
        var split = PrestoAvalonia.SplitText(ShowText(), new() { Words = null, Chars = new() });

        Assert.Empty(split.Lines);
        Assert.Empty(split.Words);
        Assert.Equal(Sentence.Replace(" ", ""), string.Concat(split.Chars.Select(TextOf)));
    }

    [AvaloniaFact]
    public void SplitsLinesOnly()
    {
        var block = ShowText();
        var split = PrestoAvalonia.SplitText(block, new() { Lines = new(), Words = null });

        Assert.True(split.Lines.Count > 1);
        Assert.Equal(split.Lines.Count, Root(block).Children.Count);
        Assert.Empty(split.Words);
        Assert.Empty(split.Chars);
        Assert.Equal(Sentence, string.Join(" ", split.Lines.Select(TextOf)));
    }

    [AvaloniaFact]
    public void SplitsWordsAndChars()
    {
        var split = PrestoAvalonia.SplitText(ShowText(), new() { Chars = new() });

        Assert.Empty(split.Lines);
        Assert.Equal(SentenceWords, split.Words.Select(TextOf));
        Assert.Equal(Sentence.Replace(" ", ""), string.Concat(split.Chars.Select(TextOf)));
    }

    [AvaloniaFact]
    public void SplitsWordsAndLines()
    {
        var block = ShowText();
        var split = PrestoAvalonia.SplitText(block, new() { Lines = new() });

        Assert.True(split.Lines.Count > 1);
        Assert.Equal(split.Lines.Count, Root(block).Children.Count);
        Assert.Equal(SentenceWords, split.Words.Select(TextOf));
        Assert.Empty(split.Chars);
    }

    [AvaloniaFact]
    public void SplitsLinesAndChars()
    {
        var block = ShowText();
        var split = PrestoAvalonia.SplitText(block, new() { Lines = new(), Words = null, Chars = new() });

        Assert.True(split.Lines.Count > 1);
        Assert.Equal(split.Lines.Count, Root(block).Children.Count);
        Assert.Empty(split.Words);
        Assert.Equal(Sentence.Replace(" ", ""), string.Concat(split.Chars.Select(TextOf)));
    }

    [AvaloniaFact]
    public void SplitsLinesWordsAndChars()
    {
        var block = ShowText();
        var split = PrestoAvalonia.SplitText(block, new() { Lines = new(), Chars = new() });

        Assert.True(split.Lines.Count > 1);
        Assert.Equal(split.Lines.Count, Root(block).Children.Count);
        Assert.Equal(SentenceWords, split.Words.Select(TextOf));
        Assert.Equal(Sentence.Replace(" ", ""), string.Concat(split.Chars.Select(TextOf)));
        Assert.Equal(Sentence, string.Join(" ", split.Lines.Select(TextOf)));
    }

    [AvaloniaFact]
    public void IncludeSpacesKeepsTheFollowingSpaceInsideThePiece()
    {
        var split = PrestoAvalonia.SplitText(ShowText(), new() { Lines = new(), Chars = new(), IncludeSpaces = true });

        // Every space but the ones a line break swallowed is now the last character of the piece in front of it.
        Assert.Equal(Sentence.Replace(' ', ' '), string.Join(' ', split.Lines.Select(TextOf)));
        Assert.Equal(SentenceWords.Length, split.Words.Count);
        Assert.Equal(Sentence.Replace(" ", "").Length, split.Chars.Count);
    }

    [AvaloniaFact]
    public void AppliesTheCustomStyleClasses()
    {
        var split = PrestoAvalonia.SplitText(ShowText(), new()
        {
            Lines = new() { Class = "split-line" },
            Words = new() { Class = "split-word" },
            Chars = new() { Class = "split-char" },
        });

        Assert.All(split.Lines, line => Assert.Contains("split-line", line.Classes));
        Assert.All(split.Words, word => Assert.Contains("split-word", word.Classes));
        Assert.All(split.Chars, character => Assert.Contains("split-char", character.Classes));
    }

    [AvaloniaFact]
    public void WrapsEachPieceInAClippingContainer()
    {
        var split = PrestoAvalonia.SplitText(ShowText(), new()
        {
            Lines = new() { Wrap = true },
            Words = new() { Wrap = true },
            Chars = new() { Wrap = true },
        });

        foreach (var piece in split.Lines.Concat(split.Words).Concat(split.Chars))
        {
            Assert.True(Assert.IsType<Border>(piece.Parent).ClipToBounds);
        }
    }

    [AvaloniaFact]
    public void EveryPieceKnowsItsLineInsideAnEffect()
    {
        var block = ShowText(160);
        var checkedPieces = 0;

        PrestoAvalonia.SplitText(block, new() { Lines = new(), Chars = new() }).AddEffect(split =>
        {
            Assert.True(split.Lines.Count > 2);
            for (var line = 0; line < split.Lines.Count; line++)
            {
                var row = split.Lines[line];
                Assert.Equal(line, TextSplitter.GetLineIndex(row));
                var words = row.GetLogicalDescendants().OfType<Control>().Where(split.Words.Contains).ToArray();
                Assert.Equal(words, split.Words.Where(word => TextSplitter.GetLineIndex(word) == line));
                Assert.Equal(TextOf(row).Replace(" ", ""), string.Concat(split.Chars.Where(c => TextSplitter.GetLineIndex(c) == line).Select(TextOf)));
                checkedPieces += words.Length;
            }
            return (Action?)null;
        });

        Assert.Equal(SentenceWords.Length, checkedPieces);
    }

    [AvaloniaFact]
    public void MeasuresTheLineHeightBeforeThePiecesAreLaidOut()
    {
        var probe = new TextBlock { Text = "Ag", FontSize = 20 };
        probe.Measure(Avalonia.Size.Infinity);
        var lineHeight = 0.0;

        PrestoAvalonia.SplitText(ShowText(160), new() { Lines = new() }).AddEffect(split =>
        {
            lineHeight = split.LineHeight;
            return (Action?)null;
        });

        Assert.True(lineHeight > 0);
        Assert.Equal(probe.DesiredSize.Height, lineHeight, tolerance: 1);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SplittingKeepsTheHostTheHeightOfItsPlainText(bool chars)
    {
        var block = ShowText(160);
        block.VerticalAlignment = VerticalAlignment.Top;
        block.UpdateLayout();
        var plain = block.Bounds.Height;

        PrestoAvalonia.SplitText(block, new() { Lines = new(), Chars = chars ? new() : null });
        block.UpdateLayout();

        Assert.Equal(plain, block.Bounds.Height, tolerance: 0.5);
    }

    private static double DelayOf(FunctionValue delay, Control piece) => (double)delay(piece, 0, 0).Raw!;

    [AvaloniaFact]
    public void StaggerByLineStepsByLineAndByPositionInTheLine()
    {
        var split = PrestoAvalonia.SplitText(ShowText(160));
        var thirdLine = split.Words.Where(word => TextSplitter.GetLineIndex(word) == 2).ToArray();
        Assert.True(thirdLine.Length > 1);

        var delay = TextSplitter.StaggerByLine(100, 20);

        Assert.Equal(200, DelayOf(delay, thirdLine[0]));
        Assert.Equal(220, DelayOf(delay, thirdLine[1]));
    }

    [AvaloniaFact]
    public void StaggerByLineStartOffsetsEveryDelay()
    {
        var split = PrestoAvalonia.SplitText(ShowText(160), new() { Words = null, Chars = new() });
        var plain = TextSplitter.StaggerByLine(100, 20);
        var offset = TextSplitter.StaggerByLine(100, 20, 500);

        Assert.All(split.Chars, c => Assert.Equal(DelayOf(plain, c) + 500, DelayOf(offset, c)));
    }

    [AvaloniaFact]
    public void LineHeightOfReadsTheOwningSplitAndFollowsARefresh()
    {
        var block = ShowText(160);
        var split = PrestoAvalonia.SplitText(block, new() { Lines = new(), Chars = new() });

        Assert.All(split.Lines.Concat(split.Words).Concat(split.Chars), piece => Assert.Equal(split.LineHeight, TextSplitter.LineHeightOf(piece)));

        var before = split.LineHeight;
        block.FontSize = 40;
        split.Refresh();

        Assert.True(split.LineHeight > before);
        Assert.Equal(split.LineHeight, TextSplitter.LineHeightOf(split.Words[0]));
        Assert.Equal(0, TextSplitter.LineHeightOf(block));
    }

    [AvaloniaFact]
    public void SlotOfGivesEachPiecesLayoutBoxInBlockCoordinates()
    {
        var block = ShowText(160);
        block.Padding = new Thickness(12, 7);
        var split = PrestoAvalonia.SplitText(block, new() { Lines = new() });
        block.UpdateLayout();

        var first = split.SlotOf(split.Words[0]);
        var space = ((Panel)split.Words[0].GetVisualParent()!).Children[1];
        Assert.Equal(12, first.X, 0.01);
        Assert.Equal(7, first.Y, 0.01);
        Assert.Equal(split.Words[0].Bounds.Width, first.Width);
        Assert.Equal(first.Right + space.Bounds.Width, split.SlotOf(split.Words[1]).X, 0.01);
        Assert.Equal(7 + split.LineHeight, split.SlotOf(split.Lines[1]).Y, 0.5);
        Assert.All(split.Words, word => Assert.Equal(word.TranslatePoint(default, block)!.Value, split.SlotOf(word).Position));
    }

    [AvaloniaFact]
    public void SlotOfIsValidInsideTheFirstEffect()
    {
        var block = ShowText(160);
        block.Padding = new Thickness(12, 7);
        Rect[] early = [];

        var split = PrestoAvalonia.SplitText(block, new() { Lines = new(), Chars = new() }).AddEffect(s =>
        {
            early = s.Lines.Concat(s.Words).Concat(s.Chars).Select(s.SlotOf).ToArray();
            return (Action?)null;
        });
        block.UpdateLayout();
        var laidOut = split.Lines.Concat(split.Words).Concat(split.Chars).Select(split.SlotOf).ToArray();

        Assert.True(early.Length > 1);
        Assert.Equal(laidOut.Length, early.Length);
        for (var i = 0; i < early.Length; i++)
        {
            Assert.Equal(laidOut[i].X, early[i].X, 0.5);
            Assert.Equal(laidOut[i].Y, early[i].Y, 0.5);
            Assert.Equal(laidOut[i].Width, early[i].Width, 0.5);
            Assert.Equal(laidOut[i].Height, early[i].Height, 0.5);
        }
    }

    [AvaloniaFact]
    public void SlotOfIsValidInsideTheFirstEffectOnACenteredBlock()
    {
        var block = ShowText();
        block.Text = "Hi there";
        block.TextAlignment = TextAlignment.Center;
        block.Padding = new Thickness(12, 7);
        Dispatcher.UIThread.RunJobs();
        Rect[] early = [];

        var split = PrestoAvalonia.SplitText(block, new() { Chars = new() }).AddEffect(s =>
        {
            early = s.Words.Concat(s.Chars).Select(s.SlotOf).ToArray();
            return (Action?)null;
        });
        block.UpdateLayout();
        var laidOut = split.Words.Concat(split.Chars).Select(split.SlotOf).ToArray();

        Assert.True(laidOut[0].X > 40, "the text was not centered");
        Assert.Equal(laidOut.Length, early.Length);
        for (var i = 0; i < early.Length; i++)
        {
            Assert.Equal(laidOut[i].X, early[i].X, 0.5);
            Assert.Equal(laidOut[i].Y, early[i].Y, 0.5);
        }
    }

    [AvaloniaFact]
    public void SlotOfIgnoresRenderTransforms()
    {
        var block = ShowText();
        var split = PrestoAvalonia.SplitText(block, new() { Lines = new(), Chars = new() });
        var word = split.Words[1];
        var before = split.SlotOf(word);

        split.Lines[0].RenderTransform = new TranslateTransform(40, 30);
        word.RenderTransform = new TransformGroup { Children = { new ScaleTransform(2, 2), new TranslateTransform(25, -10) } };
        block.UpdateLayout();

        Assert.Equal(before, split.SlotOf(word));
    }

    [AvaloniaFact]
    public void SlotOfFollowsAResplit()
    {
        var block = ShowText();
        var slots = new List<Rect[]>();
        var split = PrestoAvalonia.SplitText(block).AddEffect(s =>
        {
            slots.Add(s.Words.Select(s.SlotOf).ToArray());
            return (Action?)null;
        });
        var old = split.Words[^1];

        block.Width = 120;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);

        Assert.Equal(2, slots.Count);
        Assert.True(slots[1][^1].Y > slots[0][^1].Y);
        Assert.Throws<ArgumentException>(() => split.SlotOf(old));
    }

    [AvaloniaFact]
    public void SlotOfRejectsAControlThatIsNotAPieceOfThisSplit()
    {
        var split = PrestoAvalonia.SplitText(ShowText());
        var other = PrestoAvalonia.SplitText(ShowText());

        Assert.Throws<ArgumentException>(() => split.SlotOf(new TextBlock { Text = "The" }));
        Assert.Throws<ArgumentException>(() => split.SlotOf(other.Words[0]));
    }

    [AvaloniaFact]
    public void OutlinePiecesArePathsOnTheSlotsOfTextPieces()
    {
        var plain = PrestoAvalonia.SplitText(ShowText(), new() { Lines = new(), Chars = new() });
        var block = ShowText();
        var split = PrestoAvalonia.SplitText(block, new() { Lines = new(), Chars = new(), Outlines = true });

        Assert.Equal(plain.Chars.Count, split.Chars.Count);
        for (var i = 0; i < split.Chars.Count; i++)
        {
            var piece = Assert.IsType<Path>(split.Chars[i]);
            var expected = plain.SlotOf(plain.Chars[i]);
            var slot = split.SlotOf(piece);
            Assert.Equal(expected.X, slot.X, 0.01);
            Assert.Equal(expected.Y, slot.Y, 0.01);
            Assert.Equal(expected.Width, slot.Width, 0.01);
            Assert.Equal(expected.Height, slot.Height, 0.01);
            Assert.Equal(PrestoAvalonia.GlyphOutline(block, i).Bounds, piece.Data!.Bounds);
            Assert.Equal(TextSplitter.GraphemeOf(plain.Chars[i]), TextSplitter.GraphemeOf(piece));
        }
    }

    [AvaloniaFact]
    public void OutlinePiecesAreFilledWithTheBlocksForeground()
    {
        var block = ShowText();
        block.Foreground = Brushes.Crimson;

        var split = PrestoAvalonia.SplitText(block, new() { Words = null, Chars = new(), Outlines = true });

        Assert.All(split.Chars, piece => Assert.Same(Brushes.Crimson, Assert.IsType<Path>(piece).Fill));
    }

    [AvaloniaFact]
    public void AResplitKeepsTheOutlinePieces()
    {
        var block = ShowText();
        var split = PrestoAvalonia.SplitText(block, new() { Lines = new(), Chars = new(), Outlines = true });
        var lines = split.Lines.Count;

        block.Width = 120;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);

        Assert.True(split.Lines.Count > lines);
        Assert.All(split.Chars, piece => Assert.IsType<Path>(piece));
    }

    [AvaloniaFact]
    public void OutlinesWithoutCharsThrows()
    {
        var block = ShowText();

        Assert.Throws<ArgumentException>(() => PrestoAvalonia.SplitText(block, new() { Outlines = true }));
    }

    [AvaloniaFact]
    public void ACharacterWithoutAnOutlineStaysATextPiece()
    {
        var block = ShowText();
        block.Text = "a😀b";
        Dispatcher.UIThread.RunJobs();

        var split = PrestoAvalonia.SplitText(block, new() { Words = null, Chars = new(), Outlines = true });

        Assert.IsType<Path>(split.Chars[0]);
        Assert.Equal("😀", Assert.IsType<TextBlock>(split.Chars[1]).Text);
        Assert.IsType<Path>(split.Chars[2]);
        Assert.Equal(["a", "😀", "b"], split.Chars.Select(TextSplitter.GraphemeOf));
    }

    [AvaloniaFact]
    public void TurnsOffTheHostClipWhileSplitAndRestoresItOnRevert()
    {
        var block = ShowText();
        Assert.True(block.ClipToBounds);

        var split = PrestoAvalonia.SplitText(block);
        Assert.False(block.ClipToBounds);

        split.Revert();
        Assert.True(block.ClipToBounds);
        Assert.False(block.IsSet(Avalonia.Visual.ClipToBoundsProperty));
    }

    [AvaloniaFact]
    public void AHostThatDidNotClipStillDoesNotAfterRevert()
    {
        var block = ShowText();
        block.ClipToBounds = false;

        PrestoAvalonia.SplitText(block).Revert();

        Assert.False(block.ClipToBounds);
    }

    [AvaloniaFact]
    public void EffectRunsOnceAfterTheSplit()
    {
        var calls = 0;
        var cleanups = 0;

        PrestoAvalonia.SplitText(ShowText(), new() { Lines = new() }).AddEffect(_ =>
        {
            calls++;
            return () => cleanups++;
        });
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);

        Assert.Equal(1, calls);
        Assert.Equal(0, cleanups);
    }

    [AvaloniaFact]
    public void EffectRunsAgainWhenTheWidthChanges()
    {
        var block = ShowText();
        var calls = 0;
        var cleanups = 0;

        var split = PrestoAvalonia.SplitText(block, new() { Lines = new() }).AddEffect(_ =>
        {
            calls++;
            return () => cleanups++;
        });
        var lines = split.Lines.Count;

        block.Width = 120;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);

        Assert.Equal(2, calls);
        Assert.Equal(1, cleanups);
        Assert.True(split.Lines.Count > lines);

        block.Width = 400;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);

        Assert.Equal(3, calls);
        Assert.Equal(2, cleanups);
        Assert.True(split.Lines.Count < lines);
    }

    [AvaloniaFact]
    public void ResizeResplitsWithinTheDebounceInSecondsMode()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var block = ShowText();
        var calls = 0;
        PrestoAvalonia.SplitText(block, new() { Lines = new() }).AddEffect(_ =>
        {
            calls++;
            return () => { };
        });

        block.Width = 120;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);

        Assert.Equal(2, calls);
    }

    [AvaloniaFact]
    public void ASizeToContentBlockWidenedByItsOwnSplitIsNotSplitAgain()
    {
        var block = new TextBlock { Text = "AVATAR To WAVY", FontSize = 68, LetterSpacing = 12, HorizontalAlignment = HorizontalAlignment.Left };
        new Window { Content = new Decorator { Child = block }, Width = 1200, Height = 400 }.Show();
        Dispatcher.UIThread.RunJobs();
        var width = block.Bounds.Width;
        var calls = 0;
        var split = PrestoAvalonia.SplitText(block, new() { Words = null, Chars = new() }).AddEffect(_ =>
        {
            calls++;
            return () => { };
        });
        var first = split.Chars[0];

        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);

        Assert.NotEqual(width, block.Bounds.Width);
        Assert.Equal(1, calls);
        Assert.Same(first, split.Chars[0]);
    }

    [AvaloniaFact]
    public void CleanupSeesThePiecesItsEffectWasGiven()
    {
        var block = ShowText();
        TextSplitter? splitter = null;
        var cleanups = 0;

        splitter = PrestoAvalonia.SplitText(block, new() { Lines = new() }).AddEffect(split =>
        {
            var pieces = split.Words.ToArray();
            return () =>
            {
                Assert.Equal(pieces, splitter!.Words);
                cleanups++;
            };
        });

        Assert.Equal(0, cleanups);

        block.Width = 120;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);

        Assert.Equal(1, cleanups);
    }

    [AvaloniaFact]
    public void RefreshRunsTheCleanupsAndTheEffectsAgain()
    {
        var calls = 0;
        var cleanups = 0;

        var split = PrestoAvalonia.SplitText(ShowText(), new() { Lines = new() }).AddEffect(_ =>
        {
            calls++;
            return () => cleanups++;
        });
        split.Refresh();

        Assert.Equal(2, calls);
        Assert.Equal(1, cleanups);
    }

    [AvaloniaFact]
    public void RevertRunsTheCleanupsAndStopsRespondingToResizes()
    {
        var block = ShowText();
        var calls = 0;
        var cleanups = 0;

        var split = PrestoAvalonia.SplitText(block, new() { Lines = new() }).AddEffect(_ =>
        {
            calls++;
            return () => cleanups++;
        });
        split.Revert();

        Assert.Equal(1, calls);
        Assert.Equal(1, cleanups);

        block.Width = 120;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);

        Assert.Equal(1, calls);
        Assert.Equal(1, cleanups);
    }

    /// <summary>Upstream "addEffect() should properly track the time of an animation".</summary>
    [AvaloniaFact]
    public void TheAnimationAnEffectReturnsKeepsItsTimeAcrossARespilt()
    {
        var block = ShowText();
        Animation? animation = null;

        PrestoAvalonia.SplitText(block, new() { Lines = new() }).AddEffect(split =>
        {
            var created = Presto.Animate(split.Lines, new AnimationParams { ["Opacity"] = 0, Duration = 2000 });
            animation = created;
            return created;
        });

        Frames.Advance(500, 50);
        var first = animation!;
        var elapsed = first.CurrentTime;
        Assert.True(elapsed > 0);

        block.Width = 120;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);

        Assert.NotSame(first, animation);
        Assert.True(animation!.CurrentTime > elapsed);
    }

    /// <summary>An effect's animation is reverted with the splitter, where a re-split leaves it to the next run.</summary>
    [AvaloniaFact]
    public void RevertRevertsTheAnimationAnEffectReturned()
    {
        var block = ShowText();
        Animation? animation = null;

        var split = PrestoAvalonia.SplitText(block, new() { Chars = new() }).AddEffect(s =>
        {
            var created = Presto.Animate(s.Chars, new AnimationParams { ["Opacity"] = 0, Duration = 200 });
            animation = created;
            return created;
        });

        Frames.Advance(100, 16);
        Assert.False(animation!.Paused);

        split.Revert();
        Assert.True(animation.Canceled);
    }

    [AvaloniaFact]
    public void RevertRestoresTheOriginalTextBlock()
    {
        var block = ShowText();
        var split = PrestoAvalonia.SplitText(block, new() { Lines = new(), Chars = new() });
        Dispatcher.UIThread.RunJobs();
        Assert.NotEmpty(block.GetVisualChildren());

        split.Revert();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Sentence, block.Text);
        Assert.Empty(block.GetVisualChildren());
        Assert.Empty(split.Lines);
        Assert.Empty(split.Words);
        Assert.Empty(split.Chars);
    }
}
