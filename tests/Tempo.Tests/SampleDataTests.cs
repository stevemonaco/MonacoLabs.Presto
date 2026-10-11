using System.Collections.ObjectModel;
using System.Text.Json;
using Tempo.Model;
using Tempo.ViewModels;

namespace Tempo.Tests;

public sealed class SampleDataTests : TempoTestBase
{
    [Fact]
    public void TheSameDayGivesTheSameData() =>
        Assert.Equal(JsonSerializer.Serialize(SampleData.Create(Time)), JsonSerializer.Serialize(SampleData.Create(Time)));

    [Fact]
    public void TodayAlreadyHasSessions()
    {
        var stats = new StatsViewModel(new ObservableCollection<SessionRecord>(SampleData.Create(Time).Sessions), Time, _ => null);

        Assert.NotEqual(" 0", stats.TodaySessionsText);
    }

    [Fact]
    public void TheHistorySpansEightWeeksWithGaps()
    {
        var days = SampleData.Create(Time).Sessions.Select(s => s.Start.Date).Distinct().ToList();
        var today = Time.GetLocalNow().Date;

        Assert.Equal(today.AddDays(-55), days.Min());
        Assert.Equal(today, days.Max());
        Assert.InRange(days.Count, 48, 55);
    }

    [Fact]
    public void TasksCoverEstimatesTagsAndDone()
    {
        var data = SampleData.Create(Time);
        var ids = data.Tasks.Select(t => t.Id).ToHashSet();

        Assert.Equal(16, data.Tasks.Count);
        Assert.Equal((1, 6), (data.Tasks.Min(t => t.EstimateSessions), data.Tasks.Max(t => t.EstimateSessions)));
        Assert.Equal(Enum.GetValues<TaskTag>(), data.Tasks.Select(t => t.Tag!.Value).Distinct().Order());
        Assert.Equal(4, data.Tasks.Count(t => t.IsDone));
        Assert.Contains(data.Tasks, t => !t.IsDone && t.CompletedSessions > 0);
        Assert.All(data.Sessions.Where(s => s.Mode == FocusMode.Focus), s => Assert.Contains(s.TaskId!.Value, ids));
        Assert.Contains(data.Sessions, s => s.Mode == FocusMode.Focus && !s.Completed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12.25)]
    public void NothingIsDatedAfterNowEvenJustPastMidnight(double hoursLater)
    {
        Time.Advance(TimeSpan.FromHours(hoursLater));
        var data = SampleData.Create(Time);
        var now = Time.GetUtcNow();

        Assert.All(data.Sessions, s => Assert.True(s.Start < now));
        Assert.All(data.Tasks, t => Assert.True(t.Created < now));
        Assert.Equal(9, new StatsViewModel(new ObservableCollection<SessionRecord>(data.Sessions), Time, _ => null).Streak);
    }
}
