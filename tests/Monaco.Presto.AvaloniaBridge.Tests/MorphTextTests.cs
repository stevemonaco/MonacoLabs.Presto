using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Text;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary><see cref="Recipes.MorphText"/>: how letters pair, where they fly from and to, and that the block ends as the plain new text.</summary>
public sealed class MorphTextTests : BridgeTestBase
{
    [AvaloniaFact]
    public void TheKthOccurrenceOfALetterPairsWithTheKthInTheNewText()
    {
        var block = Shown("banana");
        var old = OldPlaces(block);
        var morph = Recipes.MorphText(block, "nab");
        morph.Seek(0);

        var now = Places(block);
        Assert.Equal(["n", "a", "b"], now.Select(p => p.Text));
        AssertAt(old[2].At, now[0].At);
        AssertAt(old[1].At, now[1].At);
        AssertAt(old[0].At, now[2].At);
        Assert.Equal(3, Ghosts(block).Length);
        RunUntil(() => morph.Completed);
    }

    [AvaloniaFact]
    public void IgnoreCasePairsLettersThatDifferOnlyInCase()
    {
        var block = Shown("Listen");
        var old = OldPlaces(block);
        var morph = Recipes.MorphText(block, "Silent", ignoreCase: true);
        morph.Seek(0);

        var now = Places(block);
        AssertAt(old[2].At, now[0].At);
        AssertAt(old[0].At, now[2].At);
        Assert.Empty(Ghosts(block));
        RunUntil(() => morph.Completed);

        var cased = Recipes.MorphText(block, "Listen");
        Assert.Equal(2, Ghosts(block).Length);
        RunUntil(() => cased.Completed);
    }

    [AvaloniaFact]
    public void MatchedLettersStartAtTheirOldSlotsAndEndAtTheirNewOnes()
    {
        var block = new TextBlock { Text = "ab", FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center };
        Show(block);
        var old = OldPlaces(block);
        var morph = Recipes.MorphText(block, "b xa");
        morph.Seek(0);

        AssertAt(old[1].At, Places(block)[0].At);
        AssertAt(old[0].At, Places(block)[2].At);
        morph.Pause();
        morph.Seek(Recipes.Tokens.Long - 0.01);
        var split = TextSplitter.Of(block)!;
        Assert.All([split.Chars[0], split.Chars[2]], c =>
        {
            Assert.Equal(0, Value(c, "X"), 0.1);
            Assert.Equal(0, Value(c, "Y"), 0.1);
        });
    }

    [AvaloniaFact]
    public void AResizeMidMorphNeitherResplitsNorStallsIt()
    {
        var block = Shown("shatter");
        var window = (Window)TopLevel.GetTopLevel(block)!;
        var morph = Recipes.MorphText(block, "threats");
        var split = TextSplitter.Of(block);
        Frames.Advance(Recipes.Tokens.Long / 4, 5);

        window.Width = 120;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(200, 10);
        Assert.Same(split, TextSplitter.Of(block));
        Assert.Equal(200, block.Bounds.Width);

        RunUntil(() => morph.Completed);
        Assert.Equal("threats", block.Text);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(120, block.Bounds.Width);
    }

    [AvaloniaFact]
    public void UnmatchedOldLettersLeaveAsGhostsThatAreDismissed()
    {
        var block = Shown("cat");
        var morph = Recipes.MorphText(block, "ca");
        var ghost = Assert.Single(Ghosts(block));

        Frames.Advance(Recipes.Tokens.Medium / 8, 10);
        Assert.InRange(ghost.Opacity, 0.01, 0.99);
        RunUntil(() => morph.Completed);
        Assert.Empty(OverlayLayer.GetOverlayLayer(block)!.Children);
    }

    [AvaloniaFact]
    public void UnmatchedNewLettersPopIn()
    {
        var block = Shown("ca");
        var morph = Recipes.MorphText(block, "cat");
        var entering = TextSplitter.Of(block)!.Chars[2];
        morph.Pause();
        morph.Seek(0);

        Assert.Equal(Recipes.Tokens.Scale, Value(entering, "Scale"));
        Assert.Equal(0, entering.Opacity);
        morph.Seek(Recipes.Tokens.Medium - 0.01);
        Assert.Equal(1, entering.Opacity, 0.05);
    }

    [AvaloniaFact]
    public void ExplodeHoldsTheMatchedLettersAtSeededPointsBeforeGathering()
    {
        var block = Shown("ab");
        var old = OldPlaces(block);
        var morph = Recipes.MorphText(block, "ba", explode: true);
        morph.Seek(0);
        var start = Places(block);
        var tokens = Recipes.Tokens;
        var (x, y, _) = Recipes.ScatterOffsets(null, null, 0);
        morph.Pause();

        foreach (var at in new[] { tokens.Medium + 1, tokens.Medium + tokens.Short - 1 })
        {
            morph.Seek(at);
            var held = Places(block);
            for (var i = 0; i < 2; i++)
            {
                var expected = start[i].At + new Vector(x(null!, i, 2).AsDouble!.Value, y(null!, i, 2).AsDouble!.Value);
                AssertAt(expected, held[i].At, 0.5);
            }
        }
        AssertAt(old[1].At, start[0].At);
        morph.Seek(tokens.Medium + tokens.Short + tokens.Long - 0.01);
        Assert.Equal(0, Value(TextSplitter.Of(block)!.Chars[0], "X"), 0.1);
    }

    [AvaloniaFact]
    public void EndsAsThePlainNewText()
    {
        var block = Shown("hello");
        var morph = Recipes.MorphText(block, "world", explode: true);
        RunUntil(() => morph.Completed);

        Assert.Equal("world", block.Text);
        Assert.Null(block.Inlines);
        Assert.Null(TextSplitter.Of(block));
        Assert.Empty(block.GetVisualDescendants().OfType<TextBlock>());
        Assert.Empty(OverlayLayer.GetOverlayLayer(block)!.Children);
    }

    [AvaloniaFact]
    public void MorphingAgainMidMorphCompletesTheFirst()
    {
        var block = Shown("abc");
        var first = Recipes.MorphText(block, "cab");
        Frames.Advance(100, 10);
        var second = Recipes.MorphText(block, "xyz");

        Assert.True(first.Completed);
        Assert.Equal(["x", "y", "z"], Places(block).Select(p => p.Text));
        RunUntil(() => second.Completed);
        Assert.Equal("xyz", block.Text);
        Assert.Null(block.Inlines);
    }

    [AvaloniaFact]
    public void ReducedCrossfadesTheBlock()
    {
        var block = Shown("one");
        Presto.Defaults.ReducedMotion = true;
        var morph = Recipes.MorphText(block, "two");
        var tokens = Recipes.Tokens;

        Assert.Null(TextSplitter.Of(block));
        Assert.Equal(2 * tokens.Short, morph.Duration, 1);
        morph.Pause();
        morph.Seek(tokens.Short / 2);
        Assert.Equal("one", block.Text);
        Assert.InRange(block.Opacity, 0.01, 0.99);
        morph.Seek(tokens.Short + 1);
        Assert.Equal("two", block.Text);
        Assert.InRange(block.Opacity, 0, 0.1);
        morph.Seek(morph.Duration);
        Assert.Equal(1, block.Opacity);
    }

    [AvaloniaFact]
    public void LettersFlyBetweenLines()
    {
        var block = new TextBlock { Text = "alpha beta gamma delta", FontSize = 24, TextWrapping = TextWrapping.Wrap };
        Show(block);
        var old = OldPlaces(block);
        Assert.NotEqual(old[2].At.Y, old[14].At.Y);
        var morph = Recipes.MorphText(block, "delta gamma beta alpha");
        morph.Seek(0);

        var now = Places(block);
        AssertAt(old[14].At, now[0].At);
        AssertAt(old[2].At, now[^3].At);
        RunUntil(() => morph.Completed);
        Assert.Equal("delta gamma beta alpha", block.Text);
    }

    private static TextBlock Shown(string text)
    {
        var block = new TextBlock { Text = text, FontSize = 20 };
        Show(block);
        return block;
    }

    /// <summary>Each character of the block's split with where it is drawn in the window, its <c>X</c>/<c>Y</c> included.</summary>
    private static (string Text, Point At)[] Places(TextBlock block)
    {
        var split = TextSplitter.Of(block)!;
        var origin = (Vector)block.TranslatePoint(default, TopLevel.GetTopLevel(block)!)!.Value;
        return split.Chars.Select(c => (((TextBlock)c).Text!, split.SlotOf(c).Position + origin + new Vector(Value(c, "X"), Value(c, "Y")))).ToArray();
    }

    private static (string Text, Point At)[] OldPlaces(TextBlock block)
    {
        var split = PrestoAvalonia.SplitText(block, new() { Words = null, Chars = new() });
        try
        {
            return Places(block);
        }
        finally
        {
            split.Revert();
        }
    }

    private static Image[] Ghosts(TextBlock block) =>
        OverlayLayer.GetOverlayLayer(block)!.Children.SelectMany(c => c.GetVisualDescendants()).OfType<Image>().ToArray();

    private static void AssertAt(Point expected, Point actual, double precision = 0.01)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
    }

    private static double Value(Control control, string property) => Convert.ToDouble(PrestoAvalonia.Get(control, property));
}
