using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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

        Assert.Equal("25:00", ClockText(window));
        Assert.Equal("Focus", Assert.IsType<string>(window.GetVisualDescendants().OfType<TransitioningContentControl>().Single(t => t.Classes.Contains("mode")).Content));
        vm.Focus.StartPauseCommand.Execute(null);
        Time.Advance(TimeSpan.FromMinutes(5));
        Ticks.Raise();
        TestFrames.Tick(60);

        Assert.Same(vm.Focus, view.DataContext);
        Assert.Equal("20:00", ClockText(window));
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

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyTheChangedDigitsMorphIntoEachSecondAndAcrossASegmentEndWhileTheRingPulsesTheLastTen(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var ring = Ring(window);
        vm.Focus.StartPauseCommand.Execute(null);
        Time.Advance(TimeSpan.FromSeconds(25 * 60 - 12));
        Ticks.Raise();
        TestFrames.Tick(60);
        Assert.Equal("00:12", ClockText(window));
        Assert.DoesNotContain("final", ring.Classes);
        var blocks = ClockBlocks(window);
        var bounds = blocks.Select(b => b.Bounds).ToList();

        for (var second = 11; second >= -1; second--)
        {
            var before = ClockText(window);
            Time.Advance(TimeSpan.FromSeconds(1));
            Ticks.Raise();
            TestFrames.Tick(8);
            var final = second is >= 1 and <= 10;
            Assert.Equal(final, ring.Classes.Contains("final"));
            Assert.Equal(final, reduced ? ring.Opacity < .9 : ring.Effect is DropShadowEffect { BlurRadius: > 0 });
            var after = vm.Focus.RemainingText;
            for (var i = 0; i < blocks.Count; i++)
            {
                var changed = before[i] != after[i];
                Assert.Equal(changed && !reduced, blocks[i].Inlines is { Count: > 0 });
                if (changed && !reduced)
                    continue;
                Assert.Equal(after[i].ToString(), blocks[i].Text);
                Assert.Equal(bounds[i], blocks[i].Bounds);
                Assert.Equal(1, blocks[i].Opacity);
            }
            TestFrames.Tick(52);
            Assert.Equal(after, ClockText(window));
            Assert.Equal(bounds, blocks.Select(b => b.Bounds));
        }

        Assert.Equal(FocusMode.ShortBreak, vm.Focus.Mode);
        Assert.All(blocks, b => Assert.True(b.Inlines is null or { Count: 0 }));
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheDigitRowRebuildsWhenTheMinutesLoseADigitMidMorph(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var overlay = OverlayLayer.GetOverlayLayer(window)!;
        var before = overlay.Children.Count;
        vm.WorkMinutes = 120;
        vm.Focus.StartPauseCommand.Execute(null);
        vm.Focus.AbandonCommand.Execute(null);
        TestFrames.Tick(60);
        Assert.Equal(6, ClockBlocks(window).Count);
        Assert.Equal("120:00", ClockText(window));

        vm.Focus.StartPauseCommand.Execute(null);
        Time.Advance(TimeSpan.FromSeconds(20 * 60));
        Ticks.Raise();
        TestFrames.Tick(5);
        Assert.Equal("100:00", vm.Focus.RemainingText);
        Time.Advance(TimeSpan.FromSeconds(1));
        Ticks.Raise();
        TestFrames.Tick(60);

        Assert.Equal(5, ClockBlocks(window).Count);
        Assert.Equal("99:59", ClockText(window));
        Assert.All(ClockBlocks(window), b => Assert.True(b.Inlines is null or { Count: 0 }));
        Assert.Equal(before, overlay.Children.Count);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SwitchingToStatsMidMorphLeavesNothingInTheOverlay(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var overlay = OverlayLayer.GetOverlayLayer(window)!;
        var before = overlay.Children.Count;
        vm.Focus.StartPauseCommand.Execute(null);
        Time.Advance(TimeSpan.FromSeconds(1));
        Ticks.Raise();
        TestFrames.Tick(5);

        vm.CurrentView = AppView.Stats;
        TestFrames.Tick(60);

        Assert.Equal(before, overlay.Children.Count);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartPauseShrinksToACircleWhileRunningAndReadsResumeWhenPaused(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var button = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "StartPause");
        var label = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "StartLabel");
        Assert.Equal("Start focus", label.Text);
        var pill = button.Bounds.Width;
        Assert.True(pill > 120, $"Pill width {pill}.");

        vm.Focus.StartPauseCommand.Execute(null);
        TestFrames.Tick(4);
        Assert.False(label.IsVisible);
        if (reduced)
            Assert.Equal(64, button.Bounds.Width, 1);
        else
            Assert.InRange(button.Bounds.Width, 65, pill - 1);
        TestFrames.Tick(60);
        Assert.Equal(64, button.Bounds.Width, 1);
        Assert.Equal(32, button.CornerRadius.TopLeft);

        vm.Focus.StartPauseCommand.Execute(null);
        TestFrames.Tick(60);
        Assert.Equal("Resume", label.Text);
        Assert.True(label.IsVisible);
        Assert.InRange(button.Bounds.Width, 65, pill - 1);

        vm.Focus.StartPauseCommand.Execute(null);
        TestFrames.Tick(60);
        Assert.Equal(64, button.Bounds.Width, 1);

        vm.Focus.StartPauseCommand.Execute(null);
        vm.Focus.SkipCommand.Execute(null);
        TestFrames.Tick(60);
        Assert.Equal("Start break", label.Text);
        Assert.True(label.Inlines is null or { Count: 0 });
        Assert.True(button.Bounds.Width > 120);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LettingGoOfAbandonEarlyShakesItAndKeepsTheSession(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var hold = window.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "AbandonHold");
        var ring = hold.GetVisualDescendants().OfType<ProgressRing>().Single();
        vm.Focus.StartPauseCommand.Execute(null);
        ring.Focus();

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        TestFrames.Tick(10);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        TestFrames.Tick(6);

        Assert.True(vm.Focus.IsRunning);
        Assert.True(reduced ? hold.Opacity < 1 : hold.RenderTransform is { Value.M31: not 0 });
        TestFrames.Tick(60);
        Assert.Equal(1, hold.Opacity);
        Assert.Equal(0, ring.Value);
        window.Close();
        await vm.FlushAsync();
    }

    private static ProgressRing Ring(Window window) => window.GetVisualDescendants().OfType<ProgressRing>().Single(r => r.Name == "Ring");
}
