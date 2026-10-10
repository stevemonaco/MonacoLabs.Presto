using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Draggables;
using MonacoLabs.Presto.Gestures;

namespace MonacoLabs.Presto.Controls;

/// <summary>
/// A sheet that slides out from an <see cref="Edge"/> of its host over a scrim and rests on its <see cref="Detents"/>: a
/// drag moves it, and a release coasts through <see cref="Inertia"/> to the detent nearest where the fling would carry it.
/// </summary>
/// <remarks>
/// <para>
/// It fills its host, a page's panel or the window's <see cref="OverlayLayer"/>, and draws its content at the edge, as deep
/// as its highest detent, clipped to the host. The first detent is closed and the rest are open. A sheet closed at 0 hides
/// its content, and opening it shows the content with <see cref="Presence.EnterOnShowProperty"/>, so the entrances inside
/// it play; closing slides it out, then hides it. While it is open, the scrim stops input to the host, and a tap on the
/// scrim, Escape or the platform's back request closes it.
/// </para>
/// <para>
/// A drag on a <see cref="ScrollViewer"/> inside the sheet scrolls it while it can still scroll that way, unless the drag
/// opens the sheet further; otherwise, such as a pull down on a list at its top, the sheet takes the drag. Past its first
/// and last detents the drag rubber-bands, and a fling past them bounces back.
/// </para>
/// <para>
/// Under <see cref="PrestoDefaults.ReducedMotion"/>, read at each release or change of <see cref="DetentIndex"/>, the
/// sheet lands on its detent at once and the scrim fades to match over <see cref="MotionTokens.Short"/>.
/// </para>
/// </remarks>
public class Sheet : Control
{
    private const double Friction = .35;
    private static readonly Inertia s_inertia = new(0);

    /// <summary>The sheet's content, drawn at its edge; it gives the sheet its look.</summary>
    public static readonly StyledProperty<Control?> ContentProperty =
        AvaloniaProperty.Register<Sheet, Control?>(nameof(Content));

    /// <summary>The extents the sheet rests at, from closed to fully open. Defaults to <c>"0,Auto"</c>: hidden, or as deep as its content.</summary>
    public static readonly StyledProperty<SheetDetents> DetentsProperty =
        AvaloniaProperty.Register<Sheet, SheetDetents>(nameof(Detents), SheetDetents.Parse("0,Auto"));

    /// <summary>The index in <see cref="Detents"/> the sheet rests at or is heading for; setting it glides there.</summary>
    public static readonly StyledProperty<int> DetentIndexProperty =
        AvaloniaProperty.Register<Sheet, int>(nameof(DetentIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Whether the sheet rests above its first detent. Setting it true opens a closed sheet to its second detent, and false
    /// closes it to its first.
    /// </summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Sheet, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The edge of the host the sheet slides out from. Defaults to <see cref="MonacoLabs.Presto.Edge.Bottom"/>.</summary>
    public static readonly StyledProperty<Edge> EdgeProperty =
        AvaloniaProperty.Register<Sheet, Edge>(nameof(Edge), Edge.Bottom);

    /// <summary>The scrim over the host, at full strength once the sheet reaches its last detent; null shows none and leaves a tap there to the host.</summary>
    public static readonly StyledProperty<IBrush?> ScrimProperty =
        AvaloniaProperty.Register<Sheet, IBrush?>(nameof(Scrim), Dismissal.Scrim);

    private readonly Border _scrim = new() { IsHitTestVisible = false, Opacity = 0 };
    private readonly Decorator _panel = new();
    private readonly Border _edge = new() { Background = Brushes.Transparent, IsVisible = false };
    private readonly VelocityTracker _velocity = new();
    private readonly MotionValue _position = new(Engine.Current, [], 0);
    private Animatable? _animate;
    private double[] _stops = [];
    private IPointer? _pointer;
    private Point _pressedAt;
    private ScrollViewer? _scroller;
    private bool _dragging;
    // The raw drag extent the rubber-banded one came from, so a grab picks up where the friction left off.
    private double _grabbedAt;
    private bool _settling;

    static Sheet()
    {
        AffectsArrange<Sheet>(EdgeProperty, DetentsProperty);
        ClipToBoundsProperty.OverrideDefaultValue<Sheet>(true);
    }

    /// <summary>Creates a closed sheet.</summary>
    public Sheet()
    {
        _scrim.Bind(Border.BackgroundProperty, this.GetObservable(ScrimProperty));
        _scrim.Tapped += (_, _) => IsOpen = false;
        Dismissal.Attach(this, () => IsOpen, () => IsOpen = false);
        var progress = _position.Map(p => _stops.Length > 1 && _stops[^1] > _stops[0] ? Math.Clamp((p - _stops[0]) / (_stops[^1] - _stops[0]), 0, 1) : 0);
        Follow.Bind(_scrim, OpacityProperty, progress, new FollowParams { Duration = 0, AnimateInitial = true, WhenReduced = Presto.Build().Duration(Recipes.Tokens.Short).Ease(Eases.None) });
        Presence.SetEnterOnShow(_panel, true);
        VisualChildren.Add(_scrim);
        VisualChildren.Add(_panel);
        VisualChildren.Add(_edge);
        LogicalChildren.Add(_panel);
        AddHandler(PointerPressedEvent, HandlePressed, handledEventsToo: true);
        AddHandler(PointerMovedEvent, HandleMoved, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, HandleReleased, handledEventsToo: true);
        PointerCaptureLost += HandleCaptureLost;
    }

    /// <inheritdoc cref="ContentProperty"/>
    [Content]
    public Control? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <inheritdoc cref="DetentsProperty"/>
    public SheetDetents Detents
    {
        get => GetValue(DetentsProperty);
        set => SetValue(DetentsProperty, value);
    }

    /// <inheritdoc cref="DetentIndexProperty"/>
    public int DetentIndex
    {
        get => GetValue(DetentIndexProperty);
        set => SetValue(DetentIndexProperty, value);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="EdgeProperty"/>
    public Edge Edge
    {
        get => GetValue(EdgeProperty);
        set => SetValue(EdgeProperty, value);
    }

    /// <inheritdoc cref="ScrimProperty"/>
    public IBrush? Scrim
    {
        get => GetValue(ScrimProperty);
        set => SetValue(ScrimProperty, value);
    }

    /// <summary>How far the sheet stands out from its edge, in DIPs, as it renders; link content to it with <see cref="MotionValue.Map(Func{double, double})"/> or read its <see cref="MotionValue.Velocity"/>.</summary>
    public MotionValue Position => _position;

    /// <summary>How wide a strip along the edge starts a drag while the sheet is closed and hidden.</summary>
    private protected virtual double EdgeSwipe => 0;

    private bool Horizontal => Edge is Edge.Left or Edge.Right;

    private string Axis => Horizontal ? "X" : "Y";

    // The translate per unit of extent, which is also the extent per unit of pointer travel along the axis.
    private int Direction => Edge is Edge.Top or Edge.Left ? 1 : -1;

    private double Extent => _animate is null ? 0 : Direction * _animate.Get(Axis);

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DetentIndexProperty)
        {
            IsOpen = DetentIndex > 0;
            if (!_settling) MoveTo(DetentIndex, 0);
        }
        else if (change.Property == IsOpenProperty)
        {
            _scrim.IsHitTestVisible = IsOpen;
            if (IsOpen != DetentIndex > 0) DetentIndex = IsOpen ? 1 : 0;
        }
        else if (change.Property == ContentProperty)
        {
            _panel.Child = Content;
        }
        else if (change.Property == EdgeProperty && _animate is not null)
        {
            _animate.Revert();
            _animate = null;
            _stops = [];
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        _scrim.Measure(availableSize);
        _panel.Measure(availableSize);
        // Measured even while the panel is hidden, for an Auto detent.
        Content?.Measure(availableSize);
        var content = Content?.DesiredSize ?? default;
        return new Size(
            double.IsInfinity(availableSize.Width) ? content.Width : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? content.Height : availableSize.Height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var (width, height) = (finalSize.Width, finalSize.Height);
        var length = Horizontal ? width : height;
        var content = Horizontal ? Content?.DesiredSize.Width ?? 0 : Content?.DesiredSize.Height ?? 0;
        var stops = Detents.Select(d => Math.Clamp(d.IsStar ? d.Value * length : d.IsAuto ? content : d.Value, 0, length)).DefaultIfEmpty(0).ToArray();
        var deepest = stops[^1];
        var edge = EdgeSwipe;
        _scrim.Arrange(new Rect(finalSize));
        // Arranged just past the edge, so the translate is the extent itself.
        _panel.Arrange(Edge switch
        {
            Edge.Top => new Rect(0, -deepest, width, deepest),
            Edge.Left => new Rect(-deepest, 0, deepest, height),
            Edge.Right => new Rect(width, 0, deepest, height),
            _ => new Rect(0, height, width, deepest),
        });
        _edge.Arrange(Edge switch
        {
            Edge.Top => new Rect(0, 0, width, edge),
            Edge.Left => new Rect(0, 0, edge, height),
            Edge.Right => new Rect(width - edge, 0, edge, height),
            _ => new Rect(0, height - edge, width, edge),
        });
        if (!stops.AsSpan().SequenceEqual(_stops))
        {
            _stops = stops;
            if (!_dragging) Place();
        }
        return finalSize;
    }

    private Animatable Animator()
    {
        if (_animate is not null) return _animate;
        var (axis, direction) = (Axis, Direction);
        _animate = _panel.CreateAnimatable(new AvaloniaAnimatableParams
        {
            [axis] = 0,
            OnComplete = _ =>
            {
                if (!_dragging && _stops.Length > 0 && _stops[Index(DetentIndex)] == 0) Show(false);
            },
        });
        // The animatable's OnUpdate can run before the property renders in a frame, so the render itself is followed.
        var animate = _animate;
        animate.Animations.Values.Single()._onRender += _ => _position.Assign(direction * animate.Get(axis));
        return animate;
    }

    private void HandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (_pointer is not null || !(e.Source is Visual v && (v == _panel || _panel.IsVisualAncestorOf(v)) || e.Source == _edge)) return;
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pointer = e.Pointer;
        _pressedAt = e.GetPosition(null);
        _scroller = (e.Source as Visual)?.GetSelfAndVisualAncestors().TakeWhile(visual => visual != _panel).OfType<ScrollViewer>().FirstOrDefault();
    }

    private void HandleMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer != _pointer) return;
        var moved = e.GetPosition(null) - _pressedAt;
        var (along, across) = Horizontal ? (moved.X, moved.Y) : (moved.Y, moved.X);
        if (!_dragging)
        {
            if (Math.Abs(along) <= Draggable.DefaultDragThreshold && Math.Abs(across) <= Draggable.DefaultDragThreshold) return;
            if (Math.Abs(across) >= Math.Abs(along) || !TakesFromScroller(along))
            {
                _pointer = null;
                return;
            }
            Grab(e.Pointer);
        }
        e.Handled = true;
        // Gesture recognizers see handled events too, and an inner scroll would steal the capture.
        e.PreventGestureRecognition();
        var extent = TransformMath.Cushion(_grabbedAt + Direction * along, _stops[0], _stops[^1], Friction);
        Animator().Set(Axis, Direction * extent, 0);
        _velocity.Add(extent, Engine.Current.Now);
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

    // A scroller that can still scroll the way the pointer moves keeps the drag, unless it opens the sheet further.
    private bool TakesFromScroller(double along)
    {
        if (_scroller is not { } scroller) return true;
        var (offset, end) = Horizontal
            ? (scroller.Offset.X, scroller.Extent.Width - scroller.Viewport.Width)
            : (scroller.Offset.Y, scroller.Extent.Height - scroller.Viewport.Height);
        var scrolls = along > 0 ? offset > 0 : offset < end;
        return !scrolls || (Direction * along > 0 && Extent < _stops[^1]);
    }

    private void Grab(IPointer pointer)
    {
        _dragging = true;
        pointer.Capture(this);
        foreach (var animation in Animator().Animations.Values) animation.Pause();
        Show(true);
        _grabbedAt = TransformMath.Cushion(Extent, _stops[0], _stops[^1], 1 / Friction);
        _velocity.Reset();
        _velocity.Add(Extent, Engine.Current.Now);
    }

    private void Lift()
    {
        // Cleared first: dropping the capture raises PointerCaptureLost, which lands back here.
        var pointer = _pointer!;
        _pointer = null;
        if (!_dragging) return;
        _dragging = false;
        if (ReferenceEquals(pointer.Captured, this)) pointer.Capture(null);
        var extent = Extent;
        var rest = Math.Clamp(extent < _stops[0] || extent > _stops[^1] ? extent : extent + _velocity.Velocity * s_inertia.TimeConstant, _stops[0], _stops[^1]);
        var nearest = 0;
        for (var i = 1; i < _stops.Length; i++)
        {
            if (Math.Abs(_stops[i] - rest) < Math.Abs(_stops[nearest] - rest)) nearest = i;
        }
        _settling = true;
        DetentIndex = nearest;
        _settling = false;
        MoveTo(nearest, _velocity.Velocity);
    }

    private int Index(int detent) => Math.Clamp(detent, 0, _stops.Length - 1);

    // Coasts to the detent; past the first or last detent the fling is left alone, so it crosses the bound and bounces.
    private void MoveTo(int detent, double velocity)
    {
        if (_stops.Length == 0 || _dragging) return;
        var (target, first, last, direction) = (_stops[Index(detent)], _stops[0], _stops[^1], Direction);
        var animate = Animator();
        if (target > 0) Show(true);
        if (Engine.Current.Defaults.ReducedMotion)
        {
            animate.Set(Axis, direction * target, 0);
            return;
        }
        animate.Fling(Axis, direction * velocity, s_inertia with
        {
            ModifyTarget = t => direction * t < first || direction * t > last ? t : direction * target,
            Min = Math.Min(direction * first, direction * last),
            Max = Math.Max(direction * first, direction * last),
        });
    }

    // Lands on the detent at once, as the host's size or the detents change.
    private void Place()
    {
        var target = _stops[Index(DetentIndex)];
        Show(target > 0);
        Animator().Set(Axis, Direction * target, 0);
    }

    private void Show(bool shown)
    {
        _panel.IsVisible = shown;
        _edge.IsVisible = !shown;
    }
}
