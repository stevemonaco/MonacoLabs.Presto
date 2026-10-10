using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using PrestoSample.Showcase.Pages;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoSample.Tests;

public sealed class MorphClockPageTests
{
    private const int Second = (1000 / 16) + 1;

    private static readonly DateTime Start = new(2026, 1, 1, 12, 34, 56);

    [AvaloniaFact]
    public void ATickMorphsOnlyTheChangedDigits()
    {
        var page = new MorphClockPage(Start);
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var seconds = page.GetControl<TextBlock>("Seconds");

        TestFrames.Tick(Second);
        Assert.Empty(page.GetControl<TextBlock>("Hours").GetVisualDescendants().OfType<Path>());
        Assert.Empty(page.GetControl<TextBlock>("Minutes").GetVisualDescendants().OfType<Path>());
        var pieces = seconds.GetVisualDescendants().OfType<Path>().ToArray();
        Assert.Equal(2, pieces.Length);
        var (tens, ones) = (pieces[0], pieces[1]);
        var (tensShape, onesShape) = (tens.Data!.Bounds, ones.Data!.Bounds);
        TestFrames.Tick(10);
        Assert.Equal(tensShape, tens.Data!.Bounds);
        Assert.NotEqual(onesShape, ones.Data!.Bounds);
        TestFrames.Tick(40);

        SceneHarness.AssertPlain(seconds, "57");
        SceneHarness.AssertPlain(page.GetControl<TextBlock>("Minutes"), "34");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheCountdownEndsAsDone()
    {
        var page = new MorphClockPage(Start);
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var start = page.GetControl<Button>("StartButton");

        SceneHarness.Click(window, start);
        TestFrames.Tick((11 * Second) + 50);

        SceneHarness.AssertPlain(page.GetControl<TextBlock>("Countdown"), "DONE");
        Assert.Equal("Start", start.Content);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheSubmitLabelBecomesACheckAndReturns()
    {
        var page = new MorphClockPage(Start);
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var label = page.GetControl<TextBlock>("SubmitLabel");

        SceneHarness.Click(window, page.GetControl<Button>("SubmitButton"));
        TestFrames.Tick((1100 + 700) / 16);
        SceneHarness.AssertPlain(label, "✓");
        TestFrames.Tick((1400 + 700) / 16);

        SceneHarness.AssertPlain(label, "Submit");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheDigitsCrossfadeAndTheColonsHoldStill()
    {
        using var reduced = ReducedMotion.On();
        var page = new MorphClockPage(Start);
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var seconds = page.GetControl<TextBlock>("Seconds");
        var (sawOutlines, dimmest, colon) = (false, 1.0, 1.0);

        for (var frame = 0; frame < Second + 40; frame++)
        {
            TestFrames.Tick();
            sawOutlines |= seconds.GetVisualDescendants().OfType<Path>().Any();
            dimmest = Math.Min(dimmest, seconds.Opacity);
            colon = Math.Min(colon, page.GetControl<TextBlock>("FirstColon").Opacity);
        }

        Assert.False(sawOutlines);
        Assert.True(dimmest < 0.5);
        Assert.Equal(1, colon);
        SceneHarness.AssertPlain(seconds, "57");
        Assert.Equal(1, seconds.Opacity, 3);
        SceneHarness.Close(window);
    }
}
