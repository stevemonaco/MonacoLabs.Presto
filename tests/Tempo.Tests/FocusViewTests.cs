using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Controls;
using Tempo.Model;
using Tempo.Motion;
using Tempo.ViewModels;
using Tempo.Views;
using PrestoMotion = MonacoLabs.Presto.Motion;

namespace Tempo.Tests;

public sealed class FocusViewTests : TempoTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARunningSessionCountsDownTheDigitsAndSweepsTheRing(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var view = Assert.Single(window.GetVisualDescendants().OfType<FocusView>());
        var ring = Ring(window);

        Assert.Equal("25:00", Digits(window).Text);
        Assert.Equal("Focus", Assert.IsType<string>(window.GetVisualDescendants().OfType<TransitioningContentControl>().Single(t => t.Classes.Contains("mode")).Content));
        vm.Focus.StartPauseCommand.Execute(null);
        Time.Advance(TimeSpan.FromMinutes(5));
        Ticks.Raise();
        TestFrames.Tick(30);

        Assert.Same(vm.Focus, view.DataContext);
        Assert.Equal("20:00", Digits(window).Text);
        Assert.Equal(.2, ring.Value, 3);
        Assert.Contains("Running", PrestoMotion.GetState(window.GetVisualDescendants().OfType<ShellView>().Single())!.ToString());
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletingASessionRaisesTheSummaryAndBurstsOnlyAtFullMotion(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced, "Write proposal");
        vm.SelectedTask = vm.Tasks[0];

        vm.Focus.StartPauseCommand.Execute(null);
        Time.Advance(TimeSpan.FromMinutes(25));
        Ticks.Raise();
        TestFrames.Tick(2);

        Assert.Equal(!reduced, OverlayLayer.GetOverlayLayer(window)!.Children.OfType<CompositorCanvas>().Any());
        TestFrames.Tick(60);
        var summary = Assert.Single(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Classes.Contains("summary"));
        Assert.Equal("25 minutes on Write proposal", summary.Text);
        Assert.Equal(1, summary.Opacity);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ABreakRecolorsTheRingDimsTheTasksAndShowsTheBreathingGuide(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var guide = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("breathe"));
        Assert.False(guide.IsVisible);

        vm.Focus.SkipCommand.Execute(null);
        TestFrames.Tick(60);

        Assert.Equal(FocusMode.ShortBreak, vm.Focus.Mode);
        Assert.Equal(TempoMotion.ShortBreakAccent, Assert.IsAssignableFrom<ISolidColorBrush>(Ring(window).Fill).Color);
        var tasks = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("break-dim"));
        Assert.Equal(.45, tasks.Opacity, 2);
        Assert.True(guide.IsVisible);
        Assert.NotNull(Animate.GetOnAttached(guide));
        window.Close();
        await vm.FlushAsync();
    }

    private static ProgressRing Ring(Window window) => window.GetVisualDescendants().OfType<ProgressRing>().Single(r => r.Name == "Ring");

    private static SplitFlap Digits(Window window) => window.GetVisualDescendants().OfType<SplitFlap>().Single();
}
