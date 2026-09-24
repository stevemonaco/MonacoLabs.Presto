using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Monaco.Presto;
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
        var window = SceneHarness.Show(page, 1000, 900, frames: 30);
        var split = page.Split!;
        var lines = split.Lines.ToArray();
        var reveal = page.Reveal!;
        Assert.InRange(reveal.Progress, 0.01, 0.99);
        var progress = reveal.Progress;

        page.GetControl<Slider>("WidthSlider").Value = 360;
        TestFrames.Tick(15);

        Assert.DoesNotContain(split.Lines, lines.Contains);
        Assert.True(page.Reveal!.Progress > progress, $"the reveal went back from {progress:0.000} to {page.Reveal.Progress:0.000}");

        TestFrames.Tick(150);
        Assert.True(page.Reveal.Completed, "the reveal never finished");

        SceneHarness.Close(window);
        Assert.Equal(text, paragraph.Text);
        Assert.Null(paragraph.Inlines);
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

    private static Control[] WordmarkLetters(TypographyPage page) =>
        [.. page.GetControl<TextBlock>("Wordmark").GetVisualDescendants().OfType<TextBlock>().Where(text => text.Text?.Length == 1)];

    private static double Y(Control letter) => (double)Presto.Get(letter, "Y")!;
}
