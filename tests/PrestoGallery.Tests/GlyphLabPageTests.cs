using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using PrestoGallery.Showcase.Pages;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoGallery.Tests;

public sealed class GlyphLabPageTests
{
    [AvaloniaFact]
    public void TheWriteInEndsAsPlainTextInBothModes()
    {
        var page = new GlyphLabPage();
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var word = page.GetControl<TextBlock>("Handwriting");
        Assert.NotEmpty(word.GetVisualDescendants().OfType<Path>());
        TestFrames.Tick(150);
        SceneHarness.AssertPlain(word, "Glyphs");

        page.GetControl<ToggleSwitch>("LetterByLetter").IsChecked = false;
        SceneHarness.Click(window, page.GetControl<Button>("ReplayButton"));
        Assert.NotEmpty(word.GetVisualDescendants().OfType<Path>());
        TestFrames.Tick(150);

        SceneHarness.AssertPlain(word, "Glyphs");
        Assert.Equal(1, word.Opacity);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheLetterCycleAdvancesAndDrawsEachContour()
    {
        var page = new GlyphLabPage();
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var glyph = page.GetControl<TextBlock>("Glyph");
        page.GetControl<ToggleSwitch>("ContoursToggle").IsChecked = true;
        Assert.Equal(2, page.GetControl<Canvas>("ContourLayer").Children.Count);

        TestFrames.Tick((1400 / 16) + 2);
        Assert.NotEmpty(AdornerLayer.GetAdornerLayer(glyph)!.GetVisualDescendants().OfType<Path>());
        // B's outer and two counters, plus the ring the second counter grows from.
        Assert.Equal(4, page.GetControl<Canvas>("ContourLayer").Children.Count);
        TestFrames.Tick(60);
        SceneHarness.AssertPlain(glyph, "B");
        TestFrames.Tick(1400 / 16);

        SceneHarness.AssertPlain(glyph, "C");
        SceneHarness.AssertPlain(page.GetControl<TextBlock>("Paired"), "B");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ShatterEndsAsThePlainNextWord()
    {
        var page = new GlyphLabPage();
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var word = page.GetControl<TextBlock>("ShatterWord");

        SceneHarness.Click(window, word);
        TestFrames.Tick(120);

        SceneHarness.AssertPlain(word, "GLYPHS");
        Assert.Equal(1, word.Opacity, 3);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ReverseReassemblesTheSameWord()
    {
        var page = new GlyphLabPage();
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var word = page.GetControl<TextBlock>("ShatterWord");

        SceneHarness.Click(window, word);
        TestFrames.Tick(10);
        SceneHarness.Click(window, page.GetControl<Button>("ReverseButton"));
        TestFrames.Tick(120);

        SceneHarness.AssertPlain(word, "SHARDS");
        Assert.Equal(1, word.Opacity, 3);
        Assert.Empty(OverlayLayer.GetOverlayLayer(word)!.GetVisualDescendants().OfType<Path>());
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheGlyphsCrossfadeWithNoOutlinesOrShards()
    {
        using var reduced = ReducedMotion.On();
        var page = new GlyphLabPage();
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var glyph = page.GetControl<TextBlock>("Glyph");
        var word = page.GetControl<TextBlock>("ShatterWord");
        var overlay = OverlayLayer.GetOverlayLayer(word)!;
        SceneHarness.Click(window, word);
        var sawOutlines = false;

        for (var frame = 0; frame < (1400 / 16) + 30; frame++)
        {
            TestFrames.Tick();
            sawOutlines |= AdornerLayer.GetAdornerLayer(glyph)!.GetVisualDescendants().OfType<Path>().Any() || overlay.GetVisualDescendants().OfType<Path>().Any();
        }

        Assert.False(sawOutlines);
        SceneHarness.AssertPlain(glyph, "B");
        SceneHarness.AssertPlain(word, "GLYPHS");
        SceneHarness.Close(window);
    }
}
