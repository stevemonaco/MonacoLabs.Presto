namespace Tempo.ViewModels;

/// <summary>The span of the session log the time-by-tag donut covers.</summary>
public enum StatsRange
{
    Today,
    /// <summary>The last 7 days, ending today.</summary>
    Week,
    All,
}
