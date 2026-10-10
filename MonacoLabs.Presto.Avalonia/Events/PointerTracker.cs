using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Events;

/// <summary>
/// The last-moved present pointer over one surface, shared by every <see cref="PrestoAvalonia.PointerProximityValue"/> and
/// <see cref="PrestoAvalonia.PointerOffsetValue"/> tracked over it, which it recomputes on each move and layout pass.
/// </summary>
internal sealed class PointerTracker
{
    internal static readonly AttachedProperty<PointerTracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<PointerTracker, InputElement, PointerTracker?>("PointerTracker");

    private readonly InputElement _surface;
    private readonly List<Entry> _entries = [];
    private IPointer? _pointer;
    private Point _position;

    private PointerTracker(InputElement surface)
    {
        _surface = surface;
        surface.AddHandler(InputElement.PointerMovedEvent, HandleMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        surface.AddHandler(InputElement.PointerPressedEvent, HandleMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        surface.AddHandler(InputElement.PointerReleasedEvent, HandleReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        surface.AddHandler(InputElement.PointerExitedEvent, HandleExited, RoutingStrategies.Direct, handledEventsToo: true);
        surface.DetachedFromVisualTree += HandleSurfaceDetached;
        surface.LayoutUpdated += HandleLayoutUpdated;
        surface.SetValue(TrackerProperty, this);
    }

    /// <summary>
    /// An offset value along <paramref name="axis"/>, or with no axis a proximity value, tracked over <paramref name="over"/> or
    /// <paramref name="control"/>'s top level once it has one.
    /// </summary>
    internal static MotionValue Track(Control control, InputElement? over, Orientation? axis, double radius = 0, bool fromCenter = false)
    {
        var entry = new Entry(control, over, axis, radius, fromCenter);
        // A TextBlock re-hosts split pieces on every arrange, so over a surface the value lives as long as the surface.
        return MotionValueSource.Listen(over ?? (AvaloniaObject)control, entry.Value, entry.Stop);
    }

    private void Add(Entry entry)
    {
        _entries.Add(entry);
        entry.Update();
    }

    private void Remove(Entry entry)
    {
        _entries.Remove(entry);
        if (_entries.Count > 0) return;
        _surface.RemoveHandler(InputElement.PointerMovedEvent, HandleMoved);
        _surface.RemoveHandler(InputElement.PointerPressedEvent, HandleMoved);
        _surface.RemoveHandler(InputElement.PointerReleasedEvent, HandleReleased);
        _surface.RemoveHandler(InputElement.PointerExitedEvent, HandleExited);
        _surface.DetachedFromVisualTree -= HandleSurfaceDetached;
        _surface.LayoutUpdated -= HandleLayoutUpdated;
        _surface.ClearValue(TrackerProperty);
    }

    private void HandleMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch && !e.Properties.IsLeftButtonPressed) return;
        _pointer = e.Pointer;
        _position = e.GetPosition(_surface);
        UpdateAll();
    }

    private void HandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch) HandleGone(e.Pointer);
    }

    private void HandleExited(object? sender, PointerEventArgs e) => HandleGone(e.Pointer);

    private void HandleGone(IPointer pointer)
    {
        if (pointer != _pointer) return;
        _pointer = null;
        UpdateAll();
    }

    private void HandleLayoutUpdated(object? sender, EventArgs e)
    {
        if (_pointer != null) UpdateAll();
    }

    private void UpdateAll()
    {
        for (var i = 0; i < _entries.Count; i++) _entries[i].Update();
    }

    private void HandleSurfaceDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        for (var i = _entries.Count - 1; i >= 0; i--) _entries[i].Stop();
    }

    private sealed class Entry
    {
        private readonly Control _control;
        private readonly InputElement? _over;
        private readonly Orientation? _axis;
        private readonly double _radius;
        private readonly bool _fromCenter;
        private PointerTracker? _tracker;
        private bool _stopped;

        public Entry(Control control, InputElement? over, Orientation? axis, double radius, bool fromCenter)
        {
            _control = control;
            _over = over;
            _axis = axis;
            _radius = radius;
            _fromCenter = fromCenter;
            Value = new MotionValue(Engine.Current, [], 0);
            if (over != null || control.IsAttachedToVisualTree()) Start();
            else control.AttachedToVisualTree += HandleAttached;
        }

        public MotionValue Value { get; }

        public void Stop()
        {
            if (_stopped) return;
            _stopped = true;
            _control.AttachedToVisualTree -= HandleAttached;
            _tracker?.Remove(this);
        }

        public void Update()
        {
            var tracker = _tracker!;
            var position = tracker._position;
            if (tracker._pointer is null || _control.GetVisualParent() is not { } parent || parent.TransformToVisual(tracker._surface) is not { } transform)
            {
                Value.Assign(0);
                return;
            }
            // Bounds is the layout slot, so the control's own render transform never feeds back into what it measures.
            var bounds = _control.Bounds.TransformToAABB(transform);
            if (_axis is { } axis)
            {
                Value.Assign(axis == Orientation.Horizontal ? position.X - bounds.Center.X : position.Y - bounds.Center.Y);
                return;
            }
            var nearest = _fromCenter
                ? bounds.Center
                : new Point(Math.Clamp(position.X, bounds.Left, bounds.Right), Math.Clamp(position.Y, bounds.Top, bounds.Bottom));
            var distance = Point.Distance(position, nearest);
            Value.Assign(1 - Math.Clamp(distance / _radius, 0, 1));
        }

        private void Start()
        {
            _control.AttachedToVisualTree -= HandleAttached;
            var surface = _over ?? TopLevel.GetTopLevel(_control)!;
            _tracker = surface.GetValue(TrackerProperty) ?? new PointerTracker(surface);
            _tracker.Add(this);
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e) => Start();
    }
}
