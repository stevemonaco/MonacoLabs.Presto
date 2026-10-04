using Avalonia.Threading;

namespace PrestoSample.Showcase;

/// <summary>Stalls the UI thread on purpose, to show which animations keep running without it.</summary>
public static class UiThread
{
    /// <summary>
    /// Blocks the dispatcher for <paramref name="duration"/>. It is posted below Render priority so the clicked button
    /// paints its pressed state before the dispatcher stops answering.
    /// </summary>
    public static void Freeze(TimeSpan duration) =>
        Dispatcher.UIThread.Post(() => Thread.Sleep(duration), DispatcherPriority.Background);
}
