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
        Stats = new StatsViewModel(Sessions, time);

        Tasks.CollectionChanged += OnListChanged;
        DoneTasks.CollectionChanged += OnListChanged;
        Sessions.CollectionChanged += OnListChanged;
    }

    public static async Task<MainViewModel> LoadAsync(TempoStore store, TimeProvider time, ITickSource ticks) =>
        new(store, await store.LoadAsync(), time, ticks);

    /// <summary>Raised when a task is deleted; <see cref="UndoDeleteCommand"/> restores it.</summary>
    public event EventHandler<TaskItemViewModel>? TaskDeleted;

    public event EventHandler? AddBoxFocusRequested;

    public event EventHandler? SettingsRequested;

    /// <summary>Open tasks in list order; reordering is a move in this collection.</summary>
    public ObservableCollection<TaskItemViewModel> Tasks { get; } = [];

    /// <summary>Done tasks, most recently completed first.</summary>
    public ObservableCollection<TaskItemViewModel> DoneTasks { get; } = [];

    public ObservableCollection<SessionRecord> Sessions { get; }

    public FocusViewModel Focus { get; }

    public StatsViewModel Stats { get; }

    public Settings Settings { get; }

    [ObservableProperty]
    public partial TaskItemViewModel? SelectedTask { get; set; }

    [ObservableProperty]
    public partial string NewTaskTitle { get; set; } = "";

    [ObservableProperty]
    public partial bool IsDetailOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    public partial AppView CurrentView { get; set; }

    /// <summary>The view model of <see cref="CurrentView"/>.</summary>
    public ObservableObject CurrentPage => CurrentView == AppView.Stats ? Stats : Focus;

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

    [RelayCommand]
    private void AddTask()
    {
        var title = NewTaskTitle.Trim();
        if (title.Length == 0)
            return;
        Tasks.Add(Track(new TaskItemViewModel(new TaskItem { Title = title, Created = _time.GetUtcNow() })));
        NewTaskTitle = "";
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

    partial void OnSelectedTaskChanged(TaskItemViewModel? value) => Focus.SelectedTask = value;

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

    private TaskItemViewModel Track(TaskItemViewModel task)
    {
        task.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TaskItemViewModel.IsDone))
                _ = MoveForDoneAsync(task);
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
