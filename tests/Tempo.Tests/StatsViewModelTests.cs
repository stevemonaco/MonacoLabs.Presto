using System.Collections.ObjectModel;
using System.Globalization;
using Tempo.Model;
using Tempo.ViewModels;

namespace Tempo.Tests;

public sealed class StatsViewModelTests : TempoTestBase
{
    private static readonly Guid s_code = Guid.NewGuid();
    private static readonly Guid s_writing = Guid.NewGuid();
    private static readonly Guid s_deleted = Guid.NewGuid();

    private readonly ObservableCollection<SessionRecord> _log = [];
    private readonly Dictionary<Guid, TaskTag?> _tags = new() { [s_code] = TaskTag.Code, [s_writing] = TaskTag.Writing };

    private StatsViewModel Create() => new(_log, Time, id => _tags.GetValueOrDefault(id));

    private void Log(int daysAgo, double minutes, Guid? task = null, FocusMode mode = FocusMode.Focus) =>
        _log.Add(new SessionRecord(task, mode, Time.GetUtcNow().AddDays(-daysAgo).AddHours(-1), TimeSpan.FromMinutes(minutes), true));

    [Fact]
    public void TheHeatmapRunsFromTheMondayElevenWeeksBeforeThisOneThroughToday()
    {
        var cells = Create().Heatmap;
        var today = Time.GetLocalNow().Date;

        Assert.Equal(DayOfWeek.Friday, today.DayOfWeek);
        Assert.Equal(77 + 5, cells.Count);
        Assert.Equal(DayOfWeek.Monday, cells[0].Date.DayOfWeek);
        Assert.Equal(today.AddDays(-81), cells[0].Date);
        Assert.Equal(today, cells[^1].Date);
        Assert.All(cells.Zip(cells.Skip(1)), pair => Assert.Equal(pair.First.Date.AddDays(1), pair.Second.Date));
    }

    [Fact]
    public void HeatmapLevelsBucketEachDaysFocusedMinutes()
    {
        Log(0, 25);
        Log(1, 45);
        Log(2, 90);
        Log(3, 100);
        Log(3, 50);
        Log(4, 60, mode: FocusMode.ShortBreak);

        var cells = Create().Heatmap;

        Assert.Equal([0, 4, 3, 2, 1], cells.TakeLast(5).Select(c => c.Level));
        Assert.Equal([0, 150, 90, 45, 25], cells.TakeLast(5).Select(c => c.Minutes));
        Assert.All(cells.SkipLast(5), c => Assert.Equal(0, c.Level));
    }

    [Fact]
    public void DistanceIsMeasuredInCellsFromToday()
    {
        var cells = Create().Heatmap;

        Assert.Equal(0, cells[^1].Distance);
        Assert.Equal(1, cells[^8].Distance);
        Assert.Equal(4, cells[^5].Distance);
        Assert.Equal(Math.Sqrt(11 * 11 + 4 * 4), cells[0].Distance, 6);
    }

    [Fact]
    public void TagMinutesFollowTheRangeAndUntaggedTakesUnboundAndDeletedTasks()
    {
        Log(0, 25, s_code);
        Log(0, 10);
        Log(3, 50, s_writing);
        Log(20, 30, s_deleted);
        Log(0, 5, s_code, FocusMode.LongBreak);
        var stats = Create();

        Assert.Equal(StatsRange.Today, stats.Range);
        Assert.Equal([0, 25, 0, 0, 0, 10], stats.Segments.Select(s => s.Minutes));
        stats.Range = StatsRange.Week;
        Assert.Equal([50, 25, 0, 0, 0, 10], stats.Segments.Select(s => s.Minutes));
        stats.Range = StatsRange.All;
        Assert.Equal([50, 25, 0, 0, 0, 40], stats.Segments.Select(s => s.Minutes));
        Assert.Equal([TaskTag.Writing, TaskTag.Code, TaskTag.Design, TaskTag.Admin, TaskTag.Personal, null], stats.Segments.Select(s => s.Tag));
        Assert.Equal("Untagged", stats.Segments[^1].Label);
    }

    [Fact]
    public void EachSegmentEndsAtItsShareAddedToThoseAfterIt()
    {
        Log(0, 25, s_code);
        Log(0, 10);
        Log(3, 50, s_writing);
        var stats = Create();
        stats.Range = StatsRange.Week;

        Assert.Equal([1, 35 / 85d, 10 / 85d, 10 / 85d, 10 / 85d, 10 / 85d], stats.Segments.Select(s => s.End));

        stats.Range = StatsRange.Today;
        Assert.Equal([1, 1, 10 / 35d, 10 / 35d, 10 / 35d, 10 / 35d], stats.Segments.Select(s => s.End));
    }

    [Fact]
    public void ARangeWithNoFocusDrawsNoSegment()
    {
        Log(3, 50, s_writing);
        var stats = Create();

        Assert.All(stats.Segments, s => Assert.Equal(0, s.End));
    }

    [Fact]
    public void TheBestDayIsTheDayWithTheMostFocus()
    {
        Log(6, 120);
        Log(6, 80);
        Log(2, 150);
        Log(0, 25);
        var day = Time.GetLocalNow().Date.AddDays(-6).ToString("dddd", CultureInfo.CurrentCulture);

        Assert.Equal($"Best day: {day}, 3h 20m", Create().BestDayText);
    }

    [Fact]
    public void ThereIsNoBestDayWithoutFocus()
    {
        Log(0, 30, mode: FocusMode.ShortBreak);

        Assert.Equal("", Create().BestDayText);
    }

    [Fact]
    public void ALogChangeRecomputesTheHeatmapTheTagsAndTheBestDay()
    {
        var stats = Create();
        Assert.Equal(0, stats.Heatmap[^1].Minutes);

        Log(0, 45, s_code);

        Assert.Equal((45, 2), (stats.Heatmap[^1].Minutes, stats.Heatmap[^1].Level));
        Assert.Equal(45, stats.Segments[1].Minutes);
        Assert.EndsWith(", 45m", stats.BestDayText);
    }

    [Fact]
    public async Task RetaggingOrDeletingATaskMovesItsTime()
    {
        var vm = await LoadWithTasksAsync("Write");
        var task = vm.Tasks[0];
        vm.Sessions.Add(new SessionRecord(task.Id, FocusMode.Focus, Time.GetUtcNow(), TimeSpan.FromMinutes(25), true));
        Assert.Equal(25, vm.Stats.Segments[^1].Minutes);

        task.Tag = TaskTag.Writing;
        Assert.Equal((25, 0), (vm.Stats.Segments[0].Minutes, vm.Stats.Segments[^1].Minutes));

        vm.SelectedTask = task;
        vm.DeleteSelectedCommand.Execute(null);
        Assert.Equal((0, 25), (vm.Stats.Segments[0].Minutes, vm.Stats.Segments[^1].Minutes));
        await vm.FlushAsync();
    }
}
