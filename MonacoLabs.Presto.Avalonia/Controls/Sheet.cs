using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
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
/// <para>
/// The sheet carries one of the pseudo-classes <c>:left</c>, <c>:top</c>, <c>:right</c> or <c>:bottom</c>, matching its
/// <see cref="Edge"/>, so its content's chrome can follow the edge, as in <c>presto|Sheet:bottom Border.grab</c>.
/// </para>
/// <para>
/// With <see cref="Presentation"/> set to <see cref="SheetPresentation.Anchored"/> and a visible <see cref="Anchor"/>, opening
/// the sheet moves its content into <see cref="Flyout"/> at the anchor instead; closing the flyout closes the sheet, and the
/// content returns once the flyout has closed. A style can switch <see cref="Edge"/>, <see cref="Detents"/>,
/// <see cref="EdgeSwipe"/> and <see cref="Presentation"/> across an app's breakpoints, and an open sheet stays open and lands
/// on its new edge or flyout at once.
/// </para>
/// </remarks>
public class Sheet : Control, IGestureMember
{
    private const double Friction = .35;

    /// <summary>The sheet's content, drawn at its edge; it gives the sheet its look.</summary>
    public static readonly StyledProperty<Control?> ContentProperty =
        AvaloniaProperty.Register<Sheet, Control?>(nameof(Content));

    /// <summary>The extents the sheet rests at, from closed to fully open. Defaults to <c>"0,Auto"</c>: hidden, or as deep as its content.</summary>
    public static readonly StyledProperty<SheetDetents> DetentsProperty =
        AvaloniaProperty.Register<Sheet, SheetDetents>(nameof(Detents), SheetDetents.Parse("0,Auto"));

    /// <summary>The index in <see cref="Detents"/> the sheet rests at or is heading for, clamped to them; setting it glides there.</summary>
    public static readonly StyledProperty<int> DetentIndexProperty =
        AvaloniaProperty.Register<Sheet, int>(nameof(DetentIndex), defaultBindingMode: BindingMode.TwoWay, coerce: CoerceDetentIndex);

    /// <summary>
    /// Whether the sheet rests above its first detent. Setting it true opens a closed sheet to its second detent, and false
    /// closes it to its first; on a sheet with a single detent it stays false.
    /// </summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Sheet, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The edge of the host the sheet slides out from. Defaults to <see cref="MonacoLabs.Presto.Edge.Bottom"/>.</summary>
    public static readonly StyledProperty<Edge> EdgeProperty =
        AvaloniaProperty.Register<Sheet, Edge>(nameof(Edge), Edge.Bottom);

    /// <summary>The scrim over the host, at full strength once the sheet reaches its last detent; null shows none and leaves a tap there to the host.</summary>
    public static readonly StyledProperty<IBrush?> ScrimProperty =
        AvaloniaProperty.Register<Sheet, IBrush?>(nameof(Scrim), Dismissal.Scrim);

    /// <summary>The time constant, in ms, of the inertia that settles the sheet on a detent; shorter settles sooner. Defaults to 350.</summary>
    public static readonly StyledProperty<double> TimeConstantProperty =
        AvaloniaProperty.Register<Sheet, double>(nameof(TimeConstant), 350);

    /// <summary>How deep a strip along the edge, in DIPs, takes presses to start a drag while the sheet is closed and hidden. Defaults to 0.</summary>
    public static readonly StyledProperty<double> EdgeSwipeProperty =
        AvaloniaProperty.Register<Sheet, double>(nameof(EdgeSwipe));

    /// <summary>The element the sheet opens at while <see cref="Presentation"/> is <see cref="SheetPresentation.Anchored"/>.</summary>
    public static readonly StyledProperty<Control?> AnchorProperty =
        AvaloniaProperty.Register<Sheet, Control?>(nameof(Anchor));

    /// <summary>The flyout an anchored sheet opens in, carrying its placement and presenter classes; null uses a plain <see cref="Avalonia.Controls.Flyout"/>.</summary>
    public static readonly StyledProperty<Flyout?> FlyoutProperty =
        AvaloniaProperty.Register<Sheet, Flyout?>(nameof(Flyout));

    /// <summary>Whether the sheet opens at its edge or in its flyout at <see cref="Anchor"/>. Defaults to <see cref="SheetPresentation.Edge"/>.</summary>
    public static readonly StyledProperty<SheetPresentation> PresentationProperty =
        AvaloniaProperty.Register<Sheet, SheetPresentation>(nameof(Presentation));

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
    private Flyout? _defaultFlyout;
    // The flyout holding the content while the sheet presents anchored, until it has closed.
    private Flyout? _held;
    private Control? _heldAt;
    private bool _hiding;

    static Sheet()
    {
        AffectsArrange<Sheet>(EdgeProperty, DetentsProperty, EdgeSwipeProperty);
        ClipToBoundsProperty.OverrideDefaultValue<Sheet>(true);
    }

    /// <summary>Creates a closed sheet.</summary>
    public Sheet()
    {
        _scrim.Bind(Border.BackgroundProperty, this.GetObservable(ScrimProperty));
        _scrim.Tapped += (_, _) => IsOpen = false;
        Dismissal.Attach(this, IsOpenProperty, () => _held is null);
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
        SetEdgeClasses();
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

    /// <inheritdoc cref="TimeConstantProperty"/>
    public double TimeConstant
    {
        get => GetValue(TimeConstantProperty);
        set => SetValue(TimeConstantProperty, value);
    }

    /// <inheritdoc cref="EdgeSwipeProperty"/>
    public double EdgeSwipe
    {
        get => GetValue(EdgeSwipeProperty);
        set => SetValue(EdgeSwipeProperty, value);
    }

    /// <inheritdoc cref="AnchorProperty"/>
    public Control? Anchor
    {
        get => GetValue(AnchorProperty);
        set => SetValue(AnchorProperty, value);
    }

    /// <inheritdoc cref="FlyoutProperty"/>
    public Flyout? Flyout
    {
        get => GetValue(FlyoutProperty);
        set => SetValue(FlyoutProperty, value);
    }

    /// <inheritdoc cref="PresentationProperty"/>
    public SheetPresentation Presentation
    {
        get => GetValue(PresentationProperty);
        set => SetValue(PresentationProperty, value);
    }

    /// <summary>How far the sheet stands out from its edge, in DIPs, as it renders; link content to it with <see cref="MotionValue.Map(Func{double, double})"/> or read its <see cref="MotionValue.Velocity"/>.</summary>
    public MotionValue Position => _position;

    Visual IGestureMember.Element => this;

    IInputElement IGestureMember.CaptureTarget => this;

    private bool Anchored => Presentation == SheetPresentation.Anchored && Anchor is { IsEffectivelyVisible: true } anchor && TopLevel.GetTopLevel(anchor) is not null;

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
            if (_held is not null || (IsOpen && Anchored)) Present();
            else if (!_settling) MoveTo(DetentIndex, 0);
        }
        else if (change.Property == IsOpenProperty)
        {
            if (IsOpen != DetentIndex > 0) DetentIndex = IsOpen ? 1 : 0;
            // A single detent coerces the index back to 0 without a change, so nothing else closes the sheet.
            if (IsOpen && DetentIndex == 0) IsOpen = false;
            _scrim.IsHitTestVisible = IsOpen && _held is null;
        }
        else if (change.Property == DetentsProperty)
        {
            if (_dragging) Drop();
            // Cleared so the coerced index does not glide and the next arrange lands on it at once.
            _stops = [];
            CoerceValue(DetentIndexProperty);
        }
        else if (change.Property == ContentProperty)
        {
            if (_held is not null) _held.Content = Content;
            else _panel.Child = Content;
        }
        else if (change.Property == PresentationProperty || change.Property == AnchorProperty)
        {
            Show(_panel.IsVisible);
            if (IsOpen) Present();
        }
        else if (change.Property == EdgeProperty)
        {
            if (_dragging) Drop();
            SetEdgeClasses();
            _animate?.Revert();
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
        _panel.Child?.Measure(availableSize);
        var content = _panel.Child?.DesiredSize ?? default;
        return new Size(
            double.IsInfinity(availableSize.Width) ? content.Width : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? content.Height : availableSize.Height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var (width, height) = (finalSize.Width, finalSize.Height);
        var length = Horizontal ? width : height;
        var content = Horizontal ? _panel.Child?.DesiredSize.Width ?? 0 : _panel.Child?.DesiredSize.Height ?? 0;
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
            if (!_dragging && _held is null) Place();
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
        var horizontal = Horizontal;
        GestureArena.Join(e, this, GestureGate.SameAxis(horizontal ? Orientation.Horizontal : Orientation.Vertical) with
        {
            Accepts = travel => TakesFromScroller(horizontal ? travel.X : travel.Y),
        });
    }

    private void HandleMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || e.Pointer != _pointer) return;
        var moved = e.GetPosition(null) - _pressedAt;
        var along = Horizontal ? moved.X : moved.Y;
        e.Handled = true;
        // Gesture recognizers see handled events too, and an inner scroll would steal the capture.
        e.PreventGestureRecognition();
        var extent = TransformMath.Cushion(_grabbedAt + Direction * along, _stops[0], _stops[^1], Friction);
        Animator().Set(Axis, Direction * extent, 0);
        _velocity.Add(extent, Engine.Current.Now);
    }

    private void HandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging || e.Pointer != _pointer) return;
        e.Handled = true;
        Lift();
    }

    private void HandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_dragging && e.Pointer == _pointer) Lift();
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

    void IGestureMember.Won()
    {
        _dragging = true;
        foreach (var animation in Animator().Animations.Values) animation.Pause();
        Show(true);
        _grabbedAt = TransformMath.Cushion(Extent, _stops[0], _stops[^1], 1 / Friction);
        _velocity.Reset();
        _velocity.Add(Extent, Engine.Current.Now);
    }

    void IGestureMember.Lost() => _pointer = null;

    private void Lift()
    {
        Drop();
        var extent = Extent;
        var rest = Math.Clamp(extent < _stops[0] || extent > _stops[^1] ? extent : extent + _velocity.Velocity * TimeConstant, _stops[0], _stops[^1]);
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

    // Cleared first: dropping the capture raises PointerCaptureLost, which would lift again.
    private void Drop()
    {
        var pointer = _pointer!;
        _pointer = null;
        _dragging = false;
        if (ReferenceEquals(pointer.Captured, this)) pointer.Capture(null);
    }

    private static int CoerceDetentIndex(AvaloniaObject sheet, int index) =>
        Math.Clamp(index, 0, Math.Max(0, ((Sheet)sheet).Detents.Count - 1));

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
        animate.Fling(Axis, direction * velocity, new Inertia()
        {
            TimeConstant = TimeConstant,
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

    // Moves an open sheet into its flyout, or out of it once the flyout has closed; Avalonia cannot show a flyout mid-close.
    private void Present()
    {
        var anchored = IsOpen && Anchored;
        if (_held is { } held)
        {
            if (!anchored && !_hiding)
            {
                _hiding = true;
                held.Hide();
            }
            return;
        }
        if (!anchored) return;
        if (DetentIndex > 1)
        {
            DetentIndex = 1;
            return;
        }
        if (_dragging) Drop();
        _animate?.Set(Axis, 0, 0);
        Show(false);
        _scrim.IsHitTestVisible = false;
        _panel.Child = null;
        _held = Flyout ?? (_defaultFlyout ??= new Flyout());
        _held.Content = Content;
        _held.Closed += HandleFlyoutClosed;
        // Avalonia leaves a flyout open at a hidden target, so a hidden anchor falls back to the edge here.
        _heldAt = Anchor!;
        Animate.AddVisibilityHandler(_heldAt, HandleAnchorVisibility);
        _held.ShowAt(_heldAt);
    }

    private void HandleFlyoutClosed(object? sender, EventArgs e)
    {
        var (held, hiding) = (_held!, _hiding);
        held.Closed -= HandleFlyoutClosed;
        Animate.RemoveVisibilityHandler(_heldAt!, HandleAnchorVisibility);
        _heldAt = null;
        held.Content = null;
        _held = null;
        _hiding = false;
        _panel.Child = Content;
        if (!hiding) IsOpen = false;
        _scrim.IsHitTestVisible = IsOpen;
        if (!IsOpen) return;
        if (Anchored)
        {
            Present();
            return;
        }
        // Cleared so the next arrange lands the panel open at once.
        _stops = [];
        InvalidateMeasure();
    }

    private void HandleAnchorVisibility(object? sender, EventArgs e) => Present();

    private void SetEdgeClasses()
    {
        PseudoClasses.Set(":left", Edge == Edge.Left);
        PseudoClasses.Set(":top", Edge == Edge.Top);
        PseudoClasses.Set(":right", Edge == Edge.Right);
        PseudoClasses.Set(":bottom", Edge == Edge.Bottom);
    }

    private void Show(bool shown)
    {
        _panel.IsVisible = shown;
        _edge.IsVisible = !shown && !Anchored;
    }
}
