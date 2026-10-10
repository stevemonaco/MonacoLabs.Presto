using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
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
}
