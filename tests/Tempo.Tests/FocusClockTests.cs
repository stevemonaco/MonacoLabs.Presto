using Microsoft.Extensions.Time.Testing;
using Tempo.Model;

namespace Tempo.Tests;

public sealed class FocusClockTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly Settings _settings = new();
    private readonly FocusClock _clock;

    public FocusClockTests() => _clock = new FocusClock(_time, _settings);

    [Fact]
    public void CountsDownFromTheWorkDurationAndCompletesOnPoll()
    {
        var task = Guid.NewGuid();
        _clock.Start(task);
        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(15), _clock.Remaining);
        Assert.Equal(0.4, _clock.Progress, 6);
        Assert.Null(_clock.Poll());

        _time.Advance(TimeSpan.FromMinutes(15));
        var record = _clock.Poll();

        Assert.NotNull(record);
        Assert.Equal(new SessionRecord(task, FocusMode.Focus, record.Start, TimeSpan.FromMinutes(25), true), record);
        Assert.Equal(FocusMode.ShortBreak, _clock.Mode);
        Assert.False(_clock.IsStarted);
    }

    [Fact]
    public void TimePausedIsNotCounted()
    {
        _clock.Start();
        _time.Advance(TimeSpan.FromMinutes(5));
        _clock.Pause();
        _time.Advance(TimeSpan.FromMinutes(30));

        Assert.Equal(TimeSpan.FromMinutes(5), _clock.Elapsed);
        Assert.Null(_clock.Poll());

        _clock.Resume();
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromMinutes(10), _clock.Elapsed);
        Assert.True(_clock.IsRunning);
    }

    [Fact]
    public void FourthCompletedWorkSessionEarnsALongBreakAndResetsTheCycle()
    {
        var modes = new List<FocusMode>();
        for (var i = 0; i < 5; i++)
        {
            RunToEnd();
            modes.Add(_clock.Mode);
            RunToEnd();
        }

        Assert.Equal(
            [FocusMode.ShortBreak, FocusMode.ShortBreak, FocusMode.ShortBreak, FocusMode.LongBreak, FocusMode.ShortBreak],
            modes);
        Assert.Equal(1, _clock.CompletedInCycle);
    }

    [Fact]
    public void AutoStartBreaksStartsTheBreakRunning()
    {
        _settings.AutoStartBreaks = true;
        RunToEnd();

        Assert.Equal(FocusMode.ShortBreak, _clock.Mode);
        Assert.True(_clock.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(5), _clock.Remaining);
    }

    [Fact]
    public void SkipEndsTheSegmentWithoutCreditAndAdvances()
    {
        _clock.Start();
        _time.Advance(TimeSpan.FromMinutes(3));
        var record = _clock.Skip();

        Assert.False(record!.Completed);
        Assert.Equal(TimeSpan.FromMinutes(3), record.Focused);
        Assert.Equal(FocusMode.ShortBreak, _clock.Mode);
        Assert.Equal(0, _clock.CompletedInCycle);

        Assert.Null(_clock.Skip());
        Assert.Equal(FocusMode.Focus, _clock.Mode);
    }

    [Fact]
    public void AbandonDiscardsTheWorkSessionAndGoesIdle()
    {
        _clock.Start(Guid.NewGuid());
        _time.Advance(TimeSpan.FromMinutes(20));
        var record = _clock.Abandon();

        Assert.False(record!.Completed);
        Assert.Equal(FocusMode.Focus, _clock.Mode);
        Assert.False(_clock.IsStarted);
        Assert.Null(_clock.TaskId);
        Assert.Equal(TimeSpan.FromMinutes(25), _clock.Remaining);
    }

    [Fact]
    public void KeepsCountingAcrossALongGapAndCompletesOnTheNextPoll()
    {
        _clock.Start();
        _time.Advance(TimeSpan.FromHours(3));

        Assert.Equal(TimeSpan.Zero, _clock.Remaining);
        Assert.Equal(1, _clock.Progress);
        Assert.Equal(TimeSpan.FromMinutes(25), _clock.Poll()!.Focused);
    }

    [Fact]
    public void DurationChangesApplyFromTheNextSegment()
    {
        _clock.Start();
        _settings.WorkMinutes = 50;

        Assert.Equal(TimeSpan.FromMinutes(25), _clock.Duration);
        _clock.Abandon();
        Assert.Equal(TimeSpan.FromMinutes(50), _clock.Duration);
    }

    private void RunToEnd()
    {
        _clock.Start();
        _time.Advance(_clock.Duration);
        Assert.NotNull(_clock.Poll());
    }
}
