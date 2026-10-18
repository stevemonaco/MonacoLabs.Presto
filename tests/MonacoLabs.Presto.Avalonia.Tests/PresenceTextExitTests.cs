using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Text;

namespace MonacoLabs.Presto.Tests;

/// <summary>A leaving <see cref="TextBlock"/> with <see cref="TextReveal.PiecesProperty"/> exits as one ghost per piece, and a hidden one on its live pieces (GAPS-2-PLAN §4).</summary>
public sealed class PresenceTextExitTests : BridgeTestBase
{
    private static readonly AnimationBuilder Long = Presto.Build().Opacity(to: 0).Duration(2000).Ease("linear");

    private static Canvas[] Hosts(Window window) => OverlayLayer.GetOverlayLayer(window)!.Children.OfType<Canvas>().ToArray();

    private static Image[] Ghosts(Window window) => Assert.Single(Hosts(window)).GetVisualDescendants().OfType<Image>().ToArray();

    private static SplitTextParams Split(TextPieces pieces, bool mask = false) => pieces switch
    {
        TextPieces.Lines => new() { Lines = new() { Wrap = mask }, Words = null },
        TextPieces.Words => new(),
        _ => new() { Words = null, Chars = new() },
    };

    private static IReadOnlyList<Control> Pieces(TextSplitter split, TextPieces pieces) => pieces switch
    {
        TextPieces.Lines => split.Lines,
        TextPieces.Words => split.Words,
        _ => split.Chars,
    };

    private static Window Leave(TextBlock block, AnimationBuilder exit, Action<Window>? before = null)
    {
        Presence.SetExit(block, exit);
        var window = TextRevealTests.Show(block);
        before?.Invoke(window);
        ((Panel)window.Content!).Children.Remove(block);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Rect WindowRect(Visual visual, Window window) => new(visual.TranslatePoint(default, window)!.Value, visual.Bounds.Size);

    [AvaloniaTheory]
    [InlineData(TextPieces.Chars)]
    [InlineData(TextPieces.Words)]
    [InlineData(TextPieces.Lines)]
    public void EachGhostStandsInItsPiecesSlot(TextPieces pieces)
    {
        var block = TextRevealTests.Block(pieces);
        AssertGhostsInSlots(block, pieces);
        Assert.Equal(TextRevealTests.Sentence, block.Text);
        Assert.Null(block.Inlines);
    }

    [AvaloniaFact]
    public void EachGhostStandsInItsPiecesSlotOnACenteredBlock()
    {
        var block = TextRevealTests.Block(TextPieces.Chars);
        block.Text = "Hi there";
        block.TextAlignment = TextAlignment.Center;

        var ghosts = AssertGhostsInSlots(block, TextPieces.Chars);

        Assert.True(ghosts[0].X > 40, "the text was not centered");
    }

    private static Point[] AssertGhostsInSlots(TextBlock block, TextPieces pieces)
    {
        Point[] slots = [];
        var window = Leave(block, Long, window =>
        {
            var split = PrestoAvalonia.SplitText(block, Split(pieces));
            Dispatcher.UIThread.RunJobs();
            slots = Pieces(split, pieces).Select(piece => piece.TranslatePoint(default, window)!.Value).ToArray();
            split.Revert();
            Dispatcher.UIThread.RunJobs();
        });

        var ghosts = Ghosts(window).Select(ghost => ghost.TranslatePoint(default, window)!.Value).ToArray();
        Assert.True(slots.Length > 1);
        Assert.Equal(slots.Length, ghosts.Length);
        for (var i = 0; i < slots.Length; i++)
        {
            Assert.Equal(slots[i].X, ghosts[i].X, 0.5);
            Assert.Equal(slots[i].Y, ghosts[i].Y, 0.5);
        }
        return ghosts;
    }

    private (Image[] Ghosts, double[] Started) Starts(TweenValue delay) =>
        Starts(TextPieces.Words, Presto.Build().Opacity(to: 0).Duration(100).Ease("linear").Delay(delay));

    private (Image[] Ghosts, double[] Started) Starts(TextPieces pieces, AnimationBuilder exit)
    {
        var window = Leave(TextRevealTests.Block(pieces), exit);
        var ghosts = Ghosts(window);
        var started = new double[ghosts.Length];
        Array.Fill(started, double.NaN);
        for (var time = 0; time < 2000; time += 5)
        {
            Frames.Advance(5, 5);
            for (var i = 0; i < ghosts.Length; i++)
            {
                if (double.IsNaN(started[i]) && ghosts[i].Opacity < 1) started[i] = time;
            }
        }
        return (ghosts, started);
    }

    [AvaloniaFact]
    public void AnIndexStaggerStartsTheGhostsInReadingOrder()
    {
        var (ghosts, started) = Starts(Presto.Stagger(50));

        Assert.Equal(TextRevealTests.Sentence.Split(' ').Length, ghosts.Length);
        for (var i = 1; i < started.Length; i++) Assert.True(started[i] > started[i - 1]);
    }

    [AvaloniaFact]
    public void StaggerByLineStartsEveryGhostOfALineAfterEveryGhostOfTheLineBefore()
    {
        var (ghosts, started) = Starts(TextSplitter.StaggerByLine(200, 20));

        double Line(int line, Func<IEnumerable<double>, double> pick) =>
            pick(started.Where((_, i) => TextSplitter.GetLineIndex(ghosts[i]) == line));
        Assert.True(ghosts.Count(ghost => TextSplitter.GetLineIndex(ghost) == 0) > 1);
        Assert.True(Line(1, Enumerable.Min) > Line(0, Enumerable.Max));
    }

    private static TextBlock ExitBlock(string recipe)
    {
        var block = TextRevealTests.Block(recipe switch
        {
            nameof(Recipes.WordFadeOut) or nameof(Recipes.WordBlurOut) => TextPieces.Words,
            nameof(Recipes.CharCascadeOut) => TextPieces.Chars,
            _ => TextPieces.Lines,
        });
        TextReveal.SetMask(block, recipe == nameof(Recipes.LineMaskOut));
        return block;
    }

    private static AnimationBuilder TextExit(string recipe) => recipe switch
    {
        nameof(Recipes.LineRiseOut) => Recipes.LineRiseOut(),
        nameof(Recipes.LineMaskOut) => Recipes.LineMaskOut(),
        nameof(Recipes.LineWipeOut) => Recipes.LineWipeOut(),
        nameof(Recipes.WordFadeOut) => Recipes.WordFadeOut(),
        nameof(Recipes.WordBlurOut) => Recipes.WordBlurOut(),
        _ => Recipes.CharCascadeOut(),
    };

    private static double Value(Visual ghost, string property) => (double)PrestoAvalonia.Get(ghost, property)!;

    [AvaloniaTheory]
    [InlineData(nameof(Recipes.LineRiseOut))]
    [InlineData(nameof(Recipes.LineMaskOut))]
    [InlineData(nameof(Recipes.LineWipeOut))]
    [InlineData(nameof(Recipes.WordFadeOut))]
    [InlineData(nameof(Recipes.WordBlurOut))]
    [InlineData(nameof(Recipes.CharCascadeOut))]
    public void TextExitRecipesAnimateEveryGhostToItsEndAndDismissTheHost(string recipe)
    {
        Image[] ghosts = [];
        var ended = false;
        var window = Leave(ExitBlock(recipe), TextExit(recipe).OnComplete(_ =>
        {
            ended = true;
            var tokens = Recipes.Tokens;
            Assert.All(ghosts, ghost =>
            {
                var lineHeight = TextSplitter.LineHeightOf(ghost);
                switch (recipe)
                {
                    case nameof(Recipes.LineRiseOut):
                        Assert.Equal(0, ghost.Opacity);
                        Assert.Equal(-lineHeight, Value(ghost, "Y"), 6);
                        break;
                    case nameof(Recipes.LineMaskOut):
                        Assert.Equal(1, ghost.Opacity);
                        Assert.Equal(-lineHeight, Value(ghost, "Y"), 6);
                        Assert.True(Assert.IsType<Canvas>(ghost.GetVisualParent()).ClipToBounds);
                        break;
                    case nameof(Recipes.LineWipeOut):
                        Assert.Equal(0, Assert.IsType<RectangleGeometry>(ghost.Clip).Rect.Width, 6);
                        break;
                    case nameof(Recipes.WordFadeOut):
                        Assert.Equal(0, ghost.Opacity);
                        Assert.Equal(-tokens.Distance, Value(ghost, "Y"), 6);
                        break;
                    case nameof(Recipes.WordBlurOut):
                        Assert.Equal(0, ghost.Opacity);
                        Assert.Equal(tokens.BlurRadius, Value(ghost, "Blur"), 6);
                        break;
                    default:
                        Assert.Equal(0, ghost.Opacity);
                        Assert.Equal(tokens.Distance, Value(ghost, "Y"), 6);
                        break;
                }
            });
        }));
        ghosts = Ghosts(window);
        Assert.True(ghosts.Length > 1);

        Frames.Advance(5000, 16);

        Assert.True(ended);
        Assert.Empty(Hosts(window));
    }

    [AvaloniaFact]
    public void AScatterExitFliesOneGhostPerCharacter()
    {
        Image[] ghosts = [];
        var ended = false;
        var window = Leave(TextRevealTests.Block(TextPieces.Chars), Recipes.Scatter(seed: 1).OnComplete(_ =>
        {
            ended = true;
            Assert.All(ghosts, ghost =>
            {
                Assert.Equal(0, ghost.Opacity);
                Assert.Equal(0.6, Value(ghost, "Scale"), 6);
            });
            Assert.True(ghosts.Select(ghost => Value(ghost, "X")).Distinct().Count() > 1);
        }));
        ghosts = Ghosts(window);
        Assert.Equal(TextRevealTests.Sentence.Count(c => c != ' '), ghosts.Length);

        Frames.Advance(5000, 16);

        Assert.True(ended);
        Assert.Empty(Hosts(window));
    }

    [AvaloniaFact]
    public void CharCascadeOutStartsEachLinesGhostsAfterTheLineBefore()
    {
        var (ghosts, started) = Starts(TextPieces.Chars, Recipes.CharCascadeOut());

        var lineStarts = started.Select((time, i) => (Line: TextSplitter.GetLineIndex(ghosts[i]), Time: time))
            .GroupBy(start => start.Line, start => start.Time).OrderBy(line => line.Key).Select(line => line.Min()).ToArray();
        Assert.True(lineStarts.Length > 1);
        Assert.All(lineStarts.Zip(lineStarts.Skip(1)), pair => Assert.True(pair.Second > pair.First));
    }

    [AvaloniaTheory]
    [InlineData(nameof(Recipes.LineRiseOut))]
    [InlineData(nameof(Recipes.LineMaskOut))]
    [InlineData(nameof(Recipes.LineWipeOut))]
    [InlineData(nameof(Recipes.WordFadeOut))]
    [InlineData(nameof(Recipes.WordBlurOut))]
    [InlineData(nameof(Recipes.CharCascadeOut))]
    public void ReducedTextExitRecipesFadeOneWholeGhostOverShort(string recipe)
    {
        Presto.Defaults.ReducedMotion = true;
        var block = ExitBlock(recipe);

        var window = Leave(block, TextExit(recipe));

        var ghost = Assert.Single(Ghosts(window));
        Assert.Equal(block.Bounds.Size, ghost.Bounds.Size);
        var fade = Assert.Single(Presto.GetAnimations(ghost, "Opacity"));
        Assert.Equal(Recipes.Tokens.Short, fade.Duration, 6);
    }

    [AvaloniaFact]
    public void LineHeightOfAGhostIsTheBlocksLineHeight()
    {
        var block = TextRevealTests.Block(TextPieces.Words);
        double lineHeight = 0;
        var window = Leave(block, Long, _ =>
        {
            var split = PrestoAvalonia.SplitText(block);
            lineHeight = split.LineHeight;
            split.Revert();
        });

        Assert.True(lineHeight > 0);
        Assert.All(Ghosts(window), ghost => Assert.Equal(lineHeight, TextSplitter.LineHeightOf(ghost)));
    }

    [AvaloniaFact]
    public void OverTheCapGhostsCoarsenFromCharsToWordsToLinesToOneWholeGhost()
    {
        var block = TextRevealTests.Block(TextPieces.Chars);
        var window = TextRevealTests.Show(block);
        var split = PrestoAvalonia.SplitText(block, Split(TextPieces.Lines));
        var lines = split.Lines.Count;
        split.Revert();
        var words = TextRevealTests.Sentence.Split(' ').Length;
        var max = PieceGhosts.Max;

        int GhostsAt(int cap)
        {
            PieceGhosts.Max = cap;
            var exit = PrestoAvalonia.AnimateExit(block, Long)!;
            var count = Ghosts(window).Length;
            exit.Cancel();
            Assert.Empty(Hosts(window));
            return count;
        }

        try
        {
            Assert.Equal(TextRevealTests.Sentence.Count(c => c != ' '), GhostsAt(max));
            Assert.Equal(words, GhostsAt(words));
            Assert.Equal(lines, GhostsAt(words - 1));
            Assert.Equal(1, GhostsAt(lines - 1));
            Assert.Equal(TextRevealTests.Sentence, block.Text);
        }
        finally
        {
            PieceGhosts.Max = max;
        }
    }

    [AvaloniaFact]
    public void TheHostIsDismissedWhenTheLastGhostEnds()
    {
        var window = Leave(TextRevealTests.Block(TextPieces.Words), Presto.Build().Opacity(to: 0).Duration(100).Ease("linear").Delay(Presto.Stagger(50)));
        var last = 100 + (50 * (Ghosts(window).Length - 1));

        Frames.Advance(last - 30, 10);
        Assert.Single(Hosts(window));

        Frames.Advance(60, 10);
        Assert.Empty(Hosts(window));
    }

    [AvaloniaFact]
    public void ReduceAllLeavesNoGhost()
    {
        Presto.Defaults.ReduceAll = true;

        var window = Leave(TextRevealTests.Block(TextPieces.Chars), Long);

        Assert.Empty(Hosts(window));
    }

    [AvaloniaFact]
    public void AMaskedLineGhostIsClippedToItsLineBox()
    {
        var block = TextRevealTests.Block(TextPieces.Lines);
        TextReveal.SetMask(block, true);
        Rect[] boxes = [];
        var window = Leave(block, Long, window =>
        {
            var split = PrestoAvalonia.SplitText(block, Split(TextPieces.Lines, mask: true));
            Dispatcher.UIThread.RunJobs();
            boxes = split.Lines.Select(line => WindowRect(Assert.IsType<Border>(line.GetVisualParent()), window)).ToArray();
            split.Revert();
            Dispatcher.UIThread.RunJobs();
        });

        var ghosts = Ghosts(window);
        Assert.Equal(boxes.Length, ghosts.Length);
        for (var i = 0; i < boxes.Length; i++)
        {
            var mask = Assert.IsType<Canvas>(ghosts[i].GetVisualParent());
            Assert.True(mask.ClipToBounds);
            // The overlay rounds layout to whole pixels and the split does not.
            var box = WindowRect(mask, window);
            Assert.Equal(boxes[i].X, box.X, 0.5);
            Assert.Equal(boxes[i].Y, box.Y, 0.5);
            Assert.Equal(boxes[i].Width, box.Width, 1d);
            Assert.Equal(boxes[i].Height, box.Height, 1d);
        }
    }

    [AvaloniaFact]
    public void LeavingMidRevealGhostsTheLivePiecesAsDrawn()
    {
        var block = TextRevealTests.Block(TextPieces.Words);
        Presence.SetInitial(block, Presto.Build().Y(from: 20).Duration(2000).Ease("linear"));

        var window = Leave(block, Long, _ => Frames.Advance(1000, 50));

        var ghosts = Ghosts(window);
        Assert.Equal(TextRevealTests.Sentence.Split(' ').Length, ghosts.Length);
        Assert.All(ghosts, ghost => Assert.Equal(10, ghost.RenderTransform!.Value.M32, 1d));
        Assert.Null(block.Inlines);
    }

    private static Window Hide(TextBlock block, AnimationBuilder exit, PresenceHideMode mode = PresenceHideMode.Hold)
    {
        Presence.SetExit(block, exit);
        Presence.SetHideMode(block, mode);
        var window = TextRevealTests.Show(block);
        block.IsVisible = false;
        return window;
    }

    private static void AssertWhole(TextBlock block)
    {
        Assert.Null(block.Inlines);
        Assert.Equal(TextRevealTests.Sentence, block.Text);
    }

    [AvaloniaFact]
    public void AHeldHidePlaysOnTheLiveCharsAndHidesWholeOnceItEnds()
    {
        var block = TextRevealTests.Block(TextPieces.Chars);
        var window = Hide(block, Long);
        Frames.Advance(1000, 50);

        var chars = TextSplitter.Of(block)!.Chars;
        Assert.Equal(TextRevealTests.Sentence.Count(c => c != ' '), chars.Count);
        Assert.All(chars, piece => Assert.InRange(piece.Opacity, .4, .6));
        Assert.Equal(1, block.Opacity);
        Assert.True(block.IsVisible);
        Assert.Empty(Hosts(window));

        Frames.Advance(1100, 50);

        Assert.False(block.IsVisible);
        AssertWhole(block);
    }

    [AvaloniaFact]
    public void AHeldHideMidArrivalRevertsTheArrivalsSplitAndExitsOnAFreshOne()
    {
        var block = TextRevealTests.Block(TextPieces.Chars);
        Presence.SetInitial(block, Presto.Build().Opacity(from: 0).Duration(2000).Ease("linear"));
        Presence.SetExit(block, Long);
        Presence.SetHideMode(block, PresenceHideMode.Hold);
        TextRevealTests.Show(block);
        Frames.Advance(500, 50);
        var arrivalPieces = TextSplitter.Of(block)!.Chars.ToArray();
        var arrival = Assert.Single(Presto.GetAnimations(arrivalPieces[0]));

        block.IsVisible = false;
        Frames.Advance(1000, 50);

        Assert.True(arrival.Canceled);
        var chars = TextSplitter.Of(block)!.Chars;
        Assert.Equal(arrivalPieces.Length, chars.Count);
        Assert.All(chars, piece => Assert.DoesNotContain(piece, arrivalPieces));
        Assert.All(chars, piece => Assert.InRange(piece.Opacity, .4, .6));
    }

    [AvaloniaFact]
    public void ShownAgainMidHeldHideThePieceExitPlaysBack()
    {
        var block = TextRevealTests.Block(TextPieces.Chars);
        Hide(block, Long);
        Frames.Advance(1000, 50);

        block.IsVisible = true;
        Frames.Advance(500, 50);

        Assert.All(TextSplitter.Of(block)!.Chars, piece => Assert.InRange(piece.Opacity, .65, .85));

        Frames.Advance(1000, 50);

        Assert.True(block.IsVisible);
        Assert.Equal(1, block.Opacity);
        AssertWhole(block);
    }

    [AvaloniaFact]
    public void AGhostHideCollapsesAtOnceAndGhostsPerPieceInOneHost()
    {
        var block = TextRevealTests.Block(TextPieces.Chars);
        var window = Hide(block, Long, PresenceHideMode.Ghost);

        Assert.False(block.IsVisible);
        Assert.Equal(TextRevealTests.Sentence.Count(c => c != ' '), Ghosts(window).Length);
        AssertWhole(block);
    }

    [AvaloniaFact]
    public void DetachingMidHeldHideReleasesTheHoldAndRevertsTheSplit()
    {
        var block = TextRevealTests.Block(TextPieces.Chars);
        var window = Hide(block, Long);
        Frames.Advance(500, 50);

        ((Panel)window.Content!).Children.Remove(block);
        Dispatcher.UIThread.RunJobs();

        Assert.False(block.IsVisible);
        AssertWhole(block);
        Assert.Empty(Hosts(window));
    }

    [AvaloniaFact]
    public void UnderReduceAllAHideIsInstantAndLeavesNoSplit()
    {
        Presto.Defaults.ReduceAll = true;
        var block = TextRevealTests.Block(TextPieces.Chars);

        Hide(block, Long);

        Assert.False(block.IsVisible);
        AssertWhole(block);
    }

    [AvaloniaFact]
    public void AReducedHeldHidePlaysOnTheUnsplitBlock()
    {
        Presto.Defaults.ReducedMotion = true;
        var block = TextRevealTests.Block(TextPieces.Chars);

        var window = Hide(block, Presto.Build().Y(to: 20).Duration(2000).WhenReduced(reduced => reduced.Opacity(to: 0).Duration(2000).Ease("linear")));
        Frames.Advance(1000, 50);

        Assert.True(block.IsVisible);
        Assert.InRange(block.Opacity, .4, .6);
        Assert.Null(TextSplitter.Of(block));
        Assert.Empty(Hosts(window));
    }
}
