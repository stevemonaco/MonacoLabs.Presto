using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge.Text;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class TypographyPageTests
{
    [AvaloniaFact]
    public void ResizingMidRevealRewrapsTheLinesAndCarriesTheRevealOnThenDetachRevertsTheSplit()
    {
        var page = new TypographyPage();
        var paragraph = page.GetControl<TextBlock>("Paragraph");
        var text = paragraph.Text;
        var window = SceneHarness.Show(page, 1000, 900, frames: 12);
        var words = Pieces(paragraph);
        var lines = TextSplitter.GetLineIndex(words[^1]);
        // The first word starts at once however the text wraps, so its opacity is the reveal's own clock.
        var opacity = words[0].Opacity;
        Assert.InRange(opacity, 0.05, 0.95);

        page.GetControl<Slider>("WidthSlider").Value = 360;
        TestFrames.Tick(15);

        var rewrapped = Pieces(paragraph);
        Assert.DoesNotContain(rewrapped, words.Contains);
        Assert.True(TextSplitter.GetLineIndex(rewrapped[^1]) > lines, "the narrower paragraph did not wrap onto more lines");
        Assert.True(rewrapped[0].Opacity > opacity, $"the reveal went back from {opacity:0.000} to {rewrapped[0].Opacity:0.000}");

        TestFrames.Tick(200);
        Assert.Empty(Pieces(paragraph));
        Assert.Equal(text, paragraph.Text);
        Assert.Null(paragraph.Inlines);

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheWordmarkLiftsTheLettersNearestThePointerAndSpringsThemBackWhenItLeaves()
    {
        var page = new TypographyPage();
        var window = SceneHarness.Show(page, 1000, 1400, frames: 2);
        var letters = WordmarkLetters(page);

        window.MouseMove(SceneHarness.Centre(letters[2], window));
        TestFrames.Tick(20);

        var centre = Y(letters[2]);
        Assert.True(centre < Y(letters[0]) - 5 && centre < Y(letters[^1]) - 5,
            $"the pointed-at letter sits at {centre:0.#} against {Y(letters[0]):0.#} and {Y(letters[^1]):0.#} at the ends");

        window.MouseMove(new Point(1, 1));
        TestFrames.Tick(95);

        foreach (var letter in letters) Assert.InRange(Y(letter), -0.5, 0.5);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ALiftedSwollenWordmarkLetterIsNeverClipped()
    {
        var page = new TypographyPage();
        var window = SceneHarness.Show(page, 1000, 1400, frames: 2);
        var letters = WordmarkLetters(page);

        SceneHarness.Click(window, page.GetControl<Border>("WordmarkStage").GetVisualDescendants().OfType<Button>().First());
        for (var frame = 0; frame < 40; frame++)
        {
            TestFrames.Tick();
            foreach (var letter in letters) AssertUnclipped(letter);
        }

        window.MouseMove(SceneHarness.Centre(letters[0], window));
        TestFrames.Tick(20);
        Assert.True(Y(letters[0]) < -25, $"the pointed-at M only lifted to {Y(letters[0]):0.#}");
        foreach (var letter in letters) AssertUnclipped(letter);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheRotatorFadesTheOldWordOutAndTheNewOneInWithoutFlashing()
    {
        var page = new TypographyPage();
        var window = SceneHarness.Show(page, 1000, 1400, frames: 100);
        var slot = page.GetControl<Border>("Slot");
        var outgoing = (TextBlock)slot.Child!;
        var last = new Dictionary<TextBlock, double>();
        var arrived = new List<TextBlock>();

        for (var frame = 0; frame < 160; frame++)
        {
            TestFrames.Tick();
            var word = (TextBlock)slot.Child!;
            var leaving = word == outgoing;
            var pieces = Pieces(word);
            if (pieces.Length == 0)
            {
                // Whole, a word may only be the one waiting to leave, the one held hidden to rise in, or one that has fully risen.
                var settled = leaving ? last.Count == 0 : word.Opacity == 0 || arrived.Count > 0 && arrived.All(piece => last[piece] >= 1 - 1e-6);
                Assert.True(settled, $"frame {frame} shows '{word.Text}' whole mid-swap");
            }
            foreach (var piece in pieces)
            {
                Assert.InRange(piece.Opacity, -1e-6, 1 + 1e-6);
                if (!leaving && !arrived.Contains(piece)) arrived.Add(piece);
                if (last.TryGetValue(piece, out var before))
                {
                    Assert.True(leaving ? piece.Opacity <= before : piece.Opacity >= before,
                        $"frame {frame}: '{piece.Text}' went from {before:0.000} to {piece.Opacity:0.000} while {(leaving ? "leaving" : "arriving")}");
                }
                last[piece] = piece.Opacity;
            }
        }

        Assert.NotEmpty(arrived);
        Assert.NotSame(outgoing, slot.Child);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheRotatorHostEasesItsWidthBetweenTheTwoWordsRatherThanSnapping()
    {
        var page = new TypographyPage();
        var window = SceneHarness.Show(page, 1000, 1400, frames: 100);
        var slot = page.GetControl<Border>("Slot");
        var from = slot.Bounds.Width;
        var widths = new List<double>();

        for (var frame = 0; frame < 160; frame++)
        {
            TestFrames.Tick();
            widths.Add(slot.Bounds.Width);
        }

        var to = widths[^1];
        Assert.True(from - to > 40, $"'timelines' at {from:0.#} and 'text' at {to:0.#} are too close to tell an ease from a snap");
        var between = widths.Count(width => width < from - 2 && width > to + 2);
        Assert.True(between >= 5, $"the slot passed through only {between} widths between {from:0.#} and {to:0.#}");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheTerminalTypesItsFirstLineAndCountsUp()
    {
        var page = new TypographyPage();
        var window = SceneHarness.Show(page, 1000, 1400, frames: 2);
        var firstLine = (TextBlock)page.GetControl<StackPanel>("TerminalLines").Children[0];

        TestFrames.Tick(160);

        Assert.True(SceneHarness.Number(page.GetControl<TextBlock>("Percent")) > 0, "the counter never moved");
        Assert.Equal("$ presto decode --stream intercept.bin", firstLine.Text);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheRotatorHoldsItsFirstWordAndTheTerminalTypesOnce()
    {
        using var reduced = ReducedMotion.On();
        var page = new TypographyPage();
        var window = SceneHarness.Show(page, 1000, 1400, frames: 2);
        var slot = page.GetControl<Border>("Slot");
        var status = page.GetControl<TextBlock>("Status");

        TestFrames.Tick(600);
        Assert.Equal("timelines", ((TextBlock)slot.Child!).Text);
        Assert.Equal("decoded · 0 errors", status.Text);

        TestFrames.Tick(200);
        Assert.Equal("timelines", ((TextBlock)slot.Child!).Text);
        Assert.Equal("decoded · 0 errors", status.Text);
        Assert.Equal(100, SceneHarness.Number(page.GetControl<TextBlock>("Percent")));
        SceneHarness.Close(window);
    }

    private static Control[] WordmarkLetters(TypographyPage page) =>
        [.. page.GetControl<TextBlock>("Wordmark").GetVisualDescendants().OfType<TextBlock>().Where(text => text.Text?.Length == 1)];

    private static TextBlock[] Pieces(TextBlock block) => [.. block.GetVisualDescendants().OfType<TextBlock>()];

    private static void AssertUnclipped(Control letter)
    {
        foreach (var ancestor in letter.GetVisualAncestors().Where(visual => visual.ClipToBounds))
        {
            var drawn = new Rect(letter.Bounds.Size).TransformToAABB(letter.TransformToVisual(ancestor)!.Value);
            Assert.True(new Rect(ancestor.Bounds.Size).Contains(drawn),
                $"'{((TextBlock)letter).Text}' is drawn at {drawn}, outside the {ancestor.GetType().Name} clipping it at {ancestor.Bounds.Size}");
        }
    }

    private static double Y(Control letter) => (double)Presto.Get(letter, "Y")!;
}
