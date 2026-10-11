using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Draggables;
using MonacoLabs.Presto.Transforms;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto;

/// <summary>
/// The drag <see cref="Reorder.EnabledProperty"/> keeps on a list. It owns the pointer through a capture on the list, since a
/// move takes the capture from the moved container, and tracks the dragged item from container to container.
/// </summary>
internal sealed class ReorderList
{
    private static readonly Dictionary<Type, Action<object, int, int>?> s_movers = [];

    private readonly ItemsControl _list;
    private IPointer? _pointer;
    private TopLevel? _root;
    private Point _pointerAt;
    private Point _pressedAt;
    private Timer? _hold;
    private bool _held;
    private Control? _pressed;
    private Orientation _axis;
    private double _grabAt;
    private int _start;
    private ScrollViewer? _scroller;
    private Timer? _scrolling;
    private bool _dragging;
    private object? _item;
    private Control? _container;
    private double _slotAtGrab;
    private double _offset;
    private Animation? _lift;
    private IDisposable? _raised;
    private Animation? _settle;

    public ReorderList(ItemsControl list)
    {
        _list = list;
        list.AddHandler(InputElement.PointerPressedEvent, HandlePressed, RoutingStrategies.Tunnel);
        list.AddHandler(InputElement.PointerMovedEvent, HandleMoved, RoutingStrategies.Tunnel);
        list.AddHandler(InputElement.PointerReleasedEvent, HandleReleased, RoutingStrategies.Tunnel);
        list.AddHandler(InputElement.KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        list.PointerCaptureLost += HandleCaptureLost;
        list.DetachedFromVisualTree += HandleDetached;
    }

    private TransformShorthand Shorthand => _axis == Orientation.Vertical ? TransformShorthand.Y : TransformShorthand.X;

    public void Release()
    {
        _list.RemoveHandler(InputElement.PointerPressedEvent, HandlePressed);
        _list.RemoveHandler(InputElement.PointerMovedEvent, HandleMoved);
        _list.RemoveHandler(InputElement.PointerReleasedEvent, HandleReleased);
        _list.RemoveHandler(InputElement.KeyDownEvent, HandleKeyDown);
        _list.PointerCaptureLost -= HandleCaptureLost;
        _list.DetachedFromVisualTree -= HandleDetached;
        Drop(settle: false);
    }

    private static void MoveIn<T>(object items, int from, int to) => ((ObservableCollection<T>)items).Move(from, to);

    private static Action<object, int, int>? MoverOf(object? items)
    {
        if (items is null) return null;
        var type = items.GetType();
        if (s_movers.TryGetValue(type, out var move)) return move;
        for (var t = type; t is not null && move is null; t = t.BaseType)
        {
            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(ObservableCollection<>)) continue;
            move = typeof(ReorderList).GetMethod(nameof(MoveIn), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(t.GenericTypeArguments[0]).CreateDelegate<Action<object, int, int>>();
        }
        return s_movers[type] = move;
    }

    private static bool HasHandle(Control container)
    {
        foreach (var visual in container.GetVisualDescendants())
        {
            if (visual is Control control && Reorder.GetHandle(control)) return true;
        }
        return false;
    }

    private double Along(Point point) => _axis == Orientation.Vertical ? point.Y : point.X;

    private double Slot(Control container) => _axis == Orientation.Vertical ? container.Bounds.Y : container.Bounds.X;

    private double Size(Control container) => _axis == Orientation.Vertical ? container.Bounds.Height : container.Bounds.Width;

    private Control? ContainerUnder(Visual? source, bool anywhere = false)
    {
        var onHandle = false;
        for (var visual = source; visual is not null && visual != _list; visual = visual.GetVisualParent())
        {
            if (visual is not Control control) continue;
            onHandle |= Reorder.GetHandle(control);
            if (control.GetVisualParent() != _list.ItemsPanelRoot || _list.IndexFromContainer(control) < 0) continue;
            return anywhere || onHandle || !HasHandle(control) ? control : null;
        }
        return null;
    }

    private void HandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (_pointer is not null || !e.GetCurrentPoint(_list).Properties.IsLeftButtonPressed) return;
        if (ContainerUnder(e.Source as Visual) is not { } container) return;
        _pointer = e.Pointer;
        _root = TopLevel.GetTopLevel(_list);
        _pointerAt = e.GetPosition(_root);
        _pressedAt = _pointerAt;
        _pressed = container;
        _axis = Reorder.GetAxis(_list);
        _grabAt = Along(e.GetPosition(_list.ItemsPanelRoot));
        var hold = Reorder.GetTouchHold(_list);
        if (e.Pointer.Type == PointerType.Mouse || !(hold > 0)) return;
        using var origin = PrestoDiagnostics.Origin("Reorder");
        _hold = Presto.CreateTimer(new TimerParams { Duration = hold, OnComplete = _ => Lift() });
    }

    private void HandleMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer != _pointer) return;
        _pointerAt = e.GetPosition(_root);
        if (_hold is not null)
        {
            // Moving before the hold is up leaves the touch or pen to the scroller or a swipe.
            if (Point.Distance(_pointerAt, _pressedAt) > Draggable.TouchSlop) Drop(settle: false);
            return;
        }
        if (!_dragging)
        {
            var moved = _pointerAt - _pressedAt;
            var (along, across) = _axis == Orientation.Vertical ? (Math.Abs(moved.Y), Math.Abs(moved.X)) : (Math.Abs(moved.X), Math.Abs(moved.Y));
            if (along <= Draggable.DefaultDragThreshold && across <= Draggable.DefaultDragThreshold) return;
            // Across first: the press belongs to a crosswise gesture, such as a swipe, for the rest of it.
            if (across >= along)
            {
                Drop(settle: false);
                return;
            }
            if (!Grab()) return;
        }
        e.Handled = true;
        Drag();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (_dragging)
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            Cancel();
            return;
        }
        if (e.KeyModifiers != KeyModifiers.Alt) return;
        var step = (Reorder.GetAxis(_list), e.Key) switch
        {
            (Orientation.Vertical, Key.Down) or (Orientation.Horizontal, Key.Right) => 1,
            (Orientation.Vertical, Key.Up) or (Orientation.Horizontal, Key.Left) => -1,
            _ => 0,
        };
        if (step == 0 || ContainerUnder(e.Source as Visual, anywhere: true) is not { } container) return;
        if (_list.ItemFromContainer(container) is not { } item) return;
        e.Handled = true;
        var from = _list.IndexFromContainer(container);
        if (from + step >= 0 && from + step < _list.ItemCount && Move(item, from, from + step)) _list.ContainerFromItem(item)?.Focus();
    }

    private void HandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer != _pointer) return;
        e.Handled = _dragging;
        var inPlace = _held && _container is { } container && _list.IndexFromContainer(container) == _start && Math.Abs(_offset) <= Draggable.TouchSlop;
        var item = _item;
        Drop(settle: true);
        if (inPlace && Reorder.GetHoldCommand(_list) is { } command && command.CanExecute(item)) command.Execute(item);
    }

    private void HandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_dragging && e.Pointer == _pointer) Drop(settle: true);
    }

    private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e) => Drop(settle: false);

    private void HandleLayoutUpdated(object? sender, EventArgs e) => Follow();

    private void Lift()
    {
        _hold = null;
        if (_pointer is null || _dragging || !Grab()) return;
        _held = true;
        Drag();
    }

    private bool Grab()
    {
        Finish();
        if (_list.ItemFromContainer(_pressed!) is not { } item) return false;
        _dragging = true;
        _item = item;
        _start = _list.IndexFromContainer(_pressed!);
        _slotAtGrab = Slot(_pressed!);
        _pointer!.Capture(_list);
        Raise(_pressed!);
        _scroller = _list.ItemsPanelRoot?.FindAncestorOfType<ScrollViewer>();
        using var origin = PrestoDiagnostics.Origin("Reorder");
        _scrolling = Presto.CreateTimer(new TimerParams { OnUpdate = _ => AutoScroll() });
        _root?.AddHandler(InputElement.KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        if (Reorder.GetCommand(_list) is null && MoverOf(_list.ItemsSource) is null)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(
                _list,
                "Reorder cannot move the items of this {Type}: its ItemsSource is not an ObservableCollection<T>, so set Reorder.Command to move them.",
                _list.GetType().Name);
        }
        _list.LayoutUpdated += HandleLayoutUpdated;
        return true;
    }

    private double Offset() => Along(_root?.TranslatePoint(_pointerAt, _list.ItemsPanelRoot!) ?? default) - _grabAt;

    private void Drag()
    {
        Follow();
        Swap();
    }

    // Dragged every frame: the content can scroll under a pointer that does not move, by this edge band or otherwise.
    private void AutoScroll()
    {
        if (_scroller is { } scroller && _root?.TranslatePoint(_pointerAt, scroller) is { } point)
        {
            var delta = Draggable.EdgeScroll(point, scroller.Viewport, Draggable.DefaultScrollSpeed, Draggable.DefaultScrollThreshold);
            var offset = scroller.Offset;
            var next = _axis == Orientation.Vertical
                ? offset.WithY(Draggable.ClampScroll(offset.Y + delta.Y, scroller.Extent.Height, scroller.Viewport.Height))
                : offset.WithX(Draggable.ClampScroll(offset.X + delta.X, scroller.Extent.Width, scroller.Viewport.Width));
            if (next != offset)
            {
                scroller.Offset = next;
                _list.UpdateLayout();
            }
        }
        Drag();
    }

    private void Cancel()
    {
        var index = _list.IndexFromContainer(_container!);
        while (index != _start && Step(index, index + Math.Sign(_start - index))) index = _list.IndexFromContainer(_container!);
        Drop(settle: true);
    }

    // Keeps the dragged item under the pointer from whichever container holds it.
    private void Follow()
    {
        var next = _list.ContainerFromItem(_item!);
        if (!ReferenceEquals(next, _container))
        {
            Lower();
            if (next is null)
            {
                Drop(settle: false);
                return;
            }
            Raise(next);
            _lift!.Seek(_lift.Duration);
        }
        var container = _container!;
        _offset = Offset();
        RenderTransformComposer.Set(container, Shorthand, _offset - (Slot(container) - _slotAtGrab));
    }

    // One step per crossed neighbor midpoint, so several crossings in one pointer move arrive in order.
    private void Swap()
    {
        while (_dragging && _container is { } container)
        {
            var index = _list.IndexFromContainer(container);
            var center = _slotAtGrab + _offset + (Size(container) / 2);
            var to = Crosses(index + 1, center) ? index + 1 : index > 0 && Crosses(index - 1, center) ? index - 1 : -1;
            if (to < 0 || !Step(index, to)) return;
        }
    }

    private bool Crosses(int index, double center)
    {
        if (_list.ContainerFromIndex(index) is not { } neighbor) return false;
        var mid = Slot(neighbor) + (Size(neighbor) / 2);
        return index > _list.IndexFromContainer(_container!) ? center > mid : center < mid;
    }

    private bool Step(int from, int to)
    {
        if (!Move(_item!, from, to)) return false;
        Follow();
        // A move takes focus from the container, and focus is what keeps it realized in a virtualized drag.
        _container?.Focus();
        return _dragging && _list.IndexFromContainer(_container!) != from;
    }

    private bool Move(object item, int from, int to)
    {
        var args = new ReorderMovingEventArgs(item, from, to);
        _list.RaiseEvent(args);
        if (args.Cancel) return false;
        var selecting = _list as SelectingItemsControl;
        var selected = selecting is not null && Equals(selecting.SelectedItem, item);
        if (Reorder.GetCommand(_list) is { } command)
        {
            if (!command.CanExecute(args)) return false;
            command.Execute(args);
        }
        else if (MoverOf(_list.ItemsSource) is { } move)
        {
            move(_list.ItemsSource!, from, to);
        }
        else
        {
            return false;
        }
        // A collection Move drops the moved item from the selection.
        if (selected) selecting!.SelectedItem = item;
        // Laid out now so the next crossing reads the new slots and the rebase lands before the frame renders.
        _list.UpdateLayout();
        return true;
    }

    private void Raise(Control container)
    {
        _container = container;
        Animate.ItemArmOf(container)?.Suspend();
        _raised =container.SetValue(Panel.ZIndexProperty, int.MaxValue, BindingPriority.Animation);
        container.Focus();
        PrestoAvalonia.EnsureInitialized();
        using var origin = PrestoDiagnostics.Origin("Reorder");
        _lift = Presto.Animate(container, (Reorder.GetLift(_list) ?? Recipes.ReorderLift()).ToParams());
    }

    private void Lower()
    {
        _settle?.Cancel();
        _settle = null;
        _lift?.Revert();
        _lift = null;
        _raised?.Dispose();
        _raised = null;
        if (_container is not { } container) return;
        RenderTransformComposer.Revert(container, Shorthand);
        Animate.ItemArmOf(container)?.Resume();
        _container = null;
    }

    private void Drop(bool settle)
    {
        _hold?.Cancel();
        _hold = null;
        _held = false;
        if (_pointer is { } pointer)
        {
            // Cleared first: dropping the capture raises PointerCaptureLost, which lands back here.
            _pointer = null;
            if (ReferenceEquals(pointer.Captured, _list)) pointer.Capture(null);
        }
        _pressed = null;
        var dragged = _dragging;
        if (dragged)
        {
            _dragging = false;
            _list.LayoutUpdated -= HandleLayoutUpdated;
            _root?.RemoveHandler(InputElement.KeyDownEvent, HandleKeyDown);
            _scrolling?.Cancel();
            _scrolling = null;
        }
        if (!settle)
        {
            Finish();
            return;
        }
        if (!dragged || _container is not { } container) return;
        _lift?.Reverse();
        using var origin = PrestoDiagnostics.Origin("Reorder");
        _settle = Presto.Animate(container, new AnimationParams { [Shorthand.ToString()] = 0, Ease = Eases.Spring(), OnComplete = _ => Finish() });
    }

    private void Finish()
    {
        Lower();
        _item = null;
    }
}
