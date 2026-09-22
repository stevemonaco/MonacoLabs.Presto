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
public sealed class AvaloniaFrameSource : IFrameSource
{
    private static readonly TimeSpan s_fallbackInterval = TimeSpan.FromMilliseconds(1000d / 60);
    private static WeakReference<Visual>? s_animated;

    private readonly long _origin = Stopwatch.GetTimestamp();
    private readonly Action<TimeSpan> _requestedFrame;
    private Action? _onFrame;
    private DispatcherTimer? _timer;
    private bool _requested;

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

    public void Start(Action onFrame)
    {
        if (_onFrame is not null) return;
        _onFrame = onFrame;
        Pump();
    }

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
        onFrame();
        // Re-request only while still started, so a Stop() from inside the callback retires the pump.
        if (_onFrame is not null) Pump();
    }

    private void FallbackFrame()
    {
        if (_onFrame is not { } onFrame) return;
        onFrame();
        // A top level that appeared since the last frame takes the pump back off the timer.
        if (_onFrame is null || FindTopLevel() is null) return;
        _timer?.Stop();
        _timer = null;
        Pump();
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
