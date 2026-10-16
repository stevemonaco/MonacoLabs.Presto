using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MonacoLabs.Presto.Platform;

namespace MonacoLabs.Presto;

/// <summary>
/// Pumps the engine from <see cref="TopLevel.RequestAnimationFrame"/>, re-requested every frame, and from a
/// render-priority <see cref="DispatcherTimer"/> while the application has no <see cref="TopLevel"/> yet.
/// </summary>
/// <remarks>
/// The <see cref="TopLevel"/> comes from the application lifetime, or, when that exposes none, from the last visual the
/// bridge animated, or else the last top level such a visual was found in. An <see cref="IActivityApplicationLifetime"/>
/// (Android) publishes only a factory for its main view and never the view itself, so there the timer pumps until the first
/// visual is animated. Frame times from a <see cref="TopLevel"/> are snapped to the display's refresh interval (see
/// <see cref="Paced"/>); the timer's are the wall clock.
/// </remarks>
public sealed class AvaloniaFrameSource : IFrameSource
{
    private static readonly TimeSpan s_fallbackInterval = TimeSpan.FromMilliseconds(1000d / 60);
    private static WeakReference<Visual>? s_animated;
    private static WeakReference<TopLevel>? s_lastTopLevel;

    private readonly TimeProvider _timeProvider;
    private readonly long _origin;
    private readonly Action<TimeSpan> _requestedFrame;
    private readonly FramePacer _pacer = new();
    private Action? _onFrame;
    private DispatcherTimer? _timer;
    private bool _requested;
    private bool _paced = true;
    private bool _inFrame;
    private double _lastFrame;

    /// <summary>Creates a frame source; <see cref="PrestoAvalonia.EnsureInitialized"/> already gives the engine one, so applications rarely construct their own.</summary>
    public AvaloniaFrameSource() : this(TimeProvider.System)
    {
    }

    /// <summary>Creates a frame source that reads <see cref="Now"/> from <paramref name="timeProvider"/> instead of the system clock.</summary>
    public AvaloniaFrameSource(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _origin = timeProvider.GetTimestamp();
        _requestedFrame = RequestedFrame;
    }

    /// <summary>
    /// Milliseconds since this source was created, read from its <see cref="TimeProvider"/> so it is also valid outside a
    /// frame, but never below the last frame's time; during a frame callback it is that frame's time.
    /// </summary>
    public double Now => _inFrame ? _lastFrame : Math.Max(Wall, _lastFrame);

    /// <summary>
    /// Whether frame times from a <see cref="TopLevel"/> are snapped to the display's refresh interval, so motion advances
    /// evenly per displayed frame; set false for the raw dispatch time. True by default.
    /// </summary>
    public bool Paced
    {
        get => _paced;
        set
        {
            if (value && !_paced) _pacer.Restart();
            _paced = value;
        }
    }

    private double Wall => _timeProvider.GetElapsedTime(_origin).TotalMilliseconds;

    /// <summary>True while frames come from the <see cref="DispatcherTimer"/> fallback instead of a <see cref="TopLevel"/>.</summary>
    internal bool IsUsingFallbackTimer => _timer is not null;

    /// <summary>Remembers a visual the bridge animates, so a <see cref="TopLevel"/> is reachable even when the lifetime exposes no window.</summary>
    internal static void Seed(Visual visual)
    {
        if (s_animated is { } animated) animated.SetTarget(visual);
        else s_animated = new WeakReference<Visual>(visual);
    }

    /// <summary>Drops the remembered visual and top level, so only the lifetime can supply a <see cref="TopLevel"/>.</summary>
    internal static void Forget()
    {
        s_animated = null;
        s_lastTopLevel = null;
    }

    /// <inheritdoc/>
    public void Start(Action onFrame)
    {
        if (_onFrame is not null) return;
        _onFrame = onFrame;
        _pacer.Restart();
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
        var wall = Wall;
        BeginFrame(_paced ? _pacer.Frame(wall) : wall);
        var completed = false;
        try
        {
            onFrame();
            completed = true;
        }
        finally
        {
            _inFrame = false;
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
        BeginFrame(Wall);
        var completed = false;
        try
        {
            onFrame();
            completed = true;
        }
        finally
        {
            _inFrame = false;
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

    private void BeginFrame(double time)
    {
        _lastFrame = Math.Max(time, _lastFrame);
        _inFrame = true;
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

    private static TopLevel? AnimatedTopLevel()
    {
        // The last animated visual may be detached (a measuring TextBlock, a page navigated away from); falling back to the
        // timer then starves Android's Input-priority jobs, scroll inertia among them, so the last top level found is kept.
        if (s_animated is { } animated && animated.TryGetTarget(out var visual) && TopLevel.GetTopLevel(visual) is { } topLevel)
        {
            if (s_lastTopLevel is { } last) last.SetTarget(topLevel);
            else s_lastTopLevel = new WeakReference<TopLevel>(topLevel);
            return topLevel;
        }
        return s_lastTopLevel is { } remembered && remembered.TryGetTarget(out var found) ? found : null;
    }
}
