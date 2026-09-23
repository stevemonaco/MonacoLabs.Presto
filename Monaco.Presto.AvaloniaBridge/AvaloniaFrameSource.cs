using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Pumps the engine from <see cref="TopLevel.RequestAnimationFrame"/>, re-requested every frame, and from a
/// render-priority <see cref="DispatcherTimer"/> while the application has no <see cref="TopLevel"/> yet.
/// </summary>
/// <remarks>
/// The <see cref="TopLevel"/> comes from the application lifetime, or, when that exposes none, from the last visual the
/// bridge animated. An <see cref="IActivityApplicationLifetime"/> (Android) publishes only a factory for its main view and
/// never the view itself, so there the timer pumps until the first visual is animated.
/// </remarks>
public sealed class AvaloniaFrameSource : IFrameSource
{
    private static readonly TimeSpan s_fallbackInterval = TimeSpan.FromMilliseconds(1000d / 60);
    private static WeakReference<Visual>? s_animated;

    private readonly long _origin = Stopwatch.GetTimestamp();
    private readonly Action<TimeSpan> _requestedFrame;
    private Action? _onFrame;
    private DispatcherTimer? _timer;
    private bool _requested;

    /// <summary>Creates a frame source; <see cref="PrestoAvalonia.EnsureInitialized"/> already gives the engine one, so applications rarely construct their own.</summary>
    public AvaloniaFrameSource() => _requestedFrame = RequestedFrame;

    /// <summary>Milliseconds since this source was created, read from a monotonic stopwatch so it is also valid outside a frame.</summary>
    public double Now => Stopwatch.GetElapsedTime(_origin).TotalMilliseconds;

    /// <summary>True while frames come from the <see cref="DispatcherTimer"/> fallback instead of a <see cref="TopLevel"/>.</summary>
    internal bool IsUsingFallbackTimer => _timer is not null;

    /// <summary>Remembers a visual the bridge animates, so a <see cref="TopLevel"/> is reachable even when the lifetime exposes no window.</summary>
    internal static void Seed(Visual visual)
    {
        if (s_animated is { } animated) animated.SetTarget(visual);
        else s_animated = new WeakReference<Visual>(visual);
    }

    /// <inheritdoc/>
    public void Start(Action onFrame)
    {
        if (_onFrame is not null) return;
        _onFrame = onFrame;
        Pump();
    }

    /// <inheritdoc/>
    public void Stop()
    {
        _onFrame = null;
        _timer?.Stop();
        _timer = null;
    }

    private void Pump()
    {
        // A request Stop() left queued still calls back and resumes the chain, so a restart must not add a second one.
        if (_requested) return;
        if (FindTopLevel() is { } topLevel)
        {
            _requested = true;
            topLevel.RequestAnimationFrame(_requestedFrame);
            return;
        }
        _timer ??= new DispatcherTimer(s_fallbackInterval, DispatcherPriority.Render, Dispatcher.UIThread, (_, _) => FallbackFrame());
    }

    private void RequestedFrame(TimeSpan _)
    {
        _requested = false;
        if (_onFrame is not { } onFrame) return;
        var completed = false;
        try
        {
            onFrame();
            completed = true;
        }
        finally
        {
            // Re-request only while still started, so a Stop() from inside the callback retires the pump.
            if (_onFrame is not null)
            {
                if (completed) Pump();
                else Rebuild();
            }
        }
    }

    private void FallbackFrame()
    {
        if (_onFrame is not { } onFrame) return;
        var completed = false;
        try
        {
            onFrame();
            completed = true;
        }
        finally
        {
            if (!completed) Rebuild();
            else if (_onFrame is not null && FindTopLevel() is not null)
            {
                // A top level that appeared since the last frame takes the pump back off the timer.
                _timer?.Stop();
                _timer = null;
                Pump();
            }
        }
    }

    /// <summary>
    /// Re-arms the pump after a frame callback threw. Neither Avalonia delivery mechanism re-arms itself once its callback
    /// has faulted, and a request made while the exception unwinds through the frame dispatch is dropped, so the pump is
    /// rebuilt from a dispatcher turn of its own.
    /// </summary>
    private void Rebuild()
    {
        _timer?.Stop();
        _timer = null;
        Dispatcher.UIThread.Post(() =>
        {
            if (_onFrame is not null) Pump();
        }, DispatcherPriority.Render);
    }

    private static TopLevel? FindTopLevel() => Application.Current?.ApplicationLifetime switch
    {
        IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow ?? FirstWindow(desktop),
        ISingleViewApplicationLifetime singleView => TopLevel.GetTopLevel(singleView.MainView),
        _ => null,
    } ?? AnimatedTopLevel();

    private static Window? FirstWindow(IClassicDesktopStyleApplicationLifetime desktop) =>
        desktop.Windows.Count > 0 ? desktop.Windows[0] : null;

    private static TopLevel? AnimatedTopLevel() =>
        s_animated is { } animated && animated.TryGetTarget(out var visual) ? TopLevel.GetTopLevel(visual) : null;
}
