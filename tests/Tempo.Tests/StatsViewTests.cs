using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using Tempo.Model;
using Tempo.Motion;
using Tempo.ViewModels;
using Tempo.Views;

namespace Tempo.Tests;

public sealed class StatsViewTests : TempoTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SwitchingToStatsAndBackAttachesEachView(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        Assert.Single(window.GetVisualDescendants().OfType<FocusView>());
        Assert.Empty(window.GetVisualDescendants().OfType<StatsView>());

        vm.CurrentView = AppView.Stats;
        TestFrames.Tick(60);
        Assert.Single(window.GetVisualDescendants().OfType<StatsView>());
        Assert.Empty(window.GetVisualDescendants().OfType<FocusView>());
        Assert.Equal(AppView.Stats, window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "Switch").SelectedItem);

        vm.CurrentView = AppView.Focus;
        TestFrames.Tick(60);
        Assert.Single(window.GetVisualDescendants().OfType<FocusView>());
        Assert.Empty(window.GetVisualDescendants().OfType<StatsView>());
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheSevenBarsEndAtTheirNormalizedHeightsOnEveryEntry(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var today = Time.GetUtcNow();
        vm.Sessions.Add(new SessionRecord(null, FocusMode.Focus, today.AddDays(-1), TimeSpan.FromMinutes(50), true));
        vm.Sessions.Add(new SessionRecord(null, FocusMode.Focus, today, TimeSpan.FromMinutes(25), true));

        for (var entry = 0; entry < 2; entry++)
        {
            vm.CurrentView = AppView.Stats;
            TestFrames.Tick(10);
            var busiest = Bars(window)[5];
            Assert.True(reduced ? busiest.Opacity < 1 : busiest.RenderTransform!.Value.M22 < .5);
            TestFrames.Tick(80);

            var bars = Bars(window);
            Assert.Equal(7, bars.Count);
            for (var i = 0; i < 7; i++)
            {
                Assert.Equal(vm.Stats.Days[i].Height * StatsView.ChartHeight, bars[i].Bounds.Height, 3);
                Assert.Equal(1, bars[i].Opacity);
                Assert.Equal(1, bars[i].RenderTransform?.Value.M22 ?? 1, 3);
            }
            Assert.Equal([0, 0, 0, 0, 0, 1, .5], vm.Stats.Days.Select(d => d.Height));

            vm.CurrentView = AppView.Focus;
            TestFrames.Tick(60);
        }
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SwitchingMidSessionKeepsTheClockRunning(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        vm.Focus.StartPauseCommand.Execute(null);

        vm.CurrentView = AppView.Stats;
        TestFrames.Tick(30);
        Time.Advance(TimeSpan.FromMinutes(5));
        Ticks.Raise();
        vm.CurrentView = AppView.Focus;
        TestFrames.Tick(60);

        Assert.True(vm.Focus.IsRunning);
        Assert.Equal("20:00", window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "Digits").Text);
        var icon = window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p => p.Name == "PlayPause");
        Assert.Equal(TempoMotion.PauseIcon.Bounds.Left, icon.Data!.Bounds.Left, 1);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheHeatmapDonutAndBestDaySettleRevealed(bool reduced)
    {
        var (window, vm) = await ShowStatsAsync(reduced);
        TestFrames.Tick(10);
        var cells = Cells(window);
        Assert.Equal(vm.Stats.Heatmap.Count, cells.Count);
        Assert.True(cells[0].Opacity < 1);
        if (!reduced)
            Assert.True(cells[0].Opacity < cells[^1].Opacity && Drawn(Segments(window)[0])[1] < 1);
        TestFrames.Tick(90);

        Assert.All(Cells(window), c => Assert.Equal((1, 1), (c.Opacity, c.RenderTransform?.Value.M11 ?? 1)));
        Assert.Equal(vm.Stats.Segments.Select(s => s.End), Segments(window).Select(s => Drawn(s)[1]));
        Assert.All(Segments(window), s => Assert.Equal(1, s.Opacity));
        var bestDay = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "BestDay");
        Assert.Equal((true, 1, vm.Stats.BestDayText), (bestDay.IsEffectivelyVisible, bestDay.Opacity, bestDay.Text));
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SwitchingTheRangeEasesTheDonutToTheNewShares(bool reduced)
    {
        var (window, vm) = await ShowStatsAsync(reduced);
        TestFrames.Tick(90);
        var untagged = Segments(window)[^1];
        Assert.Equal(0, Drawn(untagged)[1]);

        window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "RangeSwitch").SelectedItem = StatsRange.Week;
        TestFrames.Tick(5);
        Assert.Equal(StatsRange.Week, vm.Stats.Range);
        if (reduced)
            Assert.Equal(2 / 3d, Drawn(untagged)[1], 6);
        else
            Assert.InRange(Drawn(untagged)[1], .001, 2 / 3d - .01);
        TestFrames.Tick(60);

        Assert.Equal(vm.Stats.Segments.Select(s => s.End), Segments(window).Select(s => Drawn(s)[1]));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "50m");
        window.Close();
        await vm.FlushAsync();
    }

    /// <summary>On Stats with 25 minutes on a Code task today and 50 untagged three days ago.</summary>
    private async Task<(MainWindow Window, MainViewModel ViewModel)> ShowStatsAsync(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced, "Fix the build");
        vm.Tasks[0].Tag = TaskTag.Code;
        vm.Sessions.Add(new SessionRecord(null, FocusMode.Focus, Time.GetUtcNow().AddDays(-3), TimeSpan.FromMinutes(50), true));
        vm.Sessions.Add(new SessionRecord(vm.Tasks[0].Id, FocusMode.Focus, Time.GetUtcNow(), TimeSpan.FromMinutes(25), true));
        vm.CurrentView = AppView.Stats;
        return (window, vm);
    }

    private static List<Border> Cells(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("heat"))];

    private static List<Ellipse> Segments(Window window) =>
        [.. window.GetVisualDescendants().OfType<Ellipse>().Where(e => e.Classes.Contains("donut-segment"))];

    private static double[] Drawn(Ellipse segment) => (double[])PrestoAvalonia.Get(segment, "Draw")!;

    private static List<Border> Bars(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("bar"))];
}
