using Tempo.Model;
using Tempo.ViewModels;

namespace Tempo.Tests;

public sealed class MainViewModelTests : TempoTestBase
{
    [Fact]
    public async Task AddingATaskAppendsItAndClearsTheBox()
    {
        var vm = await LoadAsync();
        vm.NewTaskTitle = "  Write proposal ";
        vm.AddTaskCommand.Execute(null);

        Assert.Equal("", vm.NewTaskTitle);
        vm.NewTaskTitle = " ";
        vm.AddTaskCommand.Execute(null);

        var task = Assert.Single(vm.Tasks);
        Assert.Equal("Write proposal", task.Title);
        Assert.Equal([false], task.Dots.Select(d => d.IsFilled));
    }

    [Fact]
    public async Task CompletingATaskStrikesItAndMovesItToDone()
    {
        var vm = await LoadWithTasksAsync("A", "B");
        var a = vm.Tasks[0];

        a.IsDone = true;

        Assert.Equal(1, a.StrikeProgress);
        Assert.Equal(["A", "B"], vm.Tasks.Select(t => t.Title));
        Time.Advance(MainViewModel.DoneMoveDelay);
        Assert.Equal(["B"], vm.Tasks.Select(t => t.Title));
        Assert.Equal([a], vm.DoneTasks);
        await vm.FlushAsync();
    }

    [Fact]
    public async Task UndoRestoresADeletedTaskAtItsIndex()
    {
        var vm = await LoadWithTasksAsync("A", "B", "C");
        var b = vm.Tasks[1];
        vm.SelectedTask = b;
        TaskItemViewModel? deleted = null;
        vm.TaskDeleted += (_, task) => deleted = task;

        vm.DeleteSelectedCommand.Execute(null);

        Assert.Same(b, deleted);
        Assert.Null(vm.SelectedTask);
        Assert.Equal(["A", "C"], vm.Tasks.Select(t => t.Title));

        vm.UndoDeleteCommand.Execute(null);

        Assert.Equal(["A", "B", "C"], vm.Tasks.Select(t => t.Title));
    }

    [Fact]
    public async Task AReorderInTheCollectionPersists()
    {
        var vm = await LoadWithTasksAsync("A", "B", "C");
        vm.Tasks[1].IsDone = true;
        Time.Advance(MainViewModel.DoneMoveDelay);
        vm.Tasks.Move(0, 1);
        await vm.FlushAsync();

        var reloaded = await LoadAsync();

        Assert.Equal(["C", "A"], reloaded.Tasks.Select(t => t.Title));
        Assert.Equal(["B"], reloaded.DoneTasks.Select(t => t.Title));
    }

    [Fact]
    public async Task ACompletedWorkSessionAddsADotToTheBoundTaskAndLogsIt()
    {
        var vm = await LoadWithTasksAsync("Write proposal", "Other");
        var task = vm.Tasks[0];
        vm.SelectedTask = task;
        SessionCompletedEventArgs? completed = null;
        vm.Focus.SessionCompleted += (_, e) => completed = e;

        Assert.Equal("25:00", vm.Focus.RemainingText);
        vm.Focus.StartPauseCommand.Execute(null);
        vm.SelectedTask = vm.Tasks[1];
        Time.Advance(TimeSpan.FromMinutes(10.5));
        Ticks.Raise();

        Assert.Equal("14:30", vm.Focus.RemainingText);
        Assert.Equal(0.42, vm.Focus.Progress, 6);
        Assert.True(vm.Focus.IsRunning);
        Assert.Same(task, vm.Focus.ActiveTask);

        Time.Advance(TimeSpan.FromMinutes(14.5));
        Ticks.Raise();

        Assert.Equal(1, task.CompletedSessions);
        Assert.Equal([true], task.Dots.Select(d => d.IsFilled));
        Assert.Equal("Write proposal", completed?.TaskTitle);
        Assert.Equal(TimeSpan.FromMinutes(25), completed!.Record.Focused);
        Assert.Equal([completed.Record], vm.Sessions);
        Assert.Equal(FocusMode.ShortBreak, vm.Focus.Mode);
        Assert.True(vm.Focus.IsBreak);
        Assert.Same(vm.Tasks[1], vm.Focus.ActiveTask);

        await vm.FlushAsync();
        var reloaded = await LoadAsync();
        Assert.Equal(1, reloaded.Tasks[0].CompletedSessions);
        Assert.Single(reloaded.Sessions);
    }

    [Fact]
    public async Task StatsAggregateTheSessionLog()
    {
        var now = Time.GetUtcNow();
        var minutes = TimeSpan.FromMinutes(25);
        await new TempoStore(Root).SaveAsync(new TempoData
        {
            Sessions =
            [
                new(null, FocusMode.Focus, now.AddHours(-2), minutes, true),
                new(null, FocusMode.Focus, now.AddHours(-1), TimeSpan.FromMinutes(10), false),
                new(null, FocusMode.ShortBreak, now.AddMinutes(-30), TimeSpan.FromMinutes(5), true),
                new(null, FocusMode.Focus, now.AddDays(-1), minutes, true),
                new(null, FocusMode.Focus, now.AddDays(-1), minutes, true),
                new(null, FocusMode.Focus, now.AddDays(-3), minutes, true),
                new(null, FocusMode.Focus, now.AddDays(-9), minutes, true),
            ],
        });

        var stats = (await LoadAsync()).Stats;

        Assert.Equal((" 35", " 1"), (stats.TodayMinutesText, stats.TodaySessionsText));
        Assert.Equal([0, 0, 0, 25, 0, 50, 35], stats.Days.Select(d => d.Minutes));
        Assert.Equal([0, 0, 0, 0.5, 0, 1, 0.7], stats.Days.Select(d => Math.Round(d.Height, 6)));
        Assert.Equal(Time.GetLocalNow().ToString("ddd"), stats.Days[^1].Label);
        Assert.Equal(2, stats.Streak);
    }

    [Fact]
    public async Task TheStreakHoldsThroughTodayUntilASessionLands()
    {
        var vm = await LoadAsync();
        vm.Sessions.Add(new(null, FocusMode.Focus, Time.GetUtcNow().AddDays(-1), TimeSpan.FromMinutes(25), true));

        Assert.Equal(1, vm.Stats.Streak);
        Assert.Equal(" 0", vm.Stats.TodaySessionsText);
    }
}
