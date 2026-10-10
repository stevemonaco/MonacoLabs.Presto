using System.Collections.Specialized;
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
    public async Task TheStartPauseLabelSaysWhatAPressDoesAndIsEmptyWhileRunning()
    {
        var vm = await LoadAsync();
        Assert.Equal("Start focus", vm.Focus.StartPauseLabel);

        vm.Focus.StartPauseCommand.Execute(null);
        Assert.Equal("", vm.Focus.StartPauseLabel);

        vm.Focus.StartPauseCommand.Execute(null);
        Assert.Equal("Resume", vm.Focus.StartPauseLabel);

        vm.Focus.SkipCommand.Execute(null);
        Assert.Equal(FocusMode.ShortBreak, vm.Focus.Mode);
        Assert.Equal("Start break", vm.Focus.StartPauseLabel);
    }

    [Fact]
    public async Task TheAddBoxHasTextOnlyForATitleEnterWouldAdd()
    {
        var vm = await LoadAsync();

        vm.NewTaskTitle = "  ";
        Assert.False(vm.HasNewTaskText);
        vm.NewTaskTitle = "Plan";
        Assert.True(vm.HasNewTaskText);
        vm.AddTaskCommand.Execute(null);
        Assert.False(vm.HasNewTaskText);
    }

    [Fact]
    public async Task TheFinalCountdownCoversTheLastTenSecondsOnlyWhileRunning()
    {
        var vm = await LoadAsync();
        vm.Focus.StartPauseCommand.Execute(null);
        Time.Advance(TimeSpan.FromSeconds(25 * 60 - 11));
        Ticks.Raise();
        Assert.False(vm.Focus.IsFinalCountdown);

        Time.Advance(TimeSpan.FromSeconds(1));
        Ticks.Raise();
        Assert.Equal("00:10", vm.Focus.RemainingText);
        Assert.True(vm.Focus.IsFinalCountdown);

        vm.Focus.StartPauseCommand.Execute(null);
        Assert.False(vm.Focus.IsFinalCountdown);
        vm.Focus.StartPauseCommand.Execute(null);
        Time.Advance(TimeSpan.FromSeconds(9));
        Ticks.Raise();
        Assert.Equal("00:01", vm.Focus.RemainingText);
        Assert.True(vm.Focus.IsFinalCountdown);

        Time.Advance(TimeSpan.FromSeconds(1));
        Ticks.Raise();
        Assert.Equal(FocusMode.ShortBreak, vm.Focus.Mode);
        Assert.False(vm.Focus.IsFinalCountdown);
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

    [Fact]
    public async Task AFirstRunSeedsTheSampleDataAndSavesIt()
    {
        var vm = await LoadAsync(firstRun: true);
        await vm.FlushAsync();

        Assert.Equal((12, 4), (vm.Tasks.Count, vm.DoneTasks.Count));
        Assert.Equal(SampleData.Create(Time).Sessions, vm.Sessions);
        var reloaded = await LoadAsync();
        Assert.Equal(vm.Tasks.Select(t => (t.Id, t.Tag)), reloaded.Tasks.Select(t => (t.Id, t.Tag)));
        Assert.Equal(vm.Sessions, reloaded.Sessions);
    }

    [Fact]
    public async Task AnExistingEmptyFileDoesNotSeed()
    {
        var vm = await LoadAsync();

        Assert.Empty(vm.Tasks);
        Assert.Empty(vm.Sessions);
    }

    [Fact]
    public async Task ClearAllEmptiesTheListsAndTheLogAndSaves()
    {
        var vm = await LoadAsync(firstRun: true);
        vm.SelectedTask = vm.Tasks[0];
        vm.Focus.StartPauseCommand.Execute(null);

        vm.ClearAll();

        Assert.Empty(vm.Tasks);
        Assert.Empty(vm.DoneTasks);
        Assert.Empty(vm.Sessions);
        Assert.Equal(0, vm.Stats.Streak);
        Assert.Null(vm.SelectedTask);
        Assert.Null(vm.Focus.ActiveTask);
        Assert.False(vm.Focus.IsRunning);
        await vm.FlushAsync();
        var reloaded = await LoadAsync();
        Assert.Empty(reloaded.Tasks);
        Assert.Empty(reloaded.Sessions);
    }

    [Fact]
    public async Task RestoreSampleDataReseedsKeepsSettingsAndSaves()
    {
        var vm = await LoadWithTasksAsync("Mine");
        vm.WorkMinutes = 50;

        vm.RestoreSampleData();

        var seed = SampleData.Create(Time);
        Assert.Equal(seed.Tasks.Select(t => t.Id), vm.Tasks.Concat(vm.DoneTasks).Select(t => t.Id));
        Assert.Equal(seed.Sessions, vm.Sessions);
        await vm.FlushAsync();
        var reloaded = await LoadAsync();
        Assert.Equal(16, reloaded.Tasks.Count + reloaded.DoneTasks.Count);
        Assert.Equal(50, reloaded.WorkMinutes);
    }

    [Fact]
    public async Task ATagFilterShowsItsTasksInListOrder()
    {
        var vm = await LoadTaggedAsync();

        vm.TagFilter = TaskTag.Code;

        Assert.Equal(["A", "C"], vm.VisibleTasks.Select(t => t.Title));
        Assert.Equal(1 + (int)TaskTag.Code, vm.TagFilterIndex);
        vm.TagFilterIndex = 0;
        Assert.Null(vm.TagFilter);
        Assert.Equal(vm.Tasks, vm.VisibleTasks);
    }

    [Fact]
    public async Task SwitchingFiltersInsertsAndRemovesOnlyTheRowsThatChange()
    {
        var vm = await LoadTaggedAsync();
        vm.TagFilter = TaskTag.Code;
        var changes = new List<(NotifyCollectionChangedAction, string)>();
        vm.VisibleTasks.CollectionChanged += (_, e) => changes.Add((e.Action,
            ((TaskItemViewModel)(e.NewItems ?? e.OldItems)![0]!).Title));

        vm.TagFilter = null;
        vm.TagFilter = TaskTag.Admin;

        Assert.Equal(
        [
            (NotifyCollectionChangedAction.Add, "B"), (NotifyCollectionChangedAction.Add, "D"),
            (NotifyCollectionChangedAction.Remove, "C"), (NotifyCollectionChangedAction.Remove, "B"), (NotifyCollectionChangedAction.Remove, "A"),
        ], changes);
        Assert.Equal(["D"], vm.VisibleTasks.Select(t => t.Title));
    }

    [Fact]
    public async Task SelectionSurvivesAFilterThatShowsItAndClearsWhenHidden()
    {
        var vm = await LoadTaggedAsync();
        vm.SelectedTask = vm.Tasks[2];

        vm.TagFilter = TaskTag.Code;
        Assert.Same(vm.Tasks[2], vm.SelectedTask);

        vm.TagFilter = TaskTag.Admin;
        Assert.Null(vm.SelectedTask);
    }

    [Fact]
    public async Task ATaskAddedUnderAFilterTakesItsTagAndShows()
    {
        var vm = await LoadTaggedAsync();
        vm.TagFilter = TaskTag.Admin;

        vm.NewTaskTitle = "E";
        vm.AddTaskCommand.Execute(null);

        Assert.Equal(TaskTag.Admin, vm.Tasks[^1].Tag);
        Assert.Equal(["D", "E"], vm.VisibleTasks.Select(t => t.Title));
    }

    [Fact]
    public async Task DeletesUndoRetagsAndDoneMovesKeepTheFilteredListInStep()
    {
        var vm = await LoadTaggedAsync();
        vm.TagFilter = TaskTag.Code;
        var (a, c) = (vm.Tasks[0], vm.Tasks[2]);

        vm.SelectedTask = a;
        vm.DeleteSelectedCommand.Execute(null);
        Assert.Equal([c], vm.VisibleTasks);
        vm.UndoDeleteCommand.Execute(null);
        Assert.Equal([a, c], vm.VisibleTasks);

        a.Tag = TaskTag.Admin;
        Assert.Equal([c], vm.VisibleTasks);
        c.IsDone = true;
        Time.Advance(MainViewModel.DoneMoveDelay);
        Assert.Empty(vm.VisibleTasks);
        await vm.FlushAsync();
    }

    [Fact]
    public async Task AReorderUnderAFilterMovesTheTaskBeforeItsNewVisibleNeighbour()
    {
        var vm = await LoadTaggedAsync();
        vm.TagFilter = TaskTag.Code;

        vm.VisibleTasks.Move(1, 0);
        Assert.Equal(["C", "A", "B", "D"], vm.Tasks.Select(t => t.Title));
        vm.VisibleTasks.Move(0, 1);
        Assert.Equal(["A", "C", "B", "D"], vm.Tasks.Select(t => t.Title));
        Assert.Equal(["A", "C"], vm.VisibleTasks.Select(t => t.Title));
        await vm.FlushAsync();

        var reloaded = await LoadAsync();
        Assert.Equal(["A", "C", "B", "D"], reloaded.Tasks.Select(t => t.Title));
    }

    /// <summary>A and C are Code, B has no tag, D is Admin.</summary>
    private async Task<MainViewModel> LoadTaggedAsync()
    {
        var vm = await LoadWithTasksAsync("A", "B", "C", "D");
        (vm.Tasks[0].Tag, vm.Tasks[2].Tag, vm.Tasks[3].Tag) = (TaskTag.Code, TaskTag.Code, TaskTag.Admin);
        return vm;
    }
}
