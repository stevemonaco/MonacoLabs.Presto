namespace MonacoLabs.Presto.DevTools;

/// <summary>A timer's playback state as the tickable list shows it, polled from its public flags.</summary>
internal enum TickableState
{
    Pending,
    Delay,
    Running,
    Paused,
    Detached,
    Completed,
    Canceled,
}
