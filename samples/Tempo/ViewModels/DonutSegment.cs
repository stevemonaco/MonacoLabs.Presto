using CommunityToolkit.Mvvm.ComponentModel;
using Tempo.Model;

namespace Tempo.ViewModels;

/// <summary>One tag's share of the time-by-tag donut, updated in place so its stroke can ease to each new share.</summary>
public sealed partial class DonutSegment(TaskTag? tag) : ObservableObject
{
    /// <summary>The tag, or null for focus on no task, an untagged task or a deleted one.</summary>
    public TaskTag? Tag { get; } = tag;

    public string Label => Tag?.ToString() ?? "Untagged";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MinutesText))]
    public partial int Minutes { get; internal set; }

    public string MinutesText => StatsViewModel.FormatMinutes(Minutes);

    /// <summary>
    /// How far round the ring, 0..1, this segment's stroke runs from the top. Each segment is drawn under the ones after
    /// it, so it shows from the next segment's end to its own, and the first one's end is 1 whenever the range has any focus.
    /// </summary>
    [ObservableProperty]
    public partial double End { get; internal set; }
}
