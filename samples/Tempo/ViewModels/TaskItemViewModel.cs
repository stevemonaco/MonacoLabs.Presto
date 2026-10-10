using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonacoLabs.Presto;
using Tempo.Model;

namespace Tempo.ViewModels;

public sealed partial class TaskItemViewModel : ObservableObject
{
    private readonly DateTimeOffset _created;

    public TaskItemViewModel(TaskItem item)
    {
        Id = item.Id;
        _created = item.Created;
        Title = item.Title;
        Notes = item.Notes;
        Estimate = item.EstimateSessions;
        CompletedSessions = item.CompletedSessions;
        IsDone = item.IsDone;
        Tag = item.Tag;
    }

    /// <summary>Raised when a swipe asks to delete the task; <see cref="MainViewModel"/> deletes it.</summary>
    public event EventHandler? DeleteRequested;

    public Guid Id { get; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Notes { get; set; }

    [ObservableProperty]
    public partial int Estimate { get; set; }

    [ObservableProperty]
    public partial int CompletedSessions { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StrikeProgress))]
    public partial bool IsDone { get; set; }

    /// <summary>The detail drawer's tag chips: None, then each tag, in <see cref="TagIndex"/> order.</summary>
    public static IReadOnlyList<object> TagChoices { get; } = ["None", .. Enum.GetValues<TaskTag>().Cast<object>()];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagIndex))]
    public partial TaskTag? Tag { get; set; }

    /// <summary><see cref="Tag"/> as an index into <see cref="TagChoices"/>.</summary>
    public int TagIndex
    {
        get => Tag is { } tag ? (int)tag + 1 : 0;
        set => Tag = value > 0 ? (TaskTag)(value - 1) : null;
    }

    public double StrikeProgress => IsDone ? 1 : 0;

    /// <summary>
    /// One dot per estimated session, filled for each completed one; sessions beyond the estimate add dots. Updated in
    /// place, so a dot's view sees only its own fill change.
    /// </summary>
    public ObservableCollection<SessionDot> Dots { get; } = [];

    /// <summary>A swipe from the leading edge (<see cref="Edge.Right"/>) completes the task; one from the trailing edge deletes it.</summary>
    [RelayCommand]
    private void Swipe(Edge edge)
    {
        if (edge == Edge.Right)
            IsDone = true;
        else
            DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnEstimateChanged(int value) => SyncDots();

    partial void OnCompletedSessionsChanged(int value) => SyncDots();

    private void SyncDots()
    {
        var count = Math.Max(Estimate, CompletedSessions);
        while (Dots.Count > count)
            Dots.RemoveAt(Dots.Count - 1);
        while (Dots.Count < count)
            Dots.Add(new SessionDot());
        for (var i = 0; i < count; i++)
            Dots[i].IsFilled = i < CompletedSessions;
    }

    [RelayCommand]
    private void IncreaseEstimate() => Estimate++;

    [RelayCommand]
    private void DecreaseEstimate() => Estimate = Math.Max(1, Estimate - 1);

    public TaskItem ToModel() => new()
    {
        Id = Id,
        Created = _created,
        Title = Title,
        Notes = Notes,
        EstimateSessions = Estimate,
        CompletedSessions = CompletedSessions,
        IsDone = IsDone,
        Tag = Tag,
    };
}
