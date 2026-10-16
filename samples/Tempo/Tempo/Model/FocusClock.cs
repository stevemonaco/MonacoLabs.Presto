namespace Tempo.Model;

/// <summary>
/// The Pomodoro state machine: focus, then a short break, with a long break after every
/// <see cref="Settings.SessionsBeforeLongBreak"/> completed focus segments. Times are computed from
/// <see cref="TimeProvider"/> on demand, so the clock keeps counting with no polls in between.
/// </summary>
/// <remarks>A segment that has not started yet is idle. Settings are read live until a segment starts, which fixes its duration.</remarks>
public sealed class FocusClock(TimeProvider time, Settings settings)
{
    private DateTimeOffset? _start;
    private DateTimeOffset? _pausedAt;
    private TimeSpan _pausedTotal;
    private TimeSpan _duration;

    public FocusMode Mode { get; private set; }

    /// <summary>The task the current focus segment is bound to.</summary>
    public Guid? TaskId { get; private set; }

    /// <summary>Completed focus segments since the last long break.</summary>
    public int CompletedInCycle { get; private set; }

    public bool IsStarted => _start is not null;
    public bool IsPaused => _pausedAt is not null;
    public bool IsRunning => IsStarted && !IsPaused;

    public TimeSpan Duration => IsStarted ? _duration : DurationOf(Mode);

    public TimeSpan Elapsed
    {
        get
        {
            if (_start is not { } start)
                return TimeSpan.Zero;
            var elapsed = (_pausedAt ?? time.GetUtcNow()) - start - _pausedTotal;
            return elapsed < _duration ? elapsed : _duration;
        }
    }

    public TimeSpan Remaining => Duration - Elapsed;

    public double Progress => Duration > TimeSpan.Zero ? Elapsed / Duration : 0;

    /// <summary>Starts the idle segment, binding a focus segment to <paramref name="taskId"/>.</summary>
    public void Start(Guid? taskId = null)
    {
        if (IsStarted)
            return;
        _start = time.GetUtcNow();
        _pausedAt = null;
        _pausedTotal = TimeSpan.Zero;
        _duration = DurationOf(Mode);
        TaskId = Mode == FocusMode.Focus ? taskId : null;
    }

    public void Pause()
    {
        if (IsRunning)
            _pausedAt = time.GetUtcNow();
    }

    public void Resume()
    {
        if (_pausedAt is not { } pausedAt)
            return;
        _pausedTotal += time.GetUtcNow() - pausedAt;
        _pausedAt = null;
    }

    /// <summary>Completes the running segment once its time is up and advances to the next.</summary>
    /// <returns>The completed segment, or null when nothing completed.</returns>
    public SessionRecord? Poll()
    {
        if (!IsRunning || Elapsed < _duration)
            return null;

        var record = End(completed: true);
        if (Mode == FocusMode.Focus)
        {
            CompletedInCycle++;
            var longBreak = CompletedInCycle >= settings.SessionsBeforeLongBreak;
            if (longBreak)
                CompletedInCycle = 0;
            Mode = longBreak ? FocusMode.LongBreak : FocusMode.ShortBreak;
            if (settings.AutoStartBreaks)
                Start();
        }
        else
        {
            Mode = FocusMode.Focus;
        }
        return record;
    }

    /// <summary>Ends the current segment without credit and moves on: focus goes to a short break, a break back to focus.</summary>
    /// <returns>The skipped segment, or null when it had not started.</returns>
    public SessionRecord? Skip()
    {
        var record = IsStarted ? End(completed: false) : null;
        Mode = Mode == FocusMode.Focus ? FocusMode.ShortBreak : FocusMode.Focus;
        return record;
    }

    /// <summary>Discards the current segment and returns to an idle focus segment.</summary>
    /// <returns>The abandoned segment, or null when it had not started.</returns>
    public SessionRecord? Abandon()
    {
        var record = IsStarted ? End(completed: false) : null;
        Mode = FocusMode.Focus;
        return record;
    }

    private SessionRecord End(bool completed)
    {
        var record = new SessionRecord(TaskId, Mode, _start!.Value, Elapsed, completed);
        _start = null;
        _pausedAt = null;
        TaskId = null;
        return record;
    }

    private TimeSpan DurationOf(FocusMode mode) => TimeSpan.FromMinutes(mode switch
    {
        FocusMode.Focus => settings.WorkMinutes,
        FocusMode.ShortBreak => settings.ShortBreakMinutes,
        _ => settings.LongBreakMinutes,
    });
}
