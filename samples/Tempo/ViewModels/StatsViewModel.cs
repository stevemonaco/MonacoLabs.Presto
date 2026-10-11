using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Tempo.Model;

namespace Tempo.ViewModels;

/// <summary>
/// Totals over the session log, recomputed when it changes. Minutes count all focus time, including skipped and
/// abandoned segments; sessions and the streak count only completed focus segments.
/// </summary>
public sealed partial class StatsViewModel : ObservableObject
{
    private const int HeatmapWeeks = 12;

    private static readonly int[] s_levelMinutes = [1, 30, 60, 120];

    private readonly ObservableCollection<SessionRecord> _log;
    private readonly TimeProvider _time;
    private readonly Func<Guid, TaskTag?> _tagOf;

    /// <param name="tagOf">The tag of the task with this id, or null when it is untagged or gone.</param>
    public StatsViewModel(ObservableCollection<SessionRecord> log, TimeProvider time, Func<Guid, TaskTag?> tagOf)
    {
        _log = log;
        _time = time;
        _tagOf = tagOf;
        log.CollectionChanged += (_, _) => Recompute();
        Recompute();
    }

    /// <summary>Right-aligned to 3 cells for the SplitFlap.</summary>
    [ObservableProperty]
    public partial string TodayMinutesText { get; private set; } = "";

    /// <summary>Right-aligned to 2 cells for the SplitFlap.</summary>
    [ObservableProperty]
    public partial string TodaySessionsText { get; private set; } = "";

    /// <summary>The last 7 days, oldest first, ending today.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<StatsDay> Days { get; private set; } = [];

    /// <summary>Consecutive days with a completed focus session, up to today, or up to yesterday while today has none yet.</summary>
    [ObservableProperty]
    public partial int Streak { get; private set; }

    /// <summary>
    /// The days from the Monday <see cref="HeatmapWeeks"/> weeks back through today, oldest first: a column per week, Monday
    /// to Sunday, so the days after today in its week are absent and today's cell is the last.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<HeatmapCell> Heatmap { get; private set; } = [];

    /// <summary>The day with the most focus in the log, such as "Best day: Tuesday, 3h 20m"; empty when there is none.</summary>
    [ObservableProperty]
    public partial string BestDayText { get; private set; } = "";

    [ObservableProperty]
    public partial StatsRange Range { get; set; }

    /// <summary>Focus time in <see cref="Range"/> per tag, in tag order with Untagged last; the same instances on every recompute.</summary>
    public IReadOnlyList<DonutSegment> Segments { get; } = [.. Enum.GetValues<TaskTag>().Select(t => new DonutSegment(t)), new DonutSegment(null)];

    /// <summary>Reassigns the focus time to tags again, after a task's tag changed or a task was deleted or restored.</summary>
    public void RefreshTags()
    {
        var today = _time.GetLocalNow().Date;
        var from = Range switch
        {
            StatsRange.Today => today,
            StatsRange.Week => today.AddDays(-6),
            _ => DateTime.MinValue,
        };
        var time = new TimeSpan[Segments.Count];
        foreach (var record in _log)
        {
            if (record.Mode != FocusMode.Focus || DayOf(record) < from)
                continue;
            var tag = record.TaskId is { } id ? _tagOf(id) : null;
            time[tag is { } t ? (int)t : Segments.Count - 1] += record.Focused;
        }

        var total = time.Sum(t => t.Ticks);
        var through = 0L;
        for (var i = Segments.Count - 1; i >= 0; i--)
        {
            through += time[i].Ticks;
            Segments[i].Minutes = (int)time[i].TotalMinutes;
            Segments[i].End = total > 0 ? (double)through / total : 0;
        }
    }

    internal static string FormatMinutes(int minutes) => (minutes / 60, minutes % 60) switch
    {
        (0, var m) => $"{m}m",
        (var h, 0) => $"{h}h",
        var (h, m) => $"{h}h {m}m",
    };

    partial void OnRangeChanged(StatsRange value) => RefreshTags();

    private DateTime DayOf(SessionRecord record) => TimeZoneInfo.ConvertTime(record.Start, _time.LocalTimeZone).Date;

    private void Recompute()
    {
        var minutes = new Dictionary<DateTime, TimeSpan>();
        var completed = new HashSet<DateTime>();
        var sessionsToday = 0;
        var today = _time.GetLocalNow().Date;
        foreach (var record in _log)
        {
            if (record.Mode != FocusMode.Focus)
                continue;
            var day = DayOf(record);
            minutes[day] = minutes.GetValueOrDefault(day) + record.Focused;
            if (!record.Completed)
                continue;
            completed.Add(day);
            if (day == today)
                sessionsToday++;
        }

        int MinutesOn(DateTime day) => (int)minutes.GetValueOrDefault(day).TotalMinutes;

        TodayMinutesText = MinutesOn(today).ToString(CultureInfo.CurrentCulture).PadLeft(3);
        TodaySessionsText = sessionsToday.ToString(CultureInfo.CurrentCulture).PadLeft(2);

        var week = Enumerable.Range(0, 7).Select(i => today.AddDays(i - 6)).ToArray();
        var busiest = week.Max(MinutesOn);
        Days = [.. week.Select(day => new StatsDay(
            day.ToString("ddd", CultureInfo.CurrentCulture), MinutesOn(day), busiest > 0 ? (double)MinutesOn(day) / busiest : 0))];

        var streakDay = completed.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (completed.Contains(streakDay.AddDays(-streak)))
            streak++;
        Streak = streak;

        var todayRow = ((int)today.DayOfWeek + 6) % 7;
        var lastColumn = HeatmapWeeks - 1;
        var first = today.AddDays(-todayRow - 7 * lastColumn);
        Heatmap = [.. Enumerable.Range(0, 7 * lastColumn + todayRow + 1).Select(i =>
        {
            var day = first.AddDays(i);
            var dayMinutes = MinutesOn(day);
            return new HeatmapCell(day, dayMinutes, s_levelMinutes.Count(m => dayMinutes >= m),
                Math.Sqrt(Math.Pow(lastColumn - i / 7, 2) + Math.Pow(todayRow - i % 7, 2)));
        })];

        var best = minutes.DefaultIfEmpty().MaxBy(p => p.Value);
        BestDayText = best.Value > TimeSpan.Zero
            ? $"Best day: {best.Key.ToString("dddd", CultureInfo.CurrentCulture)}, {FormatMinutes((int)best.Value.TotalMinutes)}"
            : "";

        RefreshTags();
    }
}
