using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Monaco.Presto;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

/// <summary>
/// SAMPLES-PLAN §4.8: what each composition leaves alive behind a spammed stage, where the three pucks land, and
/// which way a wobble and the travel under it can reach the same property. The clicks are here because the DevTools
/// MCP drives a Button through its automation peer and has no pointer input at all.
/// </summary>
public sealed class InterruptionPageTests
{
    private const double Width = 1000;
    private const double Height = 820;
    private const int Clicks = 5;
    private const int BetweenClicks = 6;
    private const int RetargetFrames = 60;

    private static readonly Composition[] Modes = [Composition.Replace, Composition.None, Composition.Blend];

    private static readonly Point[] Points =
        [.. Enumerable.Range(0, Clicks).Select(i => new Point(320 + (i * 70), 90 + (i * 34)))];

    [AvaloniaFact]
    public void SpammingTheStageLeavesReplaceHoldingOneAnimationAndEveryPuckLandingOnTheLastClick()
    {
        var page = new InterruptionPage();
        var window = SceneHarness.Show(page, Width, Height);
        var stage = page.GetControl<Canvas>("ClickStage");

        foreach (var point in Points)
        {
            ClickAt(window, stage, point);
            TestFrames.Tick(BetweenClicks);
        }

        Assert.Equal(1, page.Alive(Composition.Replace));
        Assert.Equal(Clicks, page.Alive(Composition.None));
        Assert.Equal(Clicks, page.Alive(Composition.Blend));

        var last = Points[^1];
        TestFrames.Tick(20);
        var replaceLeft = Math.Abs(Drawn(page, Composition.Replace).X - last.X);
        var blendLeft = Math.Abs(Drawn(page, Composition.Blend).X - last.X);
        Assert.True(
            replaceLeft > blendLeft + 4,
            $"replace was {replaceLeft:0.#}px from the click and blend {blendLeft:0.#}px, so blend is not carrying the earlier clicks on");

        TestFrames.Tick(RetargetFrames);
        foreach (var mode in Modes)
        {
            var drawn = Drawn(page, mode);
            Assert.Equal(last.X, drawn.X, 1);
            Assert.Equal(last.Y, drawn.Y, 1);
        }

        Assert.Equal(0, page.Alive(Composition.None));
        Assert.Equal(0, page.Alive(Composition.Blend));

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ResetTakesEveryCountToZeroAndPutsThePucksBackHome()
    {
        var page = new InterruptionPage();
        var window = SceneHarness.Show(page, Width, Height);
        var stage = page.GetControl<Canvas>("ClickStage");

        foreach (var point in Points) ClickAt(window, stage, point);
        TestFrames.Tick(BetweenClicks);
        Assert.True(page.Alive(Composition.None) > 0, "the clicks never reached the stage");

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
    public void TheWobbleRidesTheTravelBlendedAndNeverReachesTheCardReplaced()
    {
        var page = new InterruptionPage();
        var window = SceneHarness.Show(page, Width, Height);
        var card = page.GetControl<Border>("Card");
        var toggle = page.GetControl<ToggleSwitch>("BlendWobble");
        TestFrames.Tick(30);

        // Parked so that the only thing left that can move the card is the wobble.
        page.Travel!.Pause();
        TestFrames.Tick();
        var parked = X(card);

        toggle.IsChecked = false;
        Hover(window, card);
        TestFrames.Tick(8);
        Assert.Equal(parked, X(card), 1);
        Assert.False(page.Travel.Cancelled, "the replaced wobble cancelled the travel it cannot even reach");

        toggle.IsChecked = true;
        Hover(window, card);
        TestFrames.Tick(8);
        Assert.True(Math.Abs(X(card) - parked) > 4, $"the blended wobble moved the parked card {X(card) - parked:0.#}px");
        Assert.False(page.Travel.Cancelled);

        page.Travel.Resume();
        TestFrames.Tick(30);
        Assert.True(X(card) > parked + 20, "the travel did not carry on under the wobble that blended onto it");

        SceneHarness.Close(window);
    }

    private static Border Puck(InterruptionPage page, Composition mode) =>
        page.GetControl<Border>($"{mode}Puck");

    /// <summary>Where a puck is drawn in its stage: its canvas slot plus the translation the tweens wrote.</summary>
    private static Point Drawn(InterruptionPage page, Composition mode)
    {
        var puck = Puck(page, mode);
        return new Point(
            Canvas.GetLeft(puck) + X(puck) + (puck.Bounds.Width / 2),
            Canvas.GetTop(puck) + (double)Presto.Get(puck, "Y")! + (puck.Bounds.Height / 2));
    }

    private static double X(Visual target) => (double)Presto.Get(target, "X")!;

    private static void ClickAt(Window window, Visual stage, Point at)
    {
        var point = stage.TranslatePoint(at, window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    /// <summary>Takes the pointer off the card and back onto it, which is what fires the hover the wobble hangs on.</summary>
    private static void Hover(Window window, Visual card)
    {
        window.MouseMove(new Point(Width - 1, Height - 1));
        TestFrames.Tick();
        window.MouseMove(SceneHarness.Centre(card, window));
        TestFrames.Tick();
    }
}
