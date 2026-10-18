using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Controls;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="SplitFlap"/>: a board shown in a window, advanced in 10 ms frames.</summary>
public sealed class SplitFlapTests : BridgeTestBase
{
    [AvaloniaFact]
    public void ACellStepsThroughTheCharsetInOrderAndWraps()
    {
        var board = Board("Z");

        board.Text = "B";

        Assert.Equal("Z0123456789.:-/ AB", string.Concat(Steps(board)));
    }

    [AvaloniaFact]
    public void ACharacterOutsideTheCharsetTakesOneStep()
    {
        var board = Board("A");

        board.Text = "#";
        Assert.Equal("A#", string.Concat(Steps(board)));

        board.Text = "B";
        Assert.Equal("#B", string.Concat(Steps(board)));
    }

    [AvaloniaFact]
    public void AnUppercaseCharsetShowsTheTextUpperCased()
    {
        Assert.Equal("ABC", Shown(Board("abc")));
        Assert.Equal("abc", Shown(Board("abc", " abc")));
    }

    [AvaloniaFact]
    public void CellsStartTheirStaggerApart()
    {
        var board = Board("AAA");
        var cells = CellsOf(board);
        var started = new double?[3];

        board.Text = "BBB";
        RunUntil(() =>
        {
            for (var i = 0; i < 3; i++) started[i] ??= Flipping(cells[i]) ? Frames.Now : null;
            return Presto.Engine._head is null;
        }, 10);

        Assert.Equal("BBB", Shown(board));
        // Cell 0 starts with the change, before the first frame, so only the later starts are a full stagger apart.
        Assert.True(started[0] < started[1]);
        Assert.InRange(started[2]!.Value - started[1]!.Value, 40, 60);
    }

    [AvaloniaFact]
    public void ATextChangeMidFlipCarriesOnFromWhatTheCellShows()
    {
        var board = Board("A");
        board.FlipDuration = 100;
        var cell = CellsOf(board)[0];
        board.Text = "D";
        RunUntil(() => Half(cell, 1) == "B" && cell.Children[3].IsVisible, 10);
        var angle = Unfold(cell);

        board.Text = "F";
        Frames.Advance(10);

        Assert.Equal(("C", "B"), (Half(cell, 0), Half(cell, 1)));
        Assert.True(cell.Children[3].IsVisible);
        Assert.True(Unfold(cell) < angle, $"the unfold went from {angle} to {Unfold(cell)}");
        Assert.Equal("BCDEF", string.Concat(Steps(board)));
    }

    [AvaloniaFact]
    public void ABoardAtRestStopsRequestingFrames()
    {
        var board = Board("A");

        board.Text = "C";
        Frames.Advance(400, 10);

        Assert.Equal("C", Shown(board));
        Assert.Null(Presto.Engine._head);
        Assert.False(Frames.IsRunning);
    }

    [AvaloniaFact]
    public void TheBoardIsNamedByItsTextAndItsCellsAreRaw()
    {
        var board = Board("gate 4", cells: 8);

        Assert.Equal("gate 4", AutomationProperties.GetName(board));
        Assert.Equal(8, CellsOf(board).Length);
        Assert.All(CellsOf(board), cell => Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(cell)));
    }

    [AvaloniaFact]
    public void ReducedShowsTheTargetsAtOnceAndFadesTheChangedCells()
    {
        var board = Board("AB");
        var cells = CellsOf(board);
        Presto.Defaults.ReducedMotion = true;

        board.Text = "AZ";
        Frames.Advance(50, 10);

        Assert.Equal("AZ", Shown(board));
        Assert.False(Flipping(cells[1]));
        Assert.Equal(1, cells[0].Opacity);
        Assert.InRange(cells[1].Opacity, .01, .99);
        Frames.Advance(Recipes.Tokens.Short, 10);
        Assert.Equal(1, cells[1].Opacity);
    }

    [AvaloniaFact]
    public void LeavingTheTreeShowsTheTargets()
    {
        var board = Board("AAAA");
        board.Text = "ZZZZ";
        Frames.Advance(100, 10);

        ((Window)TopLevel.GetTopLevel(board)!).Content = null;
        Frames.Advance(20, 10);

        Assert.Equal("ZZZZ", Shown(board));
        Assert.All(CellsOf(board), cell => Assert.False(Flipping(cell)));
        Assert.Null(Presto.Engine._head);
    }

    [AvaloniaFact]
    public void WhileTheEngineIsHiddenTheCellsHoldStill()
    {
        var board = Board("AAAA");
        board.Text = "ZZZZ";
        Frames.Advance(100, 10);
        var shown = Shown(board);

        Presto.Engine.Hidden = true;
        Frames.Advance(2000, 10);
        Assert.Equal(shown, Shown(board));

        Presto.Engine.Hidden = false;
        Frames.Advance(5000, 10);
        Assert.Equal("ZZZZ", Shown(board));
    }

    private static SplitFlap Board(string text, string? charset = null, int cells = 0)
    {
        var board = new SplitFlap { Text = text, Cells = cells, FontSize = 20, Foreground = Brushes.White, CellBackground = Brushes.Black };
        if (charset != null) board.Charset = charset;
        Show(board);
        return board;
    }

    private static Panel[] CellsOf(SplitFlap board) =>
        [.. ((StackPanel)board.GetVisualChildren().Single()).Children.Cast<Panel>()];

    // Half indices: static top 0, static bottom 1, folding top 2, unfolding bottom 3.
    private static string Half(Panel cell, int half) => ((TextBlock)((Border)cell.Children[half]).Child!).Text!;

    private static string Shown(SplitFlap board) => string.Concat(CellsOf(board).Select(cell => Half(cell, 1)));

    private static bool Flipping(Panel cell) => cell.Children[2].IsVisible || cell.Children[3].IsVisible;

    private static double Unfold(Panel cell) => ((Rotate3DTransform)cell.Children[3].RenderTransform!).AngleX;

    private List<string> Steps(SplitFlap board)
    {
        var cell = CellsOf(board)[0];
        List<string> steps = [];
        RunUntil(() =>
        {
            if (steps.Count == 0 || steps[^1] != Half(cell, 1)) steps.Add(Half(cell, 1));
            return Presto.Engine._head is null;
        }, 10);
        return steps;
    }
}
