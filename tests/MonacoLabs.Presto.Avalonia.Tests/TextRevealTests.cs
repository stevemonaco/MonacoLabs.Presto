using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Text;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="TextReveal"/>: the triggers play their builder on a split block's pieces instead of the block (PREBUILTS-2-PLAN §3.4).</summary>
public sealed class TextRevealTests : BridgeTestBase
{
    internal const string Sentence = "The quick brown fox jumps over the lazy dog near the river bank at dawn";

    private static readonly AnimationBuilder Long = Presto.Build().Opacity(from: 0).Duration(2000).Ease("linear");

    internal static TextBlock Block(TextPieces pieces) => new()
    {
        Text = Sentence, TextWrapping = TextWrapping.Wrap, FontSize = 20, Background = Brushes.Transparent,
        [TextReveal.PiecesProperty] = pieces,
    };

    internal static Window Show(TextBlock block)
    {
        var window = new Window { Content = new StackPanel { Children = { block } }, Width = 260, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static StackPanel Root(TextBlock block) =>
        Assert.IsType<StackPanel>(Assert.IsType<InlineUIContainer>(Assert.Single(block.Inlines!)).Child);

    private static TextBlock[] Words(TextBlock block) =>
        Root(block).GetVisualDescendants().OfType<TextBlock>().Where(piece => !string.IsNullOrWhiteSpace(piece.Text)).ToArray();

    private static void Tap(TextBlock block, Point? at = null)
    {
        // A second tap must land elsewhere, or it counts as a double tap and raises no Tapped.
        var window = (Window)TopLevel.GetTopLevel(block)!;
        var point = block.TranslatePoint(at ?? new Point(10, 10), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    [AvaloniaFact]
    public void InitialWordsAnimateEveryWordAndNotTheBlockWhichWaitsHiddenForTheSplit()
    {
        var block = Block(TextPieces.Words);
        Presence.SetInitial(block, Recipes.FadeUp(stagger: true));
        double? attachedOpacity = null;
        block.AttachedToVisualTree += (_, _) => attachedOpacity = block.Opacity;

        Show(block);

        Assert.Equal(0, attachedOpacity);
        Assert.Equal(1, block.Opacity);
        var words = Words(block);
        Assert.Equal(Sentence.Split(' ').Length, words.Length);
        Assert.All(words, word => Assert.NotEmpty(Presto.GetAnimations(word)));
        Assert.Empty(Presto.GetAnimations(block));
    }

    [AvaloniaFact]
    public void AResizeMidRevealResplitsAndKeepsTheTime()
    {
        var block = Block(TextPieces.Words);
        Presence.SetInitial(block, Long);
        Show(block);
        var first = Words(block)[0];
        var before = Assert.Single(Presto.GetAnimations(first));
        Frames.Advance(500, 50);
        var elapsed = before.CurrentTime;

        block.Width = 120;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(400, 16);

        Assert.True(before.Canceled);
        var after = Assert.Single(Presto.GetAnimations(Words(block)[0]));
        Assert.NotSame(first, Words(block)[0]);
        Assert.InRange(after.CurrentTime, elapsed + 350, elapsed + 450);
    }

    [AvaloniaFact]
    public void ACompletedRevealPutsTheTextBack()
    {
        var block = Block(TextPieces.Words);
        Presence.SetInitial(block, Presto.Build().Opacity(from: 0).Duration(100));
        Show(block);
        Assert.NotNull(block.Inlines);

        Frames.Advance(300, 16);

        Assert.Null(block.Inlines);
        Assert.Equal(Sentence, block.Text);
    }

    [AvaloniaFact]
    public void ABoundTextChangedMidRevealResplitsKeepingTheTimeAndEndsOnTheNewTextStillBound()
    {
        var source = new TextBlock { Text = Sentence };
        var block = Block(TextPieces.Words);
        block[!TextBlock.TextProperty] = source[!TextBlock.TextProperty];
        Presence.SetInitial(block, Long);
        Show(block);
        Frames.Advance(500, 50);
        var elapsed = Assert.Single(Presto.GetAnimations(Words(block)[0])).CurrentTime;

        source.Text = "Hello there";

        Assert.Equal(["Hello", "there"], Words(block).Select(word => word.Text));
        Assert.InRange(Assert.Single(Presto.GetAnimations(Words(block)[0])).CurrentTime, elapsed - 50, elapsed + 50);
        Frames.Advance(2000, 50);
        Assert.Null(block.Inlines);
        Assert.Equal("Hello there", block.Text);
        source.Text = "Later";
        Assert.Equal("Later", block.Text);
    }

    [AvaloniaFact]
    public void ABindingSurvivesARevealWithNoTextChange()
    {
        var source = new TextBlock { Text = Sentence };
        var block = Block(TextPieces.Words);
        block[!TextBlock.TextProperty] = source[!TextBlock.TextProperty];
        Presence.SetInitial(block, Recipes.WordFade());
        Show(block);
        Assert.NotNull(block.Inlines);

        Frames.Advance(5000, 50);
        Assert.Null(block.Inlines);
        source.Text = "Later";

        Assert.Equal("Later", block.Text);
    }

    [AvaloniaFact]
    public void MorphTextKeepsTheBlocksBinding()
    {
        var source = new TextBlock { Text = "banana" };
        var block = new TextBlock { FontSize = 20 };
        block[!TextBlock.TextProperty] = source[!TextBlock.TextProperty];
        Show(block);

        Recipes.MorphText(block, "nab").Complete();
        Assert.Equal("nab", block.Text);
        source.Text = "Later";

        Assert.Equal("Later", block.Text);
    }

    [AvaloniaFact]
    public void ALoopingRevealStaysSplit()
    {
        var block = Block(TextPieces.Words);
        Presence.SetInitial(block, Presto.Build().Opacity(from: 0).Duration(100).Loop());
        Show(block);

        Frames.Advance(300, 16);

        Assert.NotEmpty(Words(block));
    }

    [AvaloniaFact]
    public void MaskWrapsEachLineInAClippingBorder()
    {
        var block = Block(TextPieces.Lines);
        TextReveal.SetMask(block, true);
        Presence.SetInitial(block, Long);
        Show(block);

        var borders = Root(block).Children;
        Assert.True(borders.Count > 1);
        Assert.All(borders, child =>
        {
            var border = Assert.IsType<Border>(child);
            Assert.True(border.ClipToBounds);
            Assert.NotEmpty(Presto.GetAnimations(border.Child!));
        });
    }

    [AvaloniaFact]
    public void LineMaskRisesEachLineBehindItsClipToRest()
    {
        var block = Block(TextPieces.Lines);
        TextReveal.SetMask(block, true);
        double[]? ended = null;
        Presence.SetInitial(block, Recipes.LineMask().OnComplete(_ => ended = Root(block).Children
            .Select(border => (double)PrestoAvalonia.Get(((Border)border).Child!, "Y")!).ToArray()));
        Show(block);

        var first = ((Border)Root(block).Children[0]).Child!;
        Frames.Advance(50, 16);
        Assert.InRange((double)PrestoAvalonia.Get(first, "Y")!, 1, TextSplitter.LineHeightOf(first));

        Frames.Advance(2000, 16);

        Assert.NotNull(ended);
        Assert.All(ended, y => Assert.Equal(0, y));
        Assert.Null(block.Inlines);
    }

    [AvaloniaFact]
    public void LineWipeEndsWithNoClipOnAnyLine()
    {
        var block = Block(TextPieces.Lines);
        Control[]? lines = null;
        var clipped = true;
        Presence.SetInitial(block, Recipes.LineWipe().OnComplete(_ => clipped = lines!.Any(line => line.Clip != null)));
        Show(block);
        lines = Root(block).Children.ToArray();
        Frames.Advance(50, 16);
        Assert.NotNull(lines[0].Clip);

        Frames.Advance(2000, 16);

        Assert.False(clipped);
    }

    [AvaloniaTheory]
    [InlineData(TextPieces.Lines, nameof(Recipes.LineRise))]
    [InlineData(TextPieces.Lines, nameof(Recipes.LineMask))]
    [InlineData(TextPieces.Lines, nameof(Recipes.LineWipe))]
    [InlineData(TextPieces.Words, nameof(Recipes.WordFade))]
    [InlineData(TextPieces.Words, nameof(Recipes.WordBlur))]
    [InlineData(TextPieces.Chars, nameof(Recipes.CharCascade))]
    [InlineData(TextPieces.Words, nameof(Recipes.FadeUp))]
    public void ReducedTextRevealsFadeTheBlockUnsplit(TextPieces pieces, string recipe)
    {
        Presto.Defaults.ReducedMotion = true;
        var block = Block(pieces);
        TextReveal.SetMask(block, recipe == nameof(Recipes.LineMask));
        Presence.SetInitial(block, recipe switch
        {
            nameof(Recipes.LineRise) => Recipes.LineRise(),
            nameof(Recipes.LineMask) => Recipes.LineMask(),
            nameof(Recipes.LineWipe) => Recipes.LineWipe(),
            nameof(Recipes.WordFade) => Recipes.WordFade(),
            nameof(Recipes.WordBlur) => Recipes.WordBlur(),
            nameof(Recipes.FadeUp) => Recipes.FadeUp(stagger: true),
            _ => Recipes.CharCascade(),
        });
        Show(block);

        Assert.Equal(Sentence, block.Text);
        Assert.NotEmpty(Presto.GetAnimations(block, "Opacity"));
    }

    [AvaloniaFact]
    public void WordFadeStartsEachLineAfterEveryWordOfTheLineBefore()
    {
        var block = Block(TextPieces.Words);
        Presence.SetInitial(block, Recipes.WordFade());
        Show(block);
        var words = Words(block);
        var started = new double[words.Length];
        Array.Fill(started, double.NaN);

        for (var time = 0; time < 400; time += 5)
        {
            Frames.Advance(5, 5);
            for (var i = 0; i < words.Length; i++)
            {
                if (double.IsNaN(started[i]) && words[i].Opacity > 0) started[i] = time;
            }
        }

        double LineStart(int line, Func<IEnumerable<double>, double> pick) =>
            pick(started.Where((_, i) => TextSplitter.GetLineIndex(words[i]) == line));
        Assert.True(words.Count(word => TextSplitter.GetLineIndex(word) == 0) > 1);
        Assert.True(LineStart(1, Enumerable.Min) > LineStart(0, Enumerable.Max));
    }

    [AvaloniaFact]
    public void CharCascadeAnimatesEveryCharacter()
    {
        var block = Block(TextPieces.Chars);
        Presence.SetInitial(block, Recipes.CharCascade());
        Show(block);

        var chars = Words(block);
        Assert.Equal(Sentence.Count(c => c != ' '), chars.Length);
        Assert.All(chars, piece => Assert.NotEmpty(Presto.GetAnimations(piece, "Y")));
        Assert.Empty(Presto.GetAnimations(block));
    }

    [AvaloniaFact]
    public void AHoverStateStillAnimatesTheBlock()
    {
        var block = Block(TextPieces.Words);
        Motion.SetHover(block, Presto.Build().Scale(1.1).Duration(100));
        var window = Show(block);

        window.MouseMove(new Point(20, 10));
        Frames.Advance(200, 16);

        Assert.Equal(Sentence, block.Text);
        Assert.Equal(1.1, (double)PrestoAvalonia.Get(block, "Scale")!, 3);
    }

    [AvaloniaFact]
    public void OnTappedRevealsThePiecesAndATapMidRevealRestartsIt()
    {
        var block = Block(TextPieces.Words);
        Show(block);
        Animate.SetOnTapped(block, Long);

        Tap(block);
        var first = Words(block)[0];
        var reveal = Assert.Single(Presto.GetAnimations(first));
        Assert.Empty(Presto.GetAnimations(block));
        Frames.Advance(500, 50);

        Tap(block, new Point(60, 40));

        Assert.True(reveal.Canceled);
        var restarted = Words(block)[0];
        Assert.NotSame(first, restarted);
        Assert.InRange(Assert.Single(Presto.GetAnimations(restarted)).CurrentTime, 0, 50);
    }

    [AvaloniaFact]
    public void DetachingRevertsTheSplit()
    {
        var block = Block(TextPieces.Words);
        var panel = (StackPanel)Show(block).Content!;
        Animate.SetOnTapped(block, Long);
        Tap(block);
        var reveal = Assert.Single(Presto.GetAnimations(Words(block)[0]));

        panel.Children.Remove(block);

        Assert.True(reveal.Canceled);
        Assert.Null(block.Inlines);
        Assert.Equal(Sentence, block.Text);
    }

    [AvaloniaFact]
    public void DetachingBeforeTheSplitReleasesTheHold()
    {
        var panel = (StackPanel)Show(new TextBlock()).Content!;
        var block = Block(TextPieces.Words);
        Animate.SetOnAttached(block, Long);

        panel.Children.Add(block);
        Assert.Equal(0, block.Opacity);
        panel.Children.Remove(block);

        Assert.Equal(1, block.Opacity);
        Assert.Equal(Sentence, block.Text);
    }
}
