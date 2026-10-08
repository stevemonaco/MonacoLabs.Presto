using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Monaco.Presto;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

/// <summary>
/// SAMPLES-PLAN §4.8: what each composition leaves alive behind a burst, what canceling the newest retarget reveals,
/// and that a click in one pane is mirrored into the other two. The clicks are here because the DevTools MCP drives a
/// Button through its automation peer and has no pointer input at all.
/// </summary>
public sealed class InterruptionPageTests
{
    private const double Width = 1000;
    private const double Height = 820;
    private const int BurstFrames = 44;
    private const int RetargetFrames = 70;
    private const double HoverJump = 0.02;
    private const int HoverFrames = 3; // early, while OutQuint still rises steeply, or restart's snap is under a frame's travel

    private static readonly Composition[] Modes = [Composition.Replace, Composition.None, Composition.Blend];

    [AvaloniaFact]
    public void ABurstLeavesReplaceHoldingOneAnimationAndNoneHoldingAllFiveUntilTheyComplete()
    {
        var page = new InterruptionPage();
        var window = SceneHarness.Show(page, Width, Height);

        page.Burst();
        TestFrames.Tick(BurstFrames);

        Assert.Equal(1, page.Alive(Composition.Replace));
        Assert.Equal(5, page.Alive(Composition.None));

        TestFrames.Tick(RetargetFrames);
        foreach (var mode in Modes) Assert.Equal(0, page.Alive(mode));

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void CancelingTheNewestYanksNoneBackTowardTheOlderTargetsAndStopsReplaceWhereItIs()
    {
        var page = new InterruptionPage();
        var window = SceneHarness.Show(page, Width, Height);
        Point[] script = [new(0.2, 0.2), new(0.8, 0.8), new(0.2, 0.8), new(0.8, 0.2), new(0.2, 0.2)];

        foreach (var relative in script)
        {
            ClickAt(window, page, Composition.None, relative);
            TestFrames.Tick(10);
        }

        var last = Mirrored(page, Composition.None, script[^1]);
        var noneBefore = Point.Distance(page.Drawn(Composition.None), last);
        var replaceBefore = page.Drawn(Composition.Replace);

        page.CancelNewest();
        TestFrames.Tick(4);

        var noneAfter = Point.Distance(page.Drawn(Composition.None), last);
        Assert.True(noneAfter > noneBefore + 4, $"none went from {noneBefore:0.#}px to {noneAfter:0.#}px off the last target");
        for (var frame = 0; frame < 10; frame++)
        {
            Assert.True(Point.Distance(page.Drawn(Composition.Replace), replaceBefore) < 1, "replace kept moving after its only animation was canceled");
            TestFrames.Tick();
        }

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ClickingOnePaneSendsAllThreePucksToTheSameRelativePoint()
    {
        var page = new InterruptionPage();
        var window = SceneHarness.Show(page, Width, Height);
        var relative = new Point(0.7, 0.4);

        ClickAt(window, page, Composition.Blend, relative);
        TestFrames.Tick(RetargetFrames);

        foreach (var mode in Modes)
        {
            var target = Mirrored(page, mode, relative);
            var drawn = page.Drawn(mode);
            Assert.Equal(target.X, drawn.X, 1);
            Assert.Equal(target.Y, drawn.Y, 1);
        }

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ResetTakesEveryCountToZeroAndPutsThePucksBackHome()
    {
        var page = new InterruptionPage();
        var window = SceneHarness.Show(page, Width, Height);

        page.Burst();
        TestFrames.Tick(BurstFrames);
        Assert.True(page.Alive(Composition.None) > 0, "the burst never retargeted anything");

        SceneHarness.Click(window, page.GetControl<Button>("ResetPucks"));
        TestFrames.Tick(2);

        foreach (var mode in Modes)
        {
            Assert.Equal(0, page.Alive(mode));
            Assert.Equal(0d, Presto.Get(Puck(page, mode), "X"));
            Assert.Equal(0d, Presto.Get(Puck(page, mode), "Y"));
        }

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void LeavingAHalfRaisedTileUnderRetargetCarriesOnFromTheCurrentScaleWithoutAJump()
    {
        var (before, after) = ScaleAcrossALeave(restart: false);

        Assert.True(after <= before, $"the scale went back up from {before:0.###} to {after:0.###} on the leave");
    }

    [AvaloniaFact]
    public void LeavingAHalfRaisedTileUnderRestartJumpsItToTheHoveredScaleFirst()
    {
        var (before, after) = ScaleAcrossALeave(restart: true);

        Assert.True(after - before > HoverJump, $"the scale went from {before:0.###} to {after:0.###} on the leave");
    }

    private static (double Before, double After) ScaleAcrossALeave(bool restart)
    {
        var page = new InterruptionPage();
        var window = SceneHarness.Show(page, Width, Height);
        page.GetControl<ToggleSwitch>("HoverMode").IsChecked = restart;
        var tile = page.GetControl<UniformGrid>("Tiles").Children[1];

        window.MouseMove(SceneHarness.Center(tile, window));
        TestFrames.Tick(HoverFrames);
        var before = (double)Presto.Get(tile, "Scale")!;
        window.MouseMove(new Point(Width - 1, Height - 1));
        TestFrames.Tick();
        var after = (double)Presto.Get(tile, "Scale")!;

        SceneHarness.Close(window);
        return (before, after);
    }

    private static Border Puck(InterruptionPage page, Composition mode) => page.GetControl<Border>($"{mode}Puck");

    private static Canvas Pane(InterruptionPage page, Composition mode) => page.GetControl<Canvas>($"{mode}Pane");

    private static Point Mirrored(InterruptionPage page, Composition mode, Point relative)
    {
        var size = Pane(page, mode).Bounds.Size;
        return new Point(relative.X * size.Width, relative.Y * size.Height);
    }

    private static void ClickAt(Window window, InterruptionPage page, Composition mode, Point relative)
    {
        var pane = Pane(page, mode);
        var point = pane.TranslatePoint(Mirrored(page, mode, relative), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }
}
