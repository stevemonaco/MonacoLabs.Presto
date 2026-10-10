using Tempo.Model;

namespace Tempo.ViewModels;

/// <summary>A segment that ran to its end, with the title of the task a focus segment was bound to.</summary>
public sealed record SessionCompletedEventArgs(SessionRecord Record, string? TaskTitle);
