using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tempo.Model;

namespace Tempo.ViewModels;

/// <summary>
/// The app's root view model: the task lists, selection, the focus and stats panels and settings. Every change is
/// saved through <see cref="TempoStore"/> in order, coalescing changes made while a save is in flight.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>How long a task that changes its done state stays in its list, so its strike line draws before it moves.</summary>
    public static readonly TimeSpan DoneMoveDelay = TimeSpan.FromMilliseconds(450);

    private readonly TempoStore _store;
    private readonly TimeProvider _time;
    private Task _saving = Task.CompletedTask;
    private TempoData? _latest;
    private (TaskItemViewModel Task, ObservableCollection<TaskItemViewModel> List, int Index)? _deleted;

    private MainViewModel(TempoStore store, TempoData data, TimeProvider time, ITickSource ticks)
    {
        _store = store;
        _time = time;
        Settings = data.Settings;
        Sessions = [.. data.Sessions];
        foreach (var item in data.Tasks)
            (item.IsDone ? DoneTasks : Tasks).Add(Track(new TaskItemViewModel(item)));
        Focus = new FocusViewModel(new FocusClock(time, data.Settings), Sessions, ticks);
        Stats = new StatsViewModel(Sessions, time, id => Tasks.Concat(DoneTasks).FirstOrDefault(t => t.Id == id)?.Tag);
        SyncVisible();

        Tasks.CollectionChanged += OnListChanged;
        Tasks.CollectionChanged += (_, _) => SyncVisible();
        Tasks.CollectionChanged += (_, _) => Stats.RefreshTags();
        VisibleTasks.CollectionChanged += OnVisibleTasksChanged;
        DoneTasks.CollectionChanged += OnListChanged;
        DoneTasks.CollectionChanged += (_, _) => Stats.RefreshTags();
        Sessions.CollectionChanged += OnListChanged;
    }

    /// <summary>Loads the saved data or, on first run, when no file exists yet, the sample data, saved at once.</summary>
    public static async Task<MainViewModel> LoadAsync(TempoStore store, TimeProvider time, ITickSource ticks)
    {
        if (store.Exists)
            return new(store, await store.LoadAsync(), time, ticks);
        var seeded = new MainViewModel(store, SampleData.Create(time), time, ticks);
        seeded.Persist();
        return seeded;
    }

    /// <summary>Raised when a task is deleted; <see cref="UndoDeleteCommand"/> restores it.</summary>
    public event EventHandler<TaskItemViewModel>? TaskDeleted;

    public event EventHandler? AddBoxFocusRequested;

    /// <summary>Raised after the add box adds a task, so the view can bring its row into view.</summary>
    public event EventHandler<TaskItemViewModel>? TaskAdded;

    public event EventHandler? SettingsRequested;

    /// <summary>Raised when Restore or Clear asks to be confirmed; the view opens the dialog, which sets <see cref="IsConfirmOpen"/>.</summary>
    public event EventHandler? ConfirmRequested;

    /// <summary>The view's exit for a confirmed clear, which calls <see cref="ClearAll"/> once the exit has played; without one the clear applies at once.</summary>
    public Func<Task>? PlayClear { get; set; }

    /// <summary>Open tasks in list order; reordering is a move in this collection.</summary>
    public ObservableCollection<TaskItemViewModel> Tasks { get; } = [];

    /// <summary>
    /// The open tasks <see cref="TagFilter"/> shows, in list order. Kept in step with <see cref="Tasks"/> by inserting and
    /// removing only the rows whose visibility changes; a move here (a reorder) is made in <see cref="Tasks"/>.
    /// </summary>
    public ObservableCollection<TaskItemViewModel> VisibleTasks { get; } = [];

    /// <summary>The filter chips: All, then each tag, in <see cref="TagFilterIndex"/> order.</summary>
    public static IReadOnlyList<object> TagFilters { get; } = ["All", .. Enum.GetValues<TaskTag>().Cast<object>()];

    /// <summary>Done tasks, most recently completed first.</summary>
    public ObservableCollection<TaskItemViewModel> DoneTasks { get; } = [];

    public ObservableCollection<SessionRecord> Sessions { get; }

    public FocusViewModel Focus { get; }

    public StatsViewModel Stats { get; }

    public Settings Settings { get; }

    [ObservableProperty]
    public partial TaskItemViewModel? SelectedTask { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNewTaskText))]
    public partial string NewTaskTitle { get; set; } = "";

    /// <summary>Whether the add box holds a title Enter would add, when its plus shows a check.</summary>
    public bool HasNewTaskText => !string.IsNullOrWhiteSpace(NewTaskTitle);

    /// <summary>The tag <see cref="VisibleTasks"/> shows, or null for every open task; a task added meanwhile takes it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagFilterIndex))]
    public partial TaskTag? TagFilter { get; set; }

    /// <summary><see cref="TagFilter"/> as an index into <see cref="TagFilters"/>.</summary>
    public int TagFilterIndex
    {
        get => TagFilter is { } tag ? (int)tag + 1 : 0;
        set => TagFilter = value > 0 ? (TaskTag)(value - 1) : null;
    }

    [ObservableProperty]
    public partial bool IsDetailOpen { get; set; }

    [ObservableProperty]
    public partial bool IsConfirmOpen { get; set; }

    /// <summary>What the confirm dialog asks about; it keeps the last request while the dialog closes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfirmTitle), nameof(ConfirmMessage), nameof(ConfirmHoldLabel))]
    public partial DataAction ConfirmAction { get; private set; }

    public string ConfirmTitle => ConfirmAction == DataAction.Clear ? "Clear all data?" : "Restore sample data?";

    public string ConfirmMessage => ConfirmAction == DataAction.Clear
        ? "Every task, done task and focus session is deleted. Settings stay."
        : "Your tasks and session log are replaced with the sample set. Settings stay.";

    public string ConfirmHoldLabel => ConfirmAction == DataAction.Clear ? "Hold to clear" : "Hold to restore";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    public partial AppView CurrentView { get; set; }

    /// <summary>The view model of <see cref="CurrentView"/>; the Tasks page is this view model itself.</summary>
    public ObservableObject CurrentPage => CurrentView switch
    {
        AppView.Stats => Stats,
        AppView.Tasks => this,
        _ => Focus,
    };

    /// <summary>Whether the last change of <see cref="CurrentView"/> went back in the bottom bar's order: Tasks, Focus, Stats.</summary>
    [ObservableProperty]
    public partial bool IsBack { get; private set; }

    /// <summary>Whether the shell shows its compact layout, set by the view from its width.</summary>
    [ObservableProperty]
    public partial bool IsCompact { get; set; }

    public MotionPreference Motion
    {
        get => Settings.Motion;
        set => SetSetting(Settings.Motion, value, (s, v) => s.Motion = v);
    }

    public bool PlayIntro
    {
        get => Settings.PlayIntro;
        set => SetSetting(Settings.PlayIntro, value, (s, v) => s.PlayIntro = v);
    }

    public int WorkMinutes
    {
        get => Settings.WorkMinutes;
        set => SetSetting(Settings.WorkMinutes, value, (s, v) => s.WorkMinutes = v);
    }

    public int ShortBreakMinutes
    {
        get => Settings.ShortBreakMinutes;
        set => SetSetting(Settings.ShortBreakMinutes, value, (s, v) => s.ShortBreakMinutes = v);
    }

    public int LongBreakMinutes
    {
        get => Settings.LongBreakMinutes;
        set => SetSetting(Settings.LongBreakMinutes, value, (s, v) => s.LongBreakMinutes = v);
    }

    public int SessionsBeforeLongBreak
    {
        get => Settings.SessionsBeforeLongBreak;
        set => SetSetting(Settings.SessionsBeforeLongBreak, value, (s, v) => s.SessionsBeforeLongBreak = v);
    }

    public bool AutoStartBreaks
    {
        get => Settings.AutoStartBreaks;
        set => SetSetting(Settings.AutoStartBreaks, value, (s, v) => s.AutoStartBreaks = v);
    }

    /// <summary>Completes once every change made so far is on disk.</summary>
    public Task FlushAsync() => _saving;

    /// <summary>Replaces the tasks and the session log with a fresh copy of the sample data; settings stay.</summary>
    public void RestoreSampleData() => Replace(SampleData.Create(_time));

    /// <summary>Empties the task lists and the session log; settings stay.</summary>
    public void ClearAll() => Replace(new TempoData());

    [RelayCommand]
    private void AddTask()
    {
        var title = NewTaskTitle.Trim();
        if (title.Length == 0)
            return;
        var task = Track(new TaskItemViewModel(new TaskItem { Title = title, Created = _time.GetUtcNow(), Tag = TagFilter }));
        Tasks.Add(task);
        NewTaskTitle = "";
        TaskAdded?.Invoke(this, task);
    }

    private void DeleteTask(TaskItemViewModel task)
    {
        var list = Tasks.Contains(task) ? Tasks : DoneTasks;
        var index = list.IndexOf(task);
        if (index < 0)
            return;
        _deleted = (task, list, index);
        if (SelectedTask == task)
            SelectedTask = null;
        list.RemoveAt(index);
        TaskDeleted?.Invoke(this, task);
    }

    [RelayCommand]
    private void UndoDelete()
    {
        if (_deleted is not var (task, list, index))
            return;
        _deleted = null;
        list.Insert(Math.Min(index, list.Count), task);
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedTask is { } task)
            DeleteTask(task);
    }

    [RelayCommand]
    private void OpenDetail(TaskItemViewModel task)
    {
        SelectedTask = task;
        IsDetailOpen = true;
    }

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void FocusAddBox() => AddBoxFocusRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void RequestRestore() => RequestConfirm(DataAction.Restore);

    [RelayCommand]
    private void RequestClear() => RequestConfirm(DataAction.Clear);

    [RelayCommand]
    private void CancelConfirm() => IsConfirmOpen = false;

    /// <summary>Closes the dialog and applies <see cref="ConfirmAction"/>, a clear after <see cref="PlayClear"/>.</summary>
    [RelayCommand]
    private async Task Confirm()
    {
        IsConfirmOpen = false;
        if (ConfirmAction == DataAction.Restore)
            RestoreSampleData();
        else if (PlayClear is { } play)
            await play();
        else
            ClearAll();
    }

    private void RequestConfirm(DataAction action)
    {
        ConfirmAction = action;
        ConfirmRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedTaskChanged(TaskItemViewModel? value) => Focus.SelectedTask = value;

    // Before CurrentPage changes, so the page host reads the direction for the swap.
    partial void OnCurrentViewChanging(AppView oldValue, AppView newValue)
    {
        Focus.RingKey = null;
        IsBack = Order(newValue) < Order(oldValue);
    }

    private static int Order(AppView view) => view == AppView.Tasks ? -1 : (int)view;

    partial void OnIsCompactChanged(bool value)
    {
        if (!value && CurrentView == AppView.Tasks)
            CurrentView = AppView.Focus;
    }

    partial void OnTagFilterChanged(TaskTag? value) => SyncVisible();

    /// <summary>Saves the window's restored size and position; a maximized window keeps the last ones it had.</summary>
    public void SaveWindowPlacement(double width, double height, int x, int y, bool isMaximized)
    {
        Settings.IsMaximized = isMaximized;
        if (!isMaximized)
        {
            Settings.WindowWidth = width;
            Settings.WindowHeight = height;
            Settings.WindowX = x;
            Settings.WindowY = y;
        }
        Persist();
    }

    private void SetSetting<T>(T oldValue, T newValue, Action<Settings, T> set, [CallerMemberName] string? name = null)
    {
        if (SetProperty(oldValue, newValue, Settings, set, name))
            Persist();
    }

    private void Replace(TempoData data)
    {
        // A running segment would otherwise log against a task that is gone.
        Focus.AbandonCommand.Execute(null);
        SelectedTask = null;
        IsDetailOpen = false;
        _deleted = null;
        Tasks.Clear();
        DoneTasks.Clear();
        Sessions.Clear();
        foreach (var item in data.Tasks)
            (item.IsDone ? DoneTasks : Tasks).Add(Track(new TaskItemViewModel(item)));
        foreach (var record in data.Sessions)
            Sessions.Add(record);
    }

    private TaskItemViewModel Track(TaskItemViewModel task)
    {
        task.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TaskItemViewModel.IsDone))
                _ = MoveForDoneAsync(task);
            else if (e.PropertyName == nameof(TaskItemViewModel.Tag))
            {
                SyncVisible();
                Stats.RefreshTags();
            }
            Persist();
        };
        task.DeleteRequested += (_, _) => DeleteTask(task);
        return task;
    }

    private async Task MoveForDoneAsync(TaskItemViewModel task)
    {
        await Task.Delay(DoneMoveDelay, _time);
        var (from, to) = task.IsDone ? (Tasks, DoneTasks) : (DoneTasks, Tasks);
        if (!from.Remove(task))
            return;
        if (task.IsDone)
            to.Insert(0, task);
        else
            to.Add(task);
    }

    private void OnListChanged(object? sender, NotifyCollectionChangedEventArgs e) => Persist();

    private bool Shows(TaskItemViewModel task) => TagFilter is not { } tag || task.Tag == tag;

    private void SyncVisible()
    {
        for (var i = VisibleTasks.Count - 1; i >= 0; i--)
        {
            var task = VisibleTasks[i];
            if (Tasks.Contains(task) && Shows(task))
                continue;
            if (SelectedTask == task)
                SelectedTask = null;
            VisibleTasks.RemoveAt(i);
        }
        var at = 0;
        foreach (var task in Tasks)
        {
            if (!Shows(task))
                continue;
            if (at < VisibleTasks.Count && VisibleTasks[at] == task)
            {
                at++;
                continue;
            }
            // Only a move made in Tasks directly leaves the task further down.
            VisibleTasks.Remove(task);
            VisibleTasks.Insert(at++, task);
        }
    }

    /// <summary>Places a reordered row in <see cref="Tasks"/> just before its new visible successor, or after its predecessor when last.</summary>
    private void OnVisibleTasksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Move || e.OldStartingIndex == e.NewStartingIndex)
            return;
        var index = e.NewStartingIndex;
        var from = Tasks.IndexOf(VisibleTasks[index]);
        int to;
        if (index + 1 < VisibleTasks.Count)
        {
            to = Tasks.IndexOf(VisibleTasks[index + 1]);
            if (from < to)
                to--;
        }
        else
        {
            to = Tasks.IndexOf(VisibleTasks[index - 1]);
            if (from > to)
                to++;
        }
        Tasks.Move(from, to);
    }

    private void Persist()
    {
        var data = _latest = new TempoData
        {
            Tasks = [.. Tasks.Concat(DoneTasks).Select(t => t.ToModel())],
            Settings = Settings,
            Sessions = [.. Sessions],
        };
        _saving = WriteAfterAsync(_saving, data);
    }

    private async Task WriteAfterAsync(Task previous, TempoData data)
    {
        await previous.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
        if (data == _latest)
            await _store.SaveAsync(data);
    }
}
