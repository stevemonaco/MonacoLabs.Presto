using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using Tempo.Model;
using Tempo.Motion;
using Tempo.ViewModels;
using Tempo.Views;

namespace Tempo.Tests;

public sealed class MainWindowTests : TempoTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADayRunsFromTheIntroThroughTheTasksIntoABreak(bool reduced)
    {
        var (window, vm) = await ShowIntroAsync(Task.CompletedTask, reduced);
        TickUntilShell(window, 4000);
        TestFrames.Tick(60);

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
        var drawer = window.GetVisualDescendants().OfType<Drawer>().Single();
        Assert.False(drawer.IsOpen);
        Assert.Equal(0, drawer.Position.Value);

        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheSampleDataSurvivesAFilterASessionAStatsVisitAClearAndARestore(bool reduced)
    {
        var (window, vm) = await ShowIntroAsync(Task.CompletedTask, reduced, firstRun: true);
        TickUntilShell(window, 4000);
        TestFrames.Tick(60);
        var overlay = OverlayLayer.GetOverlayLayer(window)!;
        var overlays = overlay.Children.Count;
        Assert.Equal((12, 4), (vm.Tasks.Count, vm.DoneTasks.Count));
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

        var label = Find<TextBlock>(window, "StartLabel");
        vm.Focus.StartPauseCommand.Execute(null);
        Time.Advance(TimeSpan.FromSeconds(3));
        Ticks.Raise();
        TestFrames.Tick(60);
        Assert.Equal("24:57", Find<TextBlock>(window, "Digits").Text);
        Assert.False(label.IsVisible);
        vm.Focus.StartPauseCommand.Execute(null);
        TestFrames.Tick(60);
        Assert.Equal("Resume", label.Text);

        vm.CurrentView = AppView.Stats;
        TestFrames.Tick(100);
        var cells = window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("heat")).ToList();
        Assert.Equal(vm.Stats.Heatmap.Count, cells.Count);
        Assert.All(cells, c => Assert.Equal(1, c.Opacity));
        var segments = window.GetVisualDescendants().OfType<Ellipse>().Where(e => e.Classes.Contains("donut-segment"));
        Assert.Equal(vm.Stats.Segments.Select(s => s.End), segments.Select(s => ((double[])PrestoAvalonia.Get(s, "Draw")!)[1]));
        vm.CurrentView = AppView.Focus;
        TestFrames.Tick(60);
        Assert.Equal("24:57", Find<TextBlock>(window, "Digits").Text);

        OpenConfirm(vm, vm.RequestClearCommand);
        HoldToConfirm(window);
        TestFrames.Tick(120);
        Assert.Equal((0, 0, 0), (vm.Tasks.Count, vm.DoneTasks.Count, vm.Sessions.Count));
        Assert.Equal("25:00", Find<TextBlock>(window, "Digits").Text);

        OpenConfirm(vm, vm.RequestRestoreCommand);
        HoldToConfirm(window);
        TestFrames.Tick(60);
        Assert.Equal((12, 4), (vm.Tasks.Count, vm.DoneTasks.Count));
        Assert.NotEmpty(vm.Sessions);
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

    private static void AssertRowsWhole(Window window)
    {
        var rows = Rows(window).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal((1, true), (row.Opacity, row.RenderTransform?.Value.IsIdentity ?? true)));
    }
}
