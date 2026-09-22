using System.Collections.Concurrent;
using System.Diagnostics;

namespace Monaco.Presto.Tests;

/// <summary>Installs itself on the current thread for its lifetime; posted callbacks run on whichever thread calls <see cref="RunUntil"/>.</summary>
internal sealed class PumpingSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly SynchronizationContext? _previous = Current;

    public PumpingSynchronizationContext() => SetSynchronizationContext(this);

    public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

    /// <summary>Runs posted callbacks on the calling thread until <paramref name="condition"/> holds or <paramref name="timeout"/> elapses.</summary>
    public void RunUntil(Func<bool> condition, TimeSpan timeout)
    {
        var started = Stopwatch.GetTimestamp();
        while (!condition())
        {
            var remaining = timeout - Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero) return;
            if (_queue.TryTake(out var item, remaining)) item.Callback(item.State);
        }
    }

    public void Dispose() => SetSynchronizationContext(_previous);
}
