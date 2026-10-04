using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Monaco.Presto.Core;
using Monaco.Presto.Diagnostics;

namespace Monaco.Presto.AvaloniaBridge.Events;

/// <summary>
/// Port of anime.js's <c>ScrollContainer</c>: the scroll state one <see cref="ScrollViewer"/>'s observers share —
/// offset, velocity and direction — sampled by tickers that only run while the scroller is moving.
/// </summary>
/// <remarks>
/// One container exists per scroller for as long as an observer, a <see cref="PrestoAvalonia.ScrollValue"/> or a
/// <see cref="Scroll.SpeedProperty"/> element watches it, and reverts with the last of them.
/// The data timer is paused with the scroll ticker when the wake window closes — a deliberate divergence from upstream,
/// where it runs on forever — so the engine runs out of children and can sleep while an idle observer is alive.
/// </remarks>
public sealed class ScrollContainer
{
    private const double DataFrameRate = 30;
    private const double ResizeDuration = 250;
    private const double WakeDuration = 500;
    private const RoutingStrategies PointerRoutes = RoutingStrategies.Tunnel | RoutingStrategies.Bubble;

    internal static readonly Dictionary<ScrollViewer, ScrollContainer> Containers = [];

    private readonly List<ScrollObserver> _observers = [];
    private readonly List<(MotionValue Value, bool Horizontal, bool Progress)> _values = [];
    private readonly List<Scroll.Parallax> _parallaxes = [];
    private readonly Timer _scrollTicker;
    private readonly Timer _dataTimer;
    private double _prevScrollX;
    private double _prevScrollY;

    private ScrollContainer(ScrollViewer scroller)
    {
        Scroller = scroller;
        using var origin = PrestoDiagnostics.Origin("ScrollObserver");
        _dataTimer = Presto.CreateTimer(new TimerParams
        {
            Autoplay = false,
            FrameRate = DataFrameRate,
            OnUpdate = UpdateScrollData,
        });
        _scrollTicker = Presto.CreateTimer(new TimerParams
        {
            Autoplay = false,
            OnUpdate = _ => ForEachObserver(static observer => observer.HandleScroll(), BackwardX || BackwardY),
        });
        ResizeTicker = Presto.CreateTimer(new TimerParams
        {
            Autoplay = false,
            Duration = ResizeDuration * Globals.TimeScale,
            OnComplete = _ => Refresh(),
        });
        WakeTicker = Presto.CreateTimer(new TimerParams
        {
            Autoplay = false,
            Duration = WakeDuration * Globals.TimeScale,
            OnBegin = _ =>
            {
                _scrollTicker.Resume();
                _dataTimer.Resume();
            },
            OnComplete = _ =>
            {
                _scrollTicker.Pause();
                _dataTimer.Pause();
            },
        });

        UpdateScrollCoords();
        HandleScroll();
        scroller.PropertyChanged += HandlePropertyChanged;
        scroller.AddHandler(InputElement.PointerPressedEvent, HandlePointerPressed, PointerRoutes, handledEventsToo: true);
        scroller.AddHandler(InputElement.PointerReleasedEvent, HandlePointerReleased, PointerRoutes, handledEventsToo: true);
        scroller.AddHandler(InputElement.PointerCaptureLostEvent, HandlePointerReleased, PointerRoutes, handledEventsToo: true);
    }

    /// <summary>The scroller this state is sampled from.</summary>
    public ScrollViewer Scroller { get; }

    /// <summary>The scroller's offset on that axis, rounded to whole pixels.</summary>
    public double ScrollX { get; private set; }

    /// <inheritdoc cref="ScrollX"/>
    public double ScrollY { get; private set; }

    /// <summary>Scroll speed in pixels per millisecond, sampled at 30 fps.</summary>
    public double Velocity { get; private set; }

    /// <summary>Whether the last movement on that axis was towards the start of the content.</summary>
    public bool BackwardX { get; private set; }

    /// <inheritdoc cref="BackwardX"/>
    public bool BackwardY { get; private set; }

    /// <summary>Whether a pointer is pressed on the scroller, its scrollbars included.</summary>
    internal bool PointerDown { get; private set; }

    /// <summary>Debounces the observer refresh after the scroller's viewport or content size changes.</summary>
    internal Timer ResizeTicker { get; }

    /// <summary>Keeps the observer ticker and the data timer running for half a second after the last scroll.</summary>
    internal Timer WakeTicker { get; }

    /// <summary>Measures every observer's bounds again and re-reads the scroll position against them.</summary>
    public ScrollContainer Refresh()
    {
        RefreshObservers();
        HandleScroll();
        return this;
    }

    internal static ScrollContainer Register(ScrollViewer scroller, ScrollObserver observer)
    {
        var container = Get(scroller);
        container._observers.Add(observer);
        return container;
    }

    internal static ScrollContainer Register(ScrollViewer scroller, Scroll.Parallax parallax)
    {
        var container = Get(scroller);
        container._parallaxes.Add(parallax);
        return container;
    }

    internal static MotionValue AddValue(ScrollViewer scroller, ScrollAxis axis, bool progress)
    {
        var container = Get(scroller);
        var horizontal = axis == ScrollAxis.Horizontal;
        var value = new MotionValue(Engine.Current, [], container.Read(horizontal, progress));
        container._values.Add((value, horizontal, progress));
        return value;
    }

    internal void Remove(ScrollObserver observer)
    {
        _observers.Remove(observer);
        RevertIfUnused();
    }

    internal void Remove(Scroll.Parallax parallax)
    {
        _parallaxes.Remove(parallax);
        RevertIfUnused();
    }

    internal void RemoveValue(MotionValue value)
    {
        _values.RemoveAt(_values.FindIndex(entry => entry.Value == value));
        RevertIfUnused();
    }

    private static ScrollContainer Get(ScrollViewer scroller)
    {
        if (!Containers.TryGetValue(scroller, out var container)) Containers[scroller] = container = new ScrollContainer(scroller);
        return container;
    }

    private void RevertIfUnused()
    {
        if (_observers.Count > 0 || _values.Count > 0 || _parallaxes.Count > 0) return;
        Scroller.PropertyChanged -= HandlePropertyChanged;
        Scroller.RemoveHandler(InputElement.PointerPressedEvent, HandlePointerPressed);
        Scroller.RemoveHandler(InputElement.PointerReleasedEvent, HandlePointerReleased);
        Scroller.RemoveHandler(InputElement.PointerCaptureLostEvent, HandlePointerReleased);
        _scrollTicker.Cancel();
        _dataTimer.Cancel();
        ResizeTicker.Cancel();
        WakeTicker.Cancel();
        Containers.Remove(Scroller);
    }

    internal void RefreshObservers() => ForEachObserver(static observer =>
    {
        if (!observer.Ready) return;
        observer.Refresh();
        observer.RaiseResize();
    });

    internal void HandleScroll()
    {
        UpdateScrollCoords();
        WakeTicker.Restart();
    }

    private void ForEachObserver(Action<ScrollObserver> action, bool reverse = false)
    {
        // An observer can revert itself from inside the action, so the index is re-read instead of cached.
        for (var i = reverse ? _observers.Count - 1 : 0; i >= 0 && i < _observers.Count;)
        {
            var observer = _observers[i];
            action(observer);
            if (reverse) i--;
            else if (i < _observers.Count && ReferenceEquals(_observers[i], observer)) i++;
        }
    }

    private void HandlePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.OffsetProperty)
        {
            HandleScroll();
            // Pins and parallax move in this notification rather than on the next tick, so they never trail the scroll by a frame.
            ForEachObserver(static observer => observer.HandleOffsetChanged());
            foreach (var parallax in _parallaxes) parallax.Update();
        }
        else if (e.Property == ScrollViewer.ViewportProperty || e.Property == ScrollViewer.ExtentProperty)
        {
            ResizeTicker.Restart();
            UpdateValues();
            // Unlike observers, parallax elements are visible before the debounced refresh, so they measure the viewport they get now.
            foreach (var parallax in _parallaxes) parallax.Measure();
        }
    }

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e) => PointerDown = true;

    private void HandlePointerReleased(object? sender, RoutedEventArgs e)
    {
        if (!PointerDown) return;
        PointerDown = false;
        ForEachObserver(static observer => observer.WaitToSnap());
    }

    private void UpdateScrollCoords()
    {
        var offset = Scroller.Offset;
        ScrollX = Utils.Round(offset.X, 0);
        ScrollY = Utils.Round(offset.Y, 0);
        UpdateValues();
    }

    private void UpdateValues()
    {
        foreach (var (value, horizontal, progress) in _values) value.Assign(Read(horizontal, progress));
    }

    private double Read(bool horizontal, bool progress)
    {
        var scroll = horizontal ? ScrollX : ScrollY;
        if (!progress) return scroll;
        var range = horizontal ? Scroller.Extent.Width - Scroller.Viewport.Width : Scroller.Extent.Height - Scroller.Viewport.Height;
        return range > 0 ? Utils.Clamp(scroll / range, 0, 1) : 0;
    }

    private void UpdateScrollData(Timer self)
    {
        var deltaTime = self.DeltaTime;
        var px = _prevScrollX;
        var py = _prevScrollY;
        var nx = ScrollX;
        var ny = ScrollY;
        var dx = px - nx;
        var dy = py - ny;
        _prevScrollX = nx;
        _prevScrollY = ny;
        if (dx != 0) BackwardX = px > nx;
        if (dy != 0) BackwardY = py > ny;
        Velocity = Utils.Round(deltaTime > 0 ? Math.Sqrt(dx * dx + dy * dy) / deltaTime : 0, 5);
    }
}
