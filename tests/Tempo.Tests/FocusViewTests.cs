using Avalonia;
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
        Assert.Contains("IsRunning", PrestoMotion.GetState(window.GetVisualDescendants().OfType<ShellView>().Single())!.ToString());
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
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartingTheClockBreathesTheRingAtFullMotionWhileTheModeKeepsItsFill(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var ring = Ring(window);
        vm.Focus.SkipCommand.Execute(null);
        TestFrames.Tick(60);
        Assert.Equal(1, Convert.ToDouble(Presto.Get(ring, "Scale", withUnit: false)), 3);

        vm.Focus.StartPauseCommand.Execute(null);
        TestFrames.Tick(60);

        Assert.Equal(!reduced, Convert.ToDouble(Presto.Get(ring, "Scale", withUnit: false)) > 1.001);
        Assert.Equal(TempoMotion.ShortBreakAccent, Assert.IsAssignableFrom<ISolidColorBrush>(ring.Fill).Color);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SwitchingMotionMidRunRestsTheRingWhenReducedAndBreathesAgainWhenFull(bool startReduced)
    {
        var (window, vm) = await ShowAsync(startReduced);
        var ring = Ring(window);
        vm.Focus.StartPauseCommand.Execute(null);
        TestFrames.Tick(60);

        vm.Motion = MotionPreference.Reduced;
        TestFrames.Tick(200);
        Assert.Equal(1, Convert.ToDouble(Presto.Get(ring, "Scale", withUnit: false)), 3);

        vm.Motion = MotionPreference.Full;
        TestFrames.Tick(60);
        Assert.True(Convert.ToDouble(Presto.Get(ring, "Scale", withUnit: false)) > 1.001);
        Assert.Equal(TempoMotion.FocusAccent, Assert.IsAssignableFrom<ISolidColorBrush>(ring.Fill).Color);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheBreatheGuideIsPausedOutsideABreakAndPlaysDuringOne(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var guide = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("breathe"));
        TestFrames.Tick(2);
        Assert.Empty(Presto.GetAnimations(guide));

        vm.Focus.SkipCommand.Execute(null);
        TestFrames.Tick(40);

        Assert.True(guide.IsVisible);
        if (reduced)
        {
            Assert.Equal(1, guide.Opacity);
        }
        else
        {
            var scale = Presto.Get(guide, "Scale");
            TestFrames.Tick(5);
            Assert.False(Assert.Single(Presto.GetAnimations(guide)).Paused);
            Assert.NotEqual(scale, Presto.Get(guide, "Scale"));
        }

        vm.Focus.SkipCommand.Execute(null);
        TestFrames.Tick(2);
        Assert.Equal(FocusMode.Focus, vm.Focus.Mode);
        Assert.Empty(Presto.GetAnimations(guide));
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
        var block = ClockBlock(window);
        var bounds = block.Bounds;

        for (var second = 11; second >= -1; second--)
        {
            var before = ClockText(window)!;
            Time.Advance(TimeSpan.FromSeconds(1));
            Ticks.Raise();
            TestFrames.Tick(8);
            var final = second is >= 1 and <= 10;
            Assert.Equal(final, ring.Classes.Contains("final"));
            Assert.Equal(final, reduced ? ring.Opacity < .9 : ring.Effect is DropShadowEffect { BlurRadius: > 0 });
            var after = vm.Focus.RemainingText;
            var changed = Enumerable.Range(0, after.Length).Count(i => before[i] != after[i]);
            Assert.Equal(reduced ? 0 : changed, Pieces(block).Count());
            Assert.Equal(changed > 0 && !reduced, block.Inlines is { Count: > 0 });
            Assert.Equal(bounds, block.Bounds);
            Assert.Equal(1, block.Opacity);
            TestFrames.Tick(52);
            Assert.Empty(Pieces(block));
            Assert.True(block.Inlines is null or { Count: 0 });
            Assert.Equal(after, block.Text);
            Assert.Equal(bounds, block.Bounds);
        }

        Assert.Equal(FocusMode.ShortBreak, vm.Focus.Mode);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheCountdownMorphsFromThreeMinuteDigitsToTwoMidMorph(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var block = ClockBlock(window);
        vm.WorkMinutes = 120;
        vm.Focus.StartPauseCommand.Execute(null);
        vm.Focus.AbandonCommand.Execute(null);
        TestFrames.Tick(60);
        Assert.Equal("120:00", ClockText(window));

        vm.Focus.StartPauseCommand.Execute(null);
        Time.Advance(TimeSpan.FromSeconds(20 * 60));
        Ticks.Raise();
        TestFrames.Tick(5);
        Assert.Equal("100:00", vm.Focus.RemainingText);
        Time.Advance(TimeSpan.FromSeconds(1));
        Ticks.Raise();
        TestFrames.Tick(5);
        Assert.Equal(reduced ? 0 : 6, Pieces(block).Count());
        TestFrames.Tick(55);

        Assert.Same(block, ClockBlock(window));
        Assert.Equal("99:59", ClockText(window));
        Assert.True(block.Inlines is null or { Count: 0 });
        Assert.Empty(Pieces(block));
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SwitchingToStatsMidMorphLeavesNothingInTheAdornerLayer(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var layer = AdornerLayer.GetAdornerLayer(ClockBlock(window))!;
        var before = layer.Children.Count;
        vm.Focus.StartPauseCommand.Execute(null);
        Time.Advance(TimeSpan.FromSeconds(1));
        Ticks.Raise();
        TestFrames.Tick(5);
        Assert.Equal(reduced ? before : before + 1, layer.Children.Count);

        vm.CurrentView = AppView.Stats;
        TestFrames.Tick(60);

        Assert.Equal(before, layer.Children.Count);
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

    [AvaloniaTheory]
    [InlineData("hero-face")]
    [InlineData("skip-face")]
    [InlineData("TaskList")]
    public async Task SpaceStartsAndPausesWhicheverControlHasFocus(string focused)
    {
        var (window, vm) = await ShowAsync(reduced: true);
        var target = window.GetVisualDescendants().OfType<InputElement>()
            .First(c => c is Control control && (control.Name == focused || control.Classes.Contains(focused)));
        target.Focus();

        Space(window);
        Assert.True(vm.Focus.IsRunning);
        Space(window);
        Assert.False(vm.Focus.IsRunning);
        Assert.Equal(FocusMode.Focus, vm.Focus.Mode);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheDigitsHoldTheRingsCenterAndTheModeLabelStaysCentered(bool withTask)
    {
        var (window, vm) = await ShowSeededAsync(reduced: false);
        if (withTask)
            vm.SelectedTask = vm.Tasks[0];
        TestFrames.Tick(30);
        var dial = window.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "Dial");
        var digits = ClockBlock(window);
        Assert.Equal(withTask, vm.Focus.ActiveTask is not null);
        Assert.Equal(dial.Bounds.Height / 2, digits.TranslatePoint(new Point(0, digits.Bounds.Height / 2), dial)!.Value.Y, tolerance: 1);

        vm.Focus.SkipCommand.Execute(null);
        TestFrames.Tick(8);
        var mode = window.GetVisualDescendants().OfType<TransitioningContentControl>().Single(t => t.Classes.Contains("mode"));
        Assert.All(mode.GetVisualDescendants().OfType<TextBlock>(), label =>
            Assert.Equal(mode.Bounds.Width / 2, label.TranslatePoint(new Point(label.TextLayout.Width / 2, 0), mode)!.Value.X, tolerance: 1));
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartingPingsTheHaloAndTurnsTheIconIntoPause(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var halo = window.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "StartHalo");

        vm.Focus.StartPauseCommand.Execute(null);
        TestFrames.Tick(4);
        Assert.Equal(!reduced, halo.Opacity > 0);
        TestFrames.Tick(60);
        Assert.Equal(0, halo.Opacity);
        var icon = window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p => p.Name == "PlayPause");
        Assert.Equal(TempoMotion.PauseIcon.Bounds.Left, icon.Data!.Bounds.Left, 1);
        window.Close();
        await vm.FlushAsync();
    }

    private static void Space(Window window)
    {
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        TestFrames.Tick(2);
    }

    private static IEnumerable<Control> Pieces(TextBlock block) =>
        AdornerLayer.GetAdornerLayer(block)!.Children.Where(c => AdornerLayer.GetAdornedElement(c) == block).SelectMany(c => ((Canvas)c).Children);

    private static ProgressRing Ring(Window window) => window.GetVisualDescendants().OfType<ProgressRing>().Single(r => r.Name == "Ring");
}
