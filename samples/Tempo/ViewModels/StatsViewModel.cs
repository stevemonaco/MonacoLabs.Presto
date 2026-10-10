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
    private readonly ObservableCollection<SessionRecord> _log;
    private readonly TimeProvider _time;

    public StatsViewModel(ObservableCollection<SessionRecord> log, TimeProvider time)
    {
        _log = log;
        _time = time;
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
            var day = TimeZoneInfo.ConvertTime(record.Start, _time.LocalTimeZone).Date;
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
    }
}
