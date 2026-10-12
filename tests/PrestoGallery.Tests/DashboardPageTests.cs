using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Core;
using PrestoGallery.Showcase;
using PrestoGallery.Showcase.Pages;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoGallery.Tests;

/// <summary>
/// SAMPLES-PLAN §4.6: a retarget carries each widget on from the value on screen, and the live feed runs for hundreds
/// of frames without drifting from the frame clock or leaving animations behind.
/// </summary>
public sealed class DashboardPageTests
{
    private const double FeedInterval = 800;
    private const int RevealFrames = 140;

    [AvaloniaFact]
    public void ARetargetMidFlightCarriesEveryWidgetOnFromWhereItWas()
    {
        var page = new DashboardPage();
        var window = SceneHarness.Show(page, 1100, 900);
        var button = page.GetControl<Button>("NewData");
        var counter = Readouts(page)[0];
        var bar = (Border)page.GetControl<StackPanel>("BarField").Children[0];
        var spark = page.GetControl<Path>("Spark");
        var plot = page.GetControl<Canvas>("Plot");

        TestFrames.Tick(RevealFrames);
        var scale = bar.Height / Datasets.Bars[0][0];
        double[] start = [SceneHarness.Number(counter), bar.Height, spark.Data!.Bounds.Y];

        SceneHarness.Click(window, button);
        TestFrames.Tick(12);
        double[] before = [SceneHarness.Number(counter), bar.Height, spark.Data!.Bounds.Y];

        SceneHarness.Click(window, button);
        TestFrames.Tick(1);
        double[] after = [SceneHarness.Number(counter), bar.Height, spark.Data!.Bounds.Y];

        var metric = Datasets.Metrics[0];
        var top = plot.Height - (Datasets.Sparklines[2].Max() / 100 * plot.Height);
        var bottom = plot.Height - (Datasets.Sparklines[2].Min() / 100 * plot.Height);
        double[] landing = [metric.Readings[2], Datasets.Bars[2][0] * scale, top];
        string[] names = ["the counter", "the bar", "the sparkline"];
        for (var i = 0; i < names.Length; i++) AssertCarriesOn(names[i], start[i], before[i], after[i], landing[i]);

        TestFrames.Tick(90);
        Assert.Equal(Utils.RoundPad(metric.Readings[2], metric.Decimals), counter.Text);
        Assert.Equal(landing[1], bar.Height, 1);
        var drawn = spark.Data!.Bounds;
        // MorphTo resamples both contours at even distances, so a peak between two samples is missed by a pixel or so.
        Assert.True(
            Math.Abs(drawn.Y - top) < 3 && Math.Abs(drawn.Bottom - bottom) < 3,
            $"the sparkline settled on {drawn} instead of the dataset's {top}..{bottom}");

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheLiveFeedHoldsItsTimeAndLeavesNoAnimationsBehind()
    {
        var page = new DashboardPage();
        var window = SceneHarness.Show(page, 1100, 900);
        TestFrames.Tick(RevealFrames);
        SceneHarness.Click(window, page.GetControl<ToggleSwitch>("Live"));

        var feed = page.Feed!;
        Assert.False(feed.Paused);
        var widgets = Widgets(page);

        TestFrames.Tick(40);
        var opening = feed.CurrentTime;
        var counts = new List<int>();
        for (var i = 0; i < 8; i++)
        {
            TestFrames.Tick(45);
            counts.Add(LiveAnimations(widgets));
        }

        Assert.Equal(8 * 45 * 16, feed.CurrentTime - opening, 1);
        Assert.Equal((int)(feed.CurrentTime / FeedInterval), feed.CurrentIteration);
        // A round is seven animations — one per counter, one each for the bars, the arcs and the sparkline — and the
        // round a push replaces is retired rather than left ticking, so two rounds is the most that can be alive.
        Assert.All(counts, count => Assert.InRange(count, 1, 14));

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheRevealAndEachPushLandAtOnce()
    {
        TestFrames.Install();
        using var reduced = ReducedMotion.On();
        Assert.True(Presto.Defaults.ReducedMotion);
        var page = new DashboardPage();
        var window = SceneHarness.Show(page, 1100, 900, frames: 2);
        TestFrames.Tick();
        var counter = Readouts(page)[0];
        var bar = (Border)page.GetControl<StackPanel>("BarField").Children[^1];
        var metric = Datasets.Metrics[0];
        Assert.Equal(Utils.RoundPad(metric.Readings[0], metric.Decimals), counter.Text);
        Assert.Equal(Datasets.Bars[0][^1] / 100 * 168, bar.Height, 1);

        SceneHarness.Click(window, page.GetControl<Button>("NewData"));
        TestFrames.Tick();

        Assert.Equal(Utils.RoundPad(metric.Readings[1], metric.Decimals), counter.Text);
        Assert.Equal(Datasets.Bars[1][^1] / 100 * 168, bar.Height, 1);
        SceneHarness.Close(window);
    }

    private static void AssertCarriesOn(string what, double start, double before, double after, double landing)
    {
        Assert.True(
            Math.Abs(before - start) > Math.Abs(landing - before) / 8,
            $"{what} had not moved far enough off {start} for a retarget to be told from a restart: {before}");
        Assert.True(
            Math.Abs(after - before) < Math.Abs(landing - before) / 4,
            $"{what} jumped from {before} to {after} instead of carrying on towards {landing}");
        Assert.True(
            Math.Abs(after - start) > Math.Abs(after - before),
            $"{what} went back towards {start} rather than on from {before}");
    }

    private static int LiveAnimations(IReadOnlyList<object> targets) =>
        targets.SelectMany(target => Presto.GetAnimations(target)).Distinct().Count();

    private static TextBlock[] Readouts(Visual page) =>
        [.. page.GetLogicalDescendants().OfType<TextBlock>().Where(text => text.Classes.Contains("readout"))];

    private static object[] Widgets(Control page) =>
    [
        .. Readouts(page),
        .. page.GetControl<StackPanel>("BarField").Children,
        .. page.GetLogicalDescendants().OfType<Path>(),
    ];
}
