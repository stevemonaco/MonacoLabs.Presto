using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="Recipes.RollDigits"/>: which columns roll, which way, and that the block ends as plain text.</summary>
public sealed class RollDigitsTests : BridgeTestBase, IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public RollDigitsTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    void IDisposable.Dispose() => CultureInfo.CurrentCulture = _culture;

    [AvaloniaFact]
    public void OnlyTheChangedDigitsRoll()
    {
        var block = Shown("1,234");
        var animation = Recipes.RollDigits(block, 1294);

        Assert.Equal(["3", "9"], Glyphs(animation).Select(g => g.Text));
        Assert.Equal("1,2", string.Concat(Columns(block).Take(3).Cast<TextBlock>().Select(c => c.Text)));
    }

    [AvaloniaFact]
    public void MidRollTheOldGlyphIsAboveAndTheNewBelowInsideTheClip()
    {
        var block = Shown("5");
        var animation = Recipes.RollDigits(block, 6, duration: 400, ease: "linear");
        Frames.Advance(200, 10);

        var (old, next) = (Glyphs(animation)[0], Glyphs(animation)[1]);
        Assert.InRange(Y(old), -double.MaxValue, -1);
        Assert.InRange(Y(next), 1, double.MaxValue);
        Assert.InRange(old.Opacity, 0.01, 0.99);
        Assert.Same(old.Parent, next.Parent);
        Assert.True(((Control)old.Parent!).ClipToBounds);
    }

    [AvaloniaFact]
    public void TheTextKeepsItsLineBoxWhileRolling()
    {
        var block = new TextBlock { Text = "1,234", FontFamily = new("Consolas"), FontSize = 36 };
        Show(block);
        var height = block.Bounds.Height;
        Recipes.RollDigits(block, 5678);
        ((Window)TopLevel.GetTopLevel(block)!).UpdateLayout();

        Assert.Equal(height, block.Bounds.Height, 0.5);
        Assert.Equal(0, Columns(block)[1].TranslatePoint(default, block)!.Value.Y, 0.5);
    }

    [AvaloniaFact]
    public void EndsAsThePlainFormattedText()
    {
        var block = Shown("1,234");
        var animation = Recipes.RollDigits(block, 5678);
        RunUntil(() => animation.Completed);

        Assert.Equal("5,678", block.Text);
        Assert.Null(block.Inlines);
        Assert.Empty(block.GetVisualDescendants().OfType<TextBlock>());
    }

    [AvaloniaFact]
    public void AddingADigitAddsAColumnThatRollsIn()
    {
        var block = Shown("999");
        var animation = Recipes.RollDigits(block, 1000);

        Assert.Equal(5, Columns(block).Count);
        Assert.Equal(["9", "0", "9", "0", "9", "0", ",", "1"], Glyphs(animation).Select(g => g.Text));
        RunUntil(() => animation.Completed);
        Assert.Equal("1,000", block.Text);
    }

    [AvaloniaFact]
    public void ColumnsStartRightToLeft()
    {
        var block = Shown("11");
        var animation = Recipes.RollDigits(block, 22, duration: 400, ease: "linear");
        Frames.Advance(25, 5);

        var glyphs = Glyphs(animation);
        Assert.NotEqual(0, Y(glyphs[0]));
        Assert.Equal(0, Y(glyphs[2]));
    }

    [AvaloniaFact]
    public void CountingDownRollsTheOtherWay()
    {
        var block = Shown("6");
        var animation = Recipes.RollDigits(block, 5, duration: 400, ease: "linear");
        Frames.Advance(200, 10);

        Assert.InRange(Y(Glyphs(animation)[0]), 1, double.MaxValue);
        Assert.InRange(Y(Glyphs(animation)[1]), -double.MaxValue, -1);
    }

    [AvaloniaFact]
    public void RollingAgainMidRollEndsOnTheSecondValue()
    {
        var block = Shown("10");
        Recipes.RollDigits(block, 20, duration: 400);
        Frames.Advance(100, 10);
        var second = Recipes.RollDigits(block, 35, duration: 400);

        Assert.Equal(["0", "5", "2", "3"], Glyphs(second).Select(g => g.Text));
        RunUntil(() => second.Completed);
        Assert.Equal("35", block.Text);
        Assert.Null(block.Inlines);
    }

    [AvaloniaFact]
    public void ReducedSetsTheEndValueOnTheFirstFrame()
    {
        var block = Shown("10");
        Presto.Defaults.ReducedMotion = true;
        Recipes.RollDigits(block, 42);
        Frames.Advance(16);

        Assert.Equal("42", block.Text);
    }

    private static TextBlock Shown(string text)
    {
        var block = new TextBlock { Text = text };
        Show(block);
        return block;
    }

    private static IReadOnlyList<TextBlock> Glyphs(Animation animation) => animation.Targets.Cast<TextBlock>().ToArray();

    private static Avalonia.Controls.Controls Columns(TextBlock block) =>
        ((StackPanel)((Avalonia.Controls.Documents.InlineUIContainer)block.Inlines![0]).Child).Children;

    private static double Y(Control control) => Convert.ToDouble(PrestoAvalonia.Get(control, "Y"));
}
