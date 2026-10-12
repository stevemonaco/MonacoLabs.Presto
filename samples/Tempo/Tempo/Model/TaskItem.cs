namespace Tempo.Model;

/// <summary>A task on the list; list order is the order tasks are stored in.</summary>
public sealed class TaskItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public int EstimateSessions { get; set; } = 1;
    public int CompletedSessions { get; set; }
    public bool IsDone { get; set; }
    public TaskTag? Tag { get; set; }
    public DateTimeOffset Created { get; init; }
}
