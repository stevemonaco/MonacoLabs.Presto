namespace Tempo.Model;

/// <summary>
/// The first-run data: sixteen tagged tasks and eight weeks of session history ending in a nine-day streak. Built from
/// a fixed seed and dated back from today, so every run on the same day yields the same data.
/// </summary>
public static class SampleData
{
    private const int HistoryDays = 56;
    private const int StreakDays = 9;

    private static readonly TimeSpan s_focus = TimeSpan.FromMinutes(25);
    private static readonly int[] s_gapDays = [StreakDays, 17, 26, 38, 47];

    private static readonly (string Title, TaskTag Tag, int Estimate, string Notes, bool IsDone)[] s_tasks =
    [
        ("Draft the launch announcement", TaskTag.Writing, 3, "Lead with offline mode; keep it under 400 words.", false),
        ("Fix the sync retry backoff", TaskTag.Code, 4, "Retries hammer the API after a timeout. Cap the delay at 30 s.", false),
        ("Review the onboarding mockups", TaskTag.Design, 2, "", false),
        ("File the September expenses", TaskTag.Admin, 1, "", false),
        ("Plan the weekend hike", TaskTag.Personal, 1, "Check the trail report on Friday.", false),
        ("Unit tests for the date parser", TaskTag.Code, 5, "", false),
        ("Outline the conference talk", TaskTag.Writing, 6, "Three acts: the problem, the detour, the fix.", false),
        ("Icon set for the settings panel", TaskTag.Design, 3, "", false),
        ("Renew the passport", TaskTag.Personal, 1, "", false),
        ("Refactor the export pipeline", TaskTag.Code, 6, "", false),
        ("Edit the case study draft", TaskTag.Writing, 2, "", false),
        ("Color pass on the dashboard", TaskTag.Design, 4, "Try OKLCH ramps for the chart series.", false),
        ("Set up the CI build cache", TaskTag.Code, 2, "", true),
        ("Book the dentist", TaskTag.Personal, 1, "", true),
        ("Proofread the newsletter", TaskTag.Writing, 2, "", true),
        ("Submit the team timesheets", TaskTag.Admin, 1, "", true),
    ];

    public static TempoData Create(TimeProvider time)
    {
        var random = new Random(2026);
        var now = time.GetLocalNow();
        var tasks = new List<TaskItem>();
        foreach (var (title, tag, estimate, notes, isDone) in s_tasks)
        {
            var id = new byte[16];
            random.NextBytes(id);
            tasks.Add(new TaskItem
            {
                Id = new Guid(id),
                Title = title,
                Notes = notes,
                Tag = tag,
                EstimateSessions = estimate,
                CompletedSessions = isDone ? estimate : random.Next(estimate),
                IsDone = isDone,
                Created = now.AddDays(-random.Next(1, 21)).AddMinutes(-random.Next(600)),
            });
        }

        var sessions = new List<SessionRecord>();
        for (var daysAgo = HistoryDays - 1; daysAgo >= 0; daysAgo--)
        {
            if (s_gapDays.Contains(daysAgo))
                continue;
            var day = now.Date.AddDays(-daysAgo);
            var count = daysAgo == 0 ? 2 : day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? random.Next(1, 3) : random.Next(4, 9);
            var start = day.AddHours(9).AddMinutes(random.Next(60));
            // Today's sessions end before now, even early in the morning.
            var latest = now.DateTime.AddHours(-2);
            if (daysAgo == 0 && start > latest)
                start = latest < day ? day : latest;

            for (var i = 0; i < count; i++)
            {
                var completed = i == 0 || random.NextDouble() > .15;
                var focused = completed ? s_focus : TimeSpan.FromMinutes(random.Next(3, 20));
                if (!Add(tasks[random.Next(tasks.Count)].Id, FocusMode.Focus, focused, completed))
                    break;
                var mode = (i + 1) % 4 == 0 ? FocusMode.LongBreak : FocusMode.ShortBreak;
                if (!Add(null, mode, TimeSpan.FromMinutes(mode == FocusMode.LongBreak ? 15 : 5), true))
                    break;
                start = start.AddMinutes(random.Next(20));
            }

            bool Add(Guid? taskId, FocusMode mode, TimeSpan length, bool completed)
            {
                var at = new DateTimeOffset(start, time.LocalTimeZone.GetUtcOffset(start));
                if (at >= now)
                    return false;
                sessions.Add(new SessionRecord(taskId, mode, at, length, completed));
                start += length;
                return true;
            }
        }

        return new TempoData { Tasks = tasks, Sessions = sessions };
    }
}
