namespace Monaco.Presto.AvaloniaBridge.DevTools;

/// <summary>A timer's playback state as the tickable list shows it, polled from its public flags.</summary>
internal enum TickableState
{
    Pending,
    Delay,
    Running,
    Paused,
    Completed,
    Cancelled,
}
