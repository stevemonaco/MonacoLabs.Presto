using Avalonia.Input;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge.Platform;

/// <summary>
/// Runs the dispatcher's <c>Input</c>-priority jobs every 16ms while a touch scroll is coasting, so each inertia step runs.
/// </summary>
/// <remarks>
/// Works around Avalonia 12.1 on Android: the dispatcher runs <c>Input</c> jobs only while the looper's queue reports idle,
/// which on recent Android it does not while any message is queued, even a timer due later; an inertia step is such a job,
/// so a fling stalls while any <see cref="DispatcherTimer"/> is armed. Remove once Avalonia checks pending input reliably.
/// </remarks>
internal static class InertiaWake
{
    /// <summary>A gesture left without its ended event stops the wake after this long.</summary>
    internal const long MaxCoastMs = 10_000;

    private static readonly HashSet<int> s_coasting = [];
    private static DispatcherTimer? s_timer;
    private static long s_startedAt;

    /// <summary>Whether the wake timer is running.</summary>
    internal static bool Waking => s_timer is not null;

    public static void Install()
    {
        InputElement.ScrollGestureInertiaStartingEvent.AddClassHandler<InputElement>(static (_, e) => Start(e.Id), handledEventsToo: true);
        InputElement.ScrollGestureEndedEvent.AddClassHandler<InputElement>(static (_, e) => End(e.Id), handledEventsToo: true);
    }

    private static void Start(int id)
    {
        // The event bubbles, so every ancestor on the route reports the same gesture.
        if (!s_coasting.Add(id)) return;
        s_startedAt = Environment.TickCount64;
        // Above Input, so the timer itself is not held back by the idle check it works around.
        s_timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Default, static (_, _) => Wake());
    }

    private static void End(int id)
    {
        if (s_coasting.Remove(id) && s_coasting.Count == 0) Stop();
    }

    private static void Wake()
    {
        if (Environment.TickCount64 - s_startedAt > MaxCoastMs)
        {
            s_coasting.Clear();
            Stop();
            return;
        }
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Input);
    }

    private static void Stop()
    {
        s_timer?.Stop();
        s_timer = null;
    }
}
