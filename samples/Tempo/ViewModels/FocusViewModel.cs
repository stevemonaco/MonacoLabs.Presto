using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tempo.Model;

namespace Tempo.ViewModels;

/// <summary>
/// Presents the <see cref="FocusClock"/>: every tick re-reads it, completes a segment that is up, and logs each
/// finished segment to the session log.
/// </summary>
public sealed partial class FocusViewModel : ObservableObject
{
    private readonly FocusClock _clock;
    private readonly ObservableCollection<SessionRecord> _log;
    private TaskItemViewModel? _selected;

    public FocusViewModel(FocusClock clock, ObservableCollection<SessionRecord> log, ITickSource ticks)
    {
        _clock = clock;
        _log = log;
        ticks.Tick += (_, _) => Refresh();
        Refresh();
    }

    public event EventHandler<SessionCompletedEventArgs>? SessionCompleted;

    /// <summary>The remaining time as <c>mm:ss</c>, rounded up so a fresh segment reads <c>25:00</c> and the last second <c>00:01</c>.</summary>
    [ObservableProperty]
    public partial string RemainingText { get; private set; } = "";

    [ObservableProperty]
    public partial double Progress { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBreak), nameof(ModeLabel), nameof(MotionState))]
    public partial FocusMode Mode { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MotionState))]
    public partial bool IsRunning { get; private set; }

    public bool IsBreak => Mode != FocusMode.Focus;

    public string ModeLabel => Mode switch
    {
        FocusMode.ShortBreak => "Short break",
        FocusMode.LongBreak => "Long break",
        _ => "Focus",
    };

    /// <summary>The shell's <c>Motion.State</c> tokens: the mode, plus <c>Running</c> while the clock runs.</summary>
    public string MotionState => IsRunning ? $"{Mode} Running" : Mode.ToString();

    /// <summary>The task a focus segment is bound to while it runs or is paused; otherwise the selected task, which the next one binds.</summary>
    [ObservableProperty]
    public partial TaskItemViewModel? ActiveTask { get; private set; }

    public TaskItemViewModel? SelectedTask
    {
        get => _selected;
        set
        {
            _selected = value;
            if (!IsFocusBound)
                ActiveTask = value;
        }
    }

    private bool IsFocusBound => _clock.IsStarted && _clock.Mode == FocusMode.Focus;

    [RelayCommand]
    private void StartPause()
    {
        if (!_clock.IsStarted)
            _clock.Start(ActiveTask?.Id);
        else if (_clock.IsPaused)
            _clock.Resume();
        else
            _clock.Pause();
        Refresh();
    }

    [RelayCommand]
    private void Skip() => End(_clock.Skip());

    [RelayCommand]
    private void Abandon() => End(_clock.Abandon());

    private void Refresh()
    {
        var task = ActiveTask;
        if (_clock.Poll() is { } record)
        {
            if (record.Mode == FocusMode.Focus && task is not null)
                task.CompletedSessions++;
            _log.Add(record);
            SessionCompleted?.Invoke(this, new SessionCompletedEventArgs(record, record.TaskId is null ? null : task?.Title));
        }

        var seconds = (int)Math.Ceiling(_clock.Remaining.TotalSeconds);
        RemainingText = $"{seconds / 60:00}:{seconds % 60:00}";
        Progress = _clock.Progress;
        Mode = _clock.Mode;
        IsRunning = _clock.IsRunning;
        if (!IsFocusBound)
            ActiveTask = _selected;
    }

    private void End(SessionRecord? record)
    {
        if (record is not null)
            _log.Add(record);
        Refresh();
    }
}
