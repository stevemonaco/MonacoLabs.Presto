using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using PrestoGallery.Showcase.Pages;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoGallery.Tests;

public sealed class MorphClockPageTests
{
    private const int Second = (1000 / 16) + 1;

    private static readonly DateTime Start = new(2026, 1, 1, 12, 34, 56);

    [AvaloniaFact]
    public void ATickMorphsOnlyTheChangedDigits()
    {
        var page = new MorphClockPage(Start);
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var clock = page.GetControl<TextBlock>("Clock");

        TestFrames.Tick(Second);
        var piece = Assert.IsType<Path>(Assert.Single(Pieces(clock)));
        var shape = piece.Data!.Bounds;
        TestFrames.Tick(10);
        Assert.NotEqual(shape, piece.Data!.Bounds);
        TestFrames.Tick(40);

        Assert.Empty(Pieces(clock));
        SceneHarness.AssertPlain(clock, "12:34:57");
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
        Assert.True(SceneHarness.WaitFor(() => label is { Text: "✓", Inlines: null or [] }));
        SceneHarness.AssertPlain(label, "✓");
        TestFrames.Tick((1400 + 700) / 16);

        SceneHarness.AssertPlain(label, "Submit");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheClockAndCountdownChangeAtOnceAndTheLabelsCrossfade()
    {
        using var reduced = ReducedMotion.On();
        var page = new MorphClockPage(Start);
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var (clock, countdown, label) = (page.GetControl<TextBlock>("Clock"), page.GetControl<TextBlock>("Countdown"), page.GetControl<TextBlock>("SubmitLabel"));
        SceneHarness.Click(window, page.GetControl<Button>("StartButton"));
        var (sawPieces, dimmestTick, dimmestLabel) = (false, 1.0, 1.0);

        for (var frame = 0; frame < Second + 3; frame++)
        {
            if (frame == 1) SceneHarness.Click(window, page.GetControl<Button>("SubmitButton"));
            TestFrames.Tick();
            sawPieces |= Pieces(clock).Length + Pieces(countdown).Length > 0;
            dimmestTick = Math.Min(dimmestTick, Math.Min(clock.Opacity, countdown.Opacity));
            dimmestLabel = Math.Min(dimmestLabel, label.Opacity);
        }

        Assert.False(sawPieces);
        Assert.Equal(1, dimmestTick);
        Assert.True(dimmestLabel < 0.5);
        SceneHarness.AssertPlain(clock, "12:34:57");
        SceneHarness.AssertPlain(countdown, "0:09");
        SceneHarness.Close(window);
    }

    private static Control[] Pieces(TextBlock block) =>
        AdornerLayer.GetAdornerLayer(block)!.Children.Where(c => AdornerLayer.GetAdornedElement(c) == block).SelectMany(c => ((Canvas)c).Children).ToArray();
}
