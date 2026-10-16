namespace Tempo.Model;

/// <summary>Everything <see cref="TempoStore"/> persists.</summary>
public sealed class TempoData
{
    public List<TaskItem> Tasks { get; set; } = [];
    public Settings Settings { get; set; } = new();
    public List<SessionRecord> Sessions { get; set; } = [];
}
