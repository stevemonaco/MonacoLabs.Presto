using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
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
        Assert.Equal("20:00", window.GetVisualDescendants().OfType<SplitFlap>().Single().Text);
        var icon = window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p => p.Name == "PlayPause");
        Assert.Equal(TempoMotion.PauseIcon.Bounds.Left, icon.Data!.Bounds.Left, 1);
        window.Close();
        await vm.FlushAsync();
    }

    private static List<Border> Bars(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("bar"))];
}
