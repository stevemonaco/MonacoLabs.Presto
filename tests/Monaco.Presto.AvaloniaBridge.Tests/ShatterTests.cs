using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Text;
using Path = Avalonia.Controls.Shapes.Path;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary><see cref="Recipes.Shatter"/>: the shards it puts on the overlay, how they fly, re-form and leave, and its reduced form.</summary>
public sealed class ShatterTests : BridgeTestBase
{
    [AvaloniaFact]
    public void ShardsCoverTheLettersOnTheOverlayInTheBlocksForeground()
    {
        var block = Shown("OB");
        var overlay = OverlayLayer.GetOverlayLayer(block)!;
        var letters = Letters(block, overlay);

        var shatter = Recipes.Shatter(block, count: 6);
        var shards = Shards(block);
        overlay.UpdateLayout();

        Assert.Equal(12, shards.Length);
        Assert.All(shards, s => Assert.Same(block.Foreground, s.Fill));
        var covered = shards.Select(s => s.Data!.Bounds.Translate(s.TranslatePoint(default, overlay)!.Value)).Aggregate((a, b) => a.Union(b));
        Assert.Equal(letters.X, covered.X, 0.5);
        Assert.Equal(letters.Y, covered.Y, 0.5);
        Assert.Equal(letters.Width, covered.Width, 0.5);
        Assert.Equal(letters.Height, covered.Height, 0.5);
        Frames.Advance(16, 1);
        Assert.Equal(0, block.Opacity);
        RunUntil(() => shatter.Completed);
    }

    [AvaloniaFact]
    public void ShardsFlyApartSpinningAndCompletingRemovesThemLeavingTheBlockHidden()
    {
        var block = Shown("O");
        var shatter = Recipes.Shatter(block, seed: 3);
        var shards = Shards(block);
        Frames.Advance(Recipes.Tokens.Medium / 2, 5);

        Assert.All(shards, s => Assert.NotEqual(0, Value(s, "Rotate")));
        RunUntil(() => shatter.Completed);
        Assert.Empty(OverlayLayer.GetOverlayLayer(block)!.Children);
        Assert.Equal(0, block.Opacity);
    }

    [AvaloniaFact]
    public void ReversedTheShardsReformOnTheirSlotsAndTheBlockComesBack()
    {
        var block = Shown("O");
        var shatter = Recipes.Shatter(block);
        var shards = Shards(block);
        Frames.Advance(Recipes.Tokens.Medium / 2, 5);
        Assert.Contains(shards, s => Value(s, "X") != 0);

        shatter.Reverse();
        RunUntil(() => shatter.Completed);
        Assert.All(shards, s =>
        {
            Assert.Equal(0, Value(s, "X"), 0.01);
            Assert.Equal(0, Value(s, "Y"), 0.01);
            Assert.Equal(0, Value(s, "Rotate"), 0.01);
            Assert.Equal(1, Value(s, "Scale"), 0.01);
            Assert.Equal(1, s.Opacity, 0.01);
        });
        Assert.Empty(OverlayLayer.GetOverlayLayer(block)!.Children);
        Assert.Equal(1, block.Opacity);
    }

    [AvaloniaFact]
    public void ShatteringAgainMidShatterCompletesTheFirst()
    {
        var block = Shown("O");
        var first = Recipes.Shatter(block);
        Frames.Advance(100, 10);
        var second = Recipes.Shatter(block);

        Assert.True(first.Completed);
        Assert.Single(OverlayLayer.GetOverlayLayer(block)!.Children);
        RunUntil(() => second.Completed);
        Assert.Empty(OverlayLayer.GetOverlayLayer(block)!.Children);
    }

    [AvaloniaFact]
    public void ReducedFadesTheBlockWithoutShards()
    {
        var block = Shown("O");
        Presto.Defaults.ReducedMotion = true;
        var shatter = Recipes.Shatter(block);

        Assert.Empty(OverlayLayer.GetOverlayLayer(block)!.Children);
        Assert.Equal(Recipes.Tokens.Short, shatter.Duration, 1);
        RunUntil(() => shatter.Completed);
        Assert.Equal(0, block.Opacity);
    }

    private static TextBlock Shown(string text)
    {
        var block = new TextBlock { Text = text, FontSize = 40, Foreground = Brushes.Teal };
        Show(block);
        return block;
    }

    private static Path[] Shards(TextBlock block) =>
        OverlayLayer.GetOverlayLayer(block)!.Children.SelectMany(c => c.GetVisualDescendants()).OfType<Path>().ToArray();

    /// <summary>The union of the block's glyph outlines, each on its character's slot, in the overlay's coordinates.</summary>
    private static Rect Letters(TextBlock block, Visual overlay)
    {
        var split = PrestoAvalonia.SplitText(block, new() { Words = null, Chars = new() });
        try
        {
            var origin = (Vector)block.TranslatePoint(default, overlay)!.Value;
            return split.Chars.Select((c, i) => PrestoAvalonia.GlyphOutline(block, i).Bounds.Translate(split.SlotOf(c).Position + origin)).Aggregate((a, b) => a.Union(b));
        }
        finally
        {
            split.Revert();
        }
    }

    private static double Value(Control control, string property) => Convert.ToDouble(Presto.Get(control, property, withUnit: false));
}
