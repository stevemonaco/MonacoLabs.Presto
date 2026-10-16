namespace Tempo.Model;

/// <summary>One finished segment in the session log.</summary>
/// <param name="TaskId">The task a focus segment was bound to; null for breaks and unbound focus.</param>
/// <param name="Focused">Time actually run, excluding pauses.</param>
/// <param name="Completed">False when the segment was skipped or abandoned.</param>
public sealed record SessionRecord(Guid? TaskId, FocusMode Mode, DateTimeOffset Start, TimeSpan Focused, bool Completed);
