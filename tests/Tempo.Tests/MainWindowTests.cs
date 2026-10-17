using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
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

namespace Tempo.Tests;

public sealed class MainWindowTests : TempoTestBase
{
    public static TheoryData<int, int, bool> Sizes => new() { { 1100, 720, false }, { 1100, 720, true }, { 360, 780, false }, { 360, 780, true } };

    [AvaloniaTheory]
    [MemberData(nameof(Sizes))]
    public async Task ADayRunsFromTheIntroThroughTheTasksIntoABreak(int width, int height, bool reduced)
    {
        var (window, vm) = await ShowIntroAsync(Task.CompletedTask, reduced, size: new Size(width, height));
        TickUntilShell(window, 4000);
        TestFrames.Tick(60);
        Assert.Equal(width < 720, vm.IsCompact);
        Assert.Equal(vm.IsCompact, window.GetVisualDescendants().OfType<ShellView>().Single().Classes.Contains("compact"));
        Assert.Equal(vm.IsCompact, Find<ListBox>(window, "BottomNav").IsEffectivelyVisible);
        Assert.Equal(!vm.IsCompact, Find<ListBox>(window, "Switch").IsEffectivelyVisible);
        Assert.Equal(!vm.IsCompact, Find<TasksView>(window, "TasksPane").IsEffectivelyVisible);
        AssertHandoffLanded(window, vm);
        Navigate(window, vm, AppView.Tasks);

        foreach (var title in new[] { "Write", "Review", "Ship" })
        {
            vm.NewTaskTitle = title;
            vm.AddTaskCommand.Execute(null);
            TestFrames.Tick(10);
        }
        var (write, review, ship) = (vm.Tasks[0], vm.Tasks[1], vm.Tasks[2]);
        TestFrames.Tick(30);

        vm.OpenDetailCommand.Execute(write);
        TestFrames.Tick(30);
        vm.IsDetailOpen = false;
        TestFrames.Tick(30);

        review.IsDone = true;
        TestFrames.Tick(10);
        Time.Advance(MainViewModel.DoneMoveDelay);
        TestFrames.Tick(30);

        vm.SelectedTask = ship;
        vm.DeleteSelectedCommand.Execute(null);
        TestFrames.Tick(30);
        vm.SelectedTask = write;
        Navigate(window, vm, AppView.Focus);

        vm.Focus.StartPauseCommand.Execute(null);
        Time.Advance(TimeSpan.FromMinutes(vm.WorkMinutes));
        Ticks.Raise();
        TestFrames.Tick(120);

        Assert.Equal([write], vm.Tasks);
        Assert.Equal([review], vm.DoneTasks);
        Assert.Equal(1, write.CompletedSessions);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("task-row") && b.DataContext == ship);
        Assert.Empty(OverlayLayer.GetOverlayLayer(window)!.GetVisualDescendants().OfType<Image>());
        Assert.Equal(FocusMode.ShortBreak, vm.Focus.Mode);
        var accent = window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("accent-bg"));
        Assert.Equal(TempoMotion.ShortBreakAccent, Assert.IsAssignableFrom<ISolidColorBrush>(accent.Background).Color);
        var detail = Find<Sheet>(window, vm.IsCompact ? "DetailSheet" : "DetailDrawer");
        Assert.False(detail.IsOpen);
        Assert.Equal(0, detail.Position.Value);

        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [MemberData(nameof(Sizes))]
    public async Task TheSampleDataSurvivesAFilterASessionAStatsVisitAClearAndARestore(int width, int height, bool reduced)
    {
        var (window, vm) = await ShowIntroAsync(Task.CompletedTask, reduced, firstRun: true, new Size(width, height));
        TickUntilShell(window, 4000);
        TestFrames.Tick(60);
        AssertHandoffLanded(window, vm);
        var overlay = OverlayLayer.GetOverlayLayer(window)!;
        var overlays = overlay.Children.Count;
        Assert.Equal((12, 4), (vm.Tasks.Count, vm.DoneTasks.Count));
        Navigate(window, vm, AppView.Tasks);
        AssertRowsWhole(window);

        var chips = Find<ListBox>(window, "TagChips");
        chips.SelectedIndex = 1 + (int)TaskTag.Code;
        TestFrames.Tick(40);
        Assert.Equal(vm.Tasks.Where(t => t.Tag == TaskTag.Code), vm.VisibleTasks);
        Assert.InRange(vm.VisibleTasks.Count, 1, 11);
        chips.SelectedIndex = 0;
        TestFrames.Tick(90);
        Assert.Equal(vm.Tasks, vm.VisibleTasks);
        AssertRowsWhole(window);

        Navigate(window, vm, AppView.Focus);
        var label = Find<TextBlock>(window, "StartLabel");
        vm.Focus.StartPauseCommand.Execute(null);
        Time.Advance(TimeSpan.FromSeconds(3));
        Ticks.Raise();
        TestFrames.Tick(60);
        Assert.Equal("24:57", ClockText(window));
        Assert.False(label.IsVisible);
        vm.Focus.StartPauseCommand.Execute(null);
        TestFrames.Tick(60);
        Assert.Equal("Resume", label.Text);

        Navigate(window, vm, AppView.Stats);
        TestFrames.Tick(40);
        var cells = window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("heat")).ToList();
        Assert.Equal(vm.Stats.Heatmap.Count, cells.Count);
        Assert.All(cells, c => Assert.Equal(1, c.Opacity));
        var segments = window.GetVisualDescendants().OfType<Ellipse>().Where(e => e.Classes.Contains("donut-segment"));
        Assert.Equal(vm.Stats.Segments.Select(s => s.End), segments.Select(s => ((double[])PrestoAvalonia.Get(s, "Draw")!)[1]));
        Navigate(window, vm, AppView.Focus);
        Assert.Equal("24:57", ClockText(window));

        OpenConfirm(vm, vm.RequestClearCommand);
        HoldToConfirm(window);
        TestFrames.Tick(120);
        Assert.Equal((0, 0, 0), (vm.Tasks.Count, vm.DoneTasks.Count, vm.Sessions.Count));
        Assert.Equal("25:00", ClockText(window));

        OpenConfirm(vm, vm.RequestRestoreCommand);
        HoldToConfirm(window);
        TestFrames.Tick(60);
        Assert.Equal((12, 4), (vm.Tasks.Count, vm.DoneTasks.Count));
        Assert.NotEmpty(vm.Sessions);
        Navigate(window, vm, AppView.Tasks);
        AssertRowsWhole(window);
        Assert.False(Find<Modal>(window, "ConfirmModal").IsOpen);
        Assert.Empty(window.GetVisualDescendants().OfType<FlyoutPresenter>());
        Assert.Equal(overlays, overlay.Children.Count);
        Assert.Empty(overlay.GetVisualDescendants().OfType<Image>());

        window.Close();
        await vm.FlushAsync();
        var saved = await new TempoStore(Root).LoadAsync();
        Assert.Equal((12, vm.Sessions.Count), (saved.Tasks.Count(t => !t.IsDone), saved.Sessions.Count));
    }

    [AvaloniaFact]
    public async Task TheMotionSettingReachesTheFlagThroughTheWindowBinding()
    {
        var (window, vm) = await ShowAsync(reduced: false);

        vm.Motion = MotionPreference.Reduced;
        Assert.True(Presto.Defaults.ReducedMotion);
        TestApp.SystemReducedMotion = true;
        vm.Motion = MotionPreference.System;
        Assert.True(Presto.Defaults.ReducedMotion);
        vm.Motion = MotionPreference.Full;
        Assert.False(Presto.Defaults.ReducedMotion);
        window.Close();
        await vm.FlushAsync();

        Assert.Equal(MotionPreference.Full, (await new TempoStore(Root).LoadAsync()).Settings.Motion);
    }

    [AvaloniaFact]
    public async Task AnOsChangeUnderSystemRedriftsTheBackdrop()
    {
        var (window, vm) = await ShowAsync(reduced: false);
        vm.Motion = MotionPreference.System;
        var shell = window.GetVisualDescendants().OfType<ShellView>().Single();
        var drift = Drift(shell);
        Assert.NotNull(drift);

        TestApp.SystemReducedMotion = true;
        PrestoAvalonia.RefreshSystemReducedMotion();

        Assert.True(Presto.Defaults.ReducedMotion);
        Assert.NotSame(drift, Drift(shell));
        window.Close();
        await vm.FlushAsync();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_drift")]
    private static extern ref CompositorAnimation? Drift(ShellView shell);

    // Wide has no Tasks page: the list sits beside every page.
    private static void Navigate(Window window, MainViewModel vm, AppView view)
    {
        if (view == AppView.Tasks && !vm.IsCompact)
            return;
        Find<ListBox>(window, vm.IsCompact ? "BottomNav" : "Switch").SelectedItem = view;
        TestFrames.Tick(60);
        Assert.Equal(view, vm.CurrentView);
    }

    private static void AssertHandoffLanded(Window window, MainViewModel vm)
    {
        Assert.Empty(window.GetVisualDescendants().OfType<IntroView>());
        Assert.Empty(OverlayLayer.GetOverlayLayer(window)!.GetVisualDescendants().OfType<Image>());
        var wordmark = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("wordmark"));
        var ring = Find<ProgressRing>(window, "Ring");
        Assert.Equal("ring", vm.Focus.RingKey);
        foreach (var landed in new Control[] { wordmark, ring })
        {
            var slot = new Rect(landed.TranslatePoint(default, window)!.Value, landed.Bounds.Size);
            Assert.True(new Rect(window.Bounds.Size).Contains(slot), $"{landed} at {slot}");
            Assert.True(landed.IsEffectivelyVisible);
            Assert.Equal(1, landed.Opacity);
        }
    }

    private static void AssertRowsWhole(Window window)
    {
        var rows = Rows(window).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal((1, true), (row.Opacity, row.RenderTransform?.Value.IsIdentity ?? true)));
    }
}
