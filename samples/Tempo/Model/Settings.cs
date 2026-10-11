namespace Tempo.Model;

/// <summary>User preferences, persisted by <see cref="TempoStore"/>.</summary>
public sealed class Settings
{
    public int WorkMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int SessionsBeforeLongBreak { get; set; } = 4;
    public bool AutoStartBreaks { get; set; }
    public bool PlayIntro { get; set; } = true;
    public MotionPreference Motion { get; set; }

    /// <summary>The restored window size and position; null until the window has been placed once.</summary>
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public bool IsMaximized { get; set; }
}
