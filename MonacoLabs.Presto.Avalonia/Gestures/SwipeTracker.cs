using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Draggables;

namespace MonacoLabs.Presto.Gestures;

/// <summary>The drag, reveal and commit <see cref="Swipe"/> keeps on an element, written through an <see cref="Animatable"/> over its X shorthand.</summary>
internal sealed class SwipeTracker
{
    private const double Friction = .35;
    private const double CommitAt = .5;
    // A row settles in about half a second; the 350 ms default dragged a short release's spring-back out past a second.
    private static readonly Inertia s_inertia = new(0) { TimeConstant = 120 };
    // The open element of each list, keyed by its nearest ItemsControl or, outside one, its parent.
    private static readonly ConditionalWeakTable<Visual, SwipeTracker> s_open = new();

    private readonly Control _row;
    private readonly VelocityTracker _velocity = new();
    private Layer? _leading;
    private Layer? _trailing;
    private Animatable? _animate;
    private const long LongPressGrace = 500;

    private IPointer? _pointer;
    private long? _fingerLiftedAt;
    private Point _pressedAt;
    private bool _dragging;
    // The raw drag position the rubber-banded offset came from, so a grab picks up where the friction left off.
    private double _grabbedAt;
    // The side being revealed while the offset is still 0, as when a context request starts opening it.
    private int _opening;
    private Visual? _scope;
    private TopLevel? _root;
    private Action? _then;

    public SwipeTracker(Control row)
    {
        GestureOwners.Claim(row, this);
        _row = row;
        row.AddHandler(InputElement.PointerPressedEvent, HandlePressed, handledEventsToo: true);
        row.AddHandler(InputElement.PointerMovedEvent, HandleMoved, handledEventsToo: true);
        row.AddHandler(InputElement.PointerReleasedEvent, HandleReleased, handledEventsToo: true);
        row.PointerCaptureLost += HandleCaptureLost;
        row.ContextRequested += HandleContextRequested;
        row.AttachedToVisualTree += HandleAttached;
        row.DataContextChanged += HandleDataContextChanged;
        row.PropertyChanged += HandlePropertyChanged;
        row.SizeChanged += HandleSizeChanged;
    }

    private double X => _animate?.Get("X") ?? 0;

    private bool Commits => Swipe.GetCommand(_row) is not null || Swipe.GetDismiss(_row);

    public void Rebuild()
    {
        Reset();
        Unhost(ref _leading);
        Unhost(ref _trailing);
    }

    public void Release()
    {
        _row.RemoveHandler(InputElement.PointerPressedEvent, HandlePressed);
        _row.RemoveHandler(InputElement.PointerMovedEvent, HandleMoved);
        _row.RemoveHandler(InputElement.PointerReleasedEvent, HandleReleased);
        _row.PointerCaptureLost -= HandleCaptureLost;
        _row.ContextRequested -= HandleContextRequested;
        _row.AttachedToVisualTree -= HandleAttached;
        _row.DataContextChanged -= HandleDataContextChanged;
        _row.PropertyChanged -= HandlePropertyChanged;
        _row.SizeChanged -= HandleSizeChanged;
        Rebuild();
        _animate?.Revert();
        GestureOwners.Release(_row, this);
    }

    private static bool Within(object? source, Visual? ancestor) =>
        ancestor is not null && source is Visual visual && (visual == ancestor || ancestor.IsVisualAncestorOf(visual));

    private static double OpenWidth(Layer? layer) => layer?.View.DesiredSize.Width ?? 0;

    private Animatable Animator()
    {
        if (_animate is not null) return _animate;
        _animate = _row.CreateAnimatable(new AvaloniaAnimatableParams
        {
            ["X"] = 0,
            OnComplete = _ =>
            {
                var then = _then;
                _then = null;
                then?.Invoke();
            },
        });
        _animate.ValueOf("X").Changed += _ => Place();
        return _animate;
    }

    private void HandlePressed(object? sender, PointerPressedEventArgs e)
    {
        // An armed press that never dragged ended out of sight, as when a reorder took its capture, so a new press replaces it.
        if (_dragging || Within(e.Source, _leading) || Within(e.Source, _trailing)) return;
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(_row).Properties.IsLeftButtonPressed) return;
        _pointer = e.Pointer;
        _pressedAt = e.GetPosition(null);
    }

    private void HandleMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer != _pointer) return;
        var moved = e.GetPosition(null) - _pressedAt;
        if (!_dragging)
        {
            var touch = e.Pointer.Type == PointerType.Touch;
            var threshold = touch ? Draggable.TouchSlop : Draggable.DefaultDragThreshold;
            if (Math.Abs(moved.X) <= threshold && Math.Abs(moved.Y) <= threshold) return;
            // Vertical first, or on touch not clearly horizontal: the pointer is left to the ancestor scroller for the rest of this press.
            if (Math.Abs(moved.Y) * (touch ? 2 : 1) >= Math.Abs(moved.X))
            {
                _pointer = null;
                return;
            }
            Grab(e.Pointer);
        }
        e.Handled = true;
        // Gesture recognizers see handled events too, and an ancestor's scroll would steal the capture.
        e.PreventGestureRecognition();
        var (min, max) = Range(commit: true);
        Animator().Set("X", TransformMath.Cushion(_grabbedAt + moved.X, min, max, Friction), 0);
        _velocity.Add(X, Engine.Current.Now);
    }

    private void HandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer != _pointer) return;
        e.Handled = _dragging;
        Lift();
    }

    private void HandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer == _pointer) Lift();
    }

    private void HandleContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || _row.ContextMenu is not null || _row.ContextFlyout is not null || (Swipe.GetLeading(_row) is null && Swipe.GetTrailing(_row) is null)) return;
        // A long press raises a context request during the hold or just after it; on touch the actions are swiped to, and the hold may be a reorder's.
        if (_pointer is { Type: not PointerType.Mouse } || Environment.TickCount64 - _fingerLiftedAt < LongPressGrace) return;
        e.Handled = true;
        Host();
        var x = X;
        var (min, max) = Range(commit: false);
        var target = x == 0 ? (min != 0 ? min : max) : x < 0 ? max : 0;
        MoveTo(0, _ => target, Math.Min(x, target), Math.Max(x, target));
        if (target != 0 && !e.TryGetPosition(null, out _))
        {
            (target < 0 ? _trailing : _leading)!.View.GetSelfAndVisualDescendants().OfType<InputElement>()
                .FirstOrDefault(element => element.Focusable)?.Focus(NavigationMethod.Tab);
        }
    }

    private void HandleRootPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!Within(e.Source, _row)) Close();
    }

    private void HandleRootKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        var refocus = _row.IsKeyboardFocusWithin;
        Close();
        if (refocus) _row.Focus();
    }

    private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e) => Reset();

    private void HandleDataContextChanged(object? sender, EventArgs e) => Reset();

    // Shown again after a dismiss hid it, by anything but Presence's own hold.
    private void HandlePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty && e.NewValue is true && e.Priority != BindingPriority.Animation) Reset();
    }

    private void HandleSizeChanged(object? sender, SizeChangedEventArgs e) => Place();

    private void Grab(IPointer pointer)
    {
        _dragging = true;
        pointer.Capture(_row);
        var animate = Animator();
        _then = null;
        foreach (var animation in animate.Animations.Values) animation.Pause();
        Host();
        _opening = 0;
        var (min, max) = Range(commit: true);
        _grabbedAt = TransformMath.Cushion(X, min, max, 1 / Friction);
        _velocity.Reset();
        _velocity.Add(X, Engine.Current.Now);
        Open(true);
    }

    private void Lift()
    {
        // Cleared first: dropping the capture raises PointerCaptureLost, which lands back here.
        var pointer = _pointer!;
        _pointer = null;
        if (pointer.Type != PointerType.Mouse) _fingerLiftedAt = Environment.TickCount64;
        if (!_dragging)
        {
            if (X != 0) Close();
            return;
        }
        _dragging = false;
        if (ReferenceEquals(pointer.Captured, _row)) pointer.Capture(null);
        var x = X;
        var side = Math.Sign(x);
        if (side != 0 && Commits && Math.Abs(x) > CommitAt * _row.Bounds.Width)
        {
            Commit(side);
            return;
        }
        var (min, max) = Range(commit: false);
        var open = !Swipe.GetReveal(_row) ? 0 : side > 0 ? max : min;
        MoveTo(_velocity.Velocity, t => open != 0 && t * side >= Math.Abs(open) / 2 ? open : 0, side < 0 ? min : 0, side > 0 ? max : 0);
    }

    // How far each side can be dragged: its actions' width, or the whole row when a full swipe commits.
    private (double Min, double Max) Range(bool commit)
    {
        var width = _row.Bounds.Width;
        var full = commit && Commits;
        var bare = full && _leading is null && _trailing is null;
        return (_trailing is null && !bare ? 0 : full ? -width : -OpenWidth(_trailing), _leading is null && !bare ? 0 : full ? width : OpenWidth(_leading));
    }

    // Coasts to snap(projected release), within [lo, hi]; a release outside them lands on the nearer one.
    private void MoveTo(double velocity, Func<double, double> snap, double lo, double hi)
    {
        var animate = Animator();
        _then = null;
        var x = X;
        var rest = Math.Clamp(x < lo || x > hi ? x : snap(x + velocity * s_inertia.TimeConstant), lo, hi);
        _opening = Math.Sign(rest);
        Open(rest != 0);
        Place();
        if (Engine.Current.Defaults.ReducedMotion) animate.Set("X", rest, 0);
        else animate.Fling("X", velocity, s_inertia with { ModifyTarget = snap, Min = lo, Max = hi });
    }

    private void Close()
    {
        if (_animate is not null) MoveTo(0, _ => 0, Math.Min(X, 0), Math.Max(X, 0));
    }

    private void Commit(int side)
    {
        var edge = side > 0 ? Edge.Right : Edge.Left;
        if (!Swipe.GetDismiss(_row))
        {
            Run(edge);
            Close();
            return;
        }
        Open(false);
        _opening = side;
        var to = side * _row.Bounds.Width;
        if (Engine.Current.Defaults.ReducedMotion)
        {
            Animator().Set("X", to, 0);
            Dismissed(edge);
            return;
        }
        var tokens = Recipes.Tokens;
        Animator().Set("X", to, tokens.Short, tokens.Ease);
        _then = () => Dismissed(edge);
    }

    private void Dismissed(Edge edge)
    {
        Run(edge);
        if (_row.IsAttachedToVisualTree() && _row.IsVisible) _row.IsVisible = false;
    }

    private void Run(Edge edge)
    {
        if (Swipe.GetCommand(_row) is { } command && command.CanExecute(edge)) command.Execute(edge);
    }

    private void Reset()
    {
        if (_animate is null) return;
        _then = null;
        _opening = 0;
        Open(false);
        _animate.Set("X", 0, 0);
    }

    private void Open(bool open)
    {
        if (open)
        {
            _scope = _row.FindAncestorOfType<ItemsControl>() ?? _row.GetVisualParent();
            if (_scope is null) return;
            if (s_open.TryGetValue(_scope, out var other) && other != this) other.Close();
            s_open.AddOrUpdate(_scope, this);
            if (_root is not null || TopLevel.GetTopLevel(_row) is not { } root) return;
            _root = root;
            root.AddHandler(InputElement.PointerPressedEvent, HandleRootPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            root.AddHandler(InputElement.KeyDownEvent, HandleRootKeyDown, RoutingStrategies.Tunnel);
            return;
        }
        if (_scope is { } scope && s_open.TryGetValue(scope, out var current) && current == this) s_open.Remove(scope);
        _scope = null;
        _root?.RemoveHandler(InputElement.PointerPressedEvent, HandleRootPressed);
        _root?.RemoveHandler(InputElement.KeyDownEvent, HandleRootKeyDown);
        _root = null;
    }

    private void Host()
    {
        _leading = Hosted(_leading, Swipe.GetLeading(_row), trailing: false);
        _trailing = Hosted(_trailing, Swipe.GetTrailing(_row), trailing: true);
    }

    private Layer? Hosted(Layer? layer, object? actions, bool trailing)
    {
        if (layer is null)
        {
            var view = actions switch
            {
                Control control => control,
                IDataTemplate template => template.Build(_row.DataContext),
                _ => null,
            };
            if (view is null) return null;
            layer = new Layer(view, trailing);
        }
        // Appended, so a panel's children keep their indices; the strip lies outside the row, so draw order does not matter.
        if (layer.GetVisualParent() != _row) Selection.VisualChildren(_row).Add(layer);
        var logical = Selection.LogicalChildren(_row);
        if (!logical.Contains(layer)) logical.Add(layer);
        layer.View.Measure(Size.Infinity);
        return layer;
    }

    private void Unhost(ref Layer? layer)
    {
        if (layer is null) return;
        Selection.VisualChildren(_row).Remove(layer);
        Selection.LogicalChildren(_row).Remove(layer);
        layer = null;
    }

    // Each side's actions fill the strip the row uncovers, at least as wide as they measure.
    private void Place()
    {
        var x = X;
        var side = x != 0 ? Math.Sign(x) : _opening;
        var (width, height, uncovered) = (_row.Bounds.Width, _row.Bounds.Height, Math.Abs(x));
        _leading?.Show(side > 0, new Rect(-uncovered, 0, uncovered, height));
        _trailing?.Show(side < 0, new Rect(width, 0, uncovered, height));
    }

    /// <summary>Clips one side's actions to its strip, which it arranges itself at whatever rect its parent offers.</summary>
    private sealed class Layer : Control
    {
        private readonly bool _trailing;
        private Rect _strip;

        public Layer(Control view, bool trailing)
        {
            View = view;
            _trailing = trailing;
            ClipToBounds = true;
            IsVisible = false;
            VisualChildren.Add(view);
            LogicalChildren.Add(view);
        }

        public Control View { get; }

        public void Show(bool shown, Rect strip)
        {
            IsVisible = shown;
            _strip = strip;
            Arrange(strip);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            View.Measure(Size.Infinity);
            return default;
        }

        protected override void ArrangeCore(Rect finalRect) => base.ArrangeCore(_strip);

        protected override Size ArrangeOverride(Size finalSize)
        {
            var width = Math.Max(finalSize.Width, View.DesiredSize.Width);
            View.Arrange(new Rect(_trailing ? finalSize.Width - width : 0, 0, width, finalSize.Height));
            return finalSize;
        }
    }
}
