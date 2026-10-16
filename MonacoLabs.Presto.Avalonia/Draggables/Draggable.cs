using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Gestures;
using MonacoLabs.Presto.Transforms;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto.Draggables;

/// <summary>
/// Pointer dragging of a control, written through an <see cref="Animatable"/>
/// over the two mapped axis properties, with per-axis enabling and modifiers, a drag threshold, snapping, container
/// bounds and container friction, and a release that throws the target on with the pointer's velocity.
/// </summary>
/// <remarks>One pointer drags at a time, mouse and touch alike, through a pointer capture on the trigger.</remarks>
public sealed class Draggable : IRevertible
{
    internal const double DefaultScrollSpeed = 1.5;
    internal const double DefaultScrollThreshold = 20;
    private const double ResizeDebounce = 150;
    private const double ScrollInViewDuration = 350;
    private const double ReleaseDistance = 150;

    private static readonly Cursor s_grab = new(StandardCursorType.Hand);
    private static readonly Cursor s_grabbing = new(StandardCursorType.SizeAll);

    // Pointer events arrive long after construction, when the ambient engine may be another one.
    private readonly Engine _engine;
    private readonly Control _target;
    private readonly Control _trigger;
    private readonly DraggableParams _parameters;
    private readonly Timer _updateTicker;
    private readonly Timer _overshootTicker;
    private readonly Timer _resizeTicker;
    private readonly Coords _overshoot = new();
    private readonly Coords _scroll = new();
    private readonly Spring _releaseSpringX;
    private readonly Spring _releaseSpringY;
    private readonly Ease _releaseEase;
    private readonly bool _hasReleaseSpring;
    private readonly VelocityTracker _velocityTracker = new();
    private readonly string _xProp;
    private readonly string _yProp;
    private readonly bool _xIsLayout;
    private readonly bool _yIsLayout;
    private readonly bool _disabledX;
    private readonly bool _disabledY;

    private Thickness _padding;
    private Func<double, double>? _snapX;
    private Func<double, double>? _snapY;
    private ScrollViewer? _scroller;
    private Animation? _overshootOut;
    private Animation? _overshootBack;
    private Animation? _scrolling;
    private Cursor? _hoverCursor;
    private Cursor? _grabCursor;
    private Cursor? _originalCursor;
    private bool _cursorTaken;
    private double _dragThreshold = GestureGate.DefaultDragThreshold;
    private double _containerFriction;
    private double _releaseContainerFriction;
    private double _minVelocity;
    private double _maxVelocity;
    private double _velocityMultiplier;
    private double _scrollSpeed;
    private double _scrollThreshold;
    private double _boundsTop = -Constants.MaxValue;
    private double _boundsRight = Constants.MaxValue;
    private double _boundsBottom = Constants.MaxValue;
    private double _boundsLeft = -Constants.MaxValue;
    private double _coordX;
    private double _coordY;
    private double _prevX;
    private double _prevY;
    private double _pointerX;
    private double _pointerY;
    private double _pointerPrevX;
    private double _pointerPrevY;
    private double _appliedX;
    private double _appliedY;
    private double _grabX;
    private double _grabY;
    private double _grabValueX;
    private double _grabValueY;
    private TopLevel? _escapeRoot;
    private double _snappedX;
    private double _snappedY;
    private double _destX;
    private double _destY;
    private IPointer? _pointer;
    private bool _manual;
    private bool _enabled;
    private bool _grabbed;
    private bool _dragged;
    private bool _updated;
    private bool _released;
    private bool _scrollLocked;
    private bool _hovering;
    private MotionValue? _xValue;
    private MotionValue? _yValue;
    private LayoutTransition? _flight;
    private LayoutTransition? _displacedLayout;

    internal Draggable(Control target, DraggableParams parameters)
    {
        GestureOwners.Claim(target, this);
        _engine = Engine.Current;
        _engine.CurrentScope?.Register(this);
        _target = target;
        _trigger = parameters.Trigger ?? target;
        _parameters = parameters;

        var x = parameters.X;
        var y = parameters.Y;
        _xProp = x?.MapTo ?? "X";
        _yProp = y?.MapTo ?? "Y";
        _xIsLayout = TransformShorthands.Parse(_xProp) is null;
        _yIsLayout = TransformShorthands.Parse(_yProp) is null;
        _disabledX = x is { Enabled: false };
        _disabledY = y is { Enabled: false };

        var suppliedSpring = parameters.ReleaseEase?.Raw as Spring;
        _hasReleaseSpring = suppliedSpring != null;
        _releaseSpringX = suppliedSpring ?? NewReleaseSpring(parameters);
        _releaseSpringY = suppliedSpring ?? NewReleaseSpring(parameters);
        _releaseEase = parameters.ReleaseEase is { } releaseEase ? releaseEase.Resolve(target, 0, 1, out _) : Eases.OutQuint;
        _velocityTracker.AddDisplacement(0, _engine.Now);

        using var origin = PrestoDiagnostics.Origin("Draggable");
        Animate = target.CreateAnimatable(new AvaloniaAnimatableParams
        {
            [_xProp] = new AnimatableProperty { Duration = 0, Modifier = x?.Modifier, Composition = x?.Composition },
            [_yProp] = new AnimatableProperty { Duration = 0, Modifier = y?.Modifier, Composition = y?.Composition },
            Modifier = parameters.Modifier,
            OnUpdate = HandleRender,
            OnComplete = HandleSettle,
        });

        _updateTicker = Presto.CreateTimer(new TimerParams { Autoplay = false, OnUpdate = _ => Update() });
        _overshootTicker = Presto.CreateTimer(new TimerParams
        {
            Autoplay = false,
            // Stretched to the release duration on every release; an infinite duration would not scale.
            Duration = 1,
            OnUpdate = _ =>
            {
                _updated = true;
                _manual = true;
                // Duration 1 rather than 0 so the animatable does not complete mid-throw and report a settle.
                if (!_disabledX) Animate.Set(_xProp, _overshoot.X, 1);
                if (!_disabledY) Animate.Set(_yProp, _overshoot.Y, 1);
            },
            OnComplete = _ =>
            {
                _manual = false;
                SetAxes(_overshoot.X, 0, null, _overshoot.Y, 0, null);
            },
        });
        _resizeTicker = Presto.CreateTimer(new TimerParams
        {
            Autoplay = false,
            Duration = ResizeDebounce * Globals.TimeScale,
            OnComplete = _ =>
            {
                _parameters.OnResize?.Invoke(this);
                Refresh();
                _parameters.OnAfterResize?.Invoke(this);
            },
        });

        Enable();
        Refresh();
        _target.SizeChanged += HandleSizeChanged;
        if (parameters.Container is { } container && container != _target) container.SizeChanged += HandleSizeChanged;
    }

    /// <summary>The animatable the axes are written through, so a drag can be scripted: <c>Animate.Set("X", 100, 250)</c>.</summary>
    public Animatable Animate { get; }

    /// <summary>The target's position along that axis, in the unit of the property the axis maps to.</summary>
    public double X
    {
        get => Utils.Round(Animate.Get(_xProp), _engine.Precision);
        set => SetX(value, false);
    }

    /// <inheritdoc cref="X"/>
    public double Y
    {
        get => Utils.Round(Animate.Get(_yProp), _engine.Precision);
        set => SetY(value, false);
    }

    /// <summary>A <see cref="MotionValue"/> mirroring <see cref="X"/>, updated whenever the axis renders and frozen once the draggable reverts.</summary>
    public MotionValue XValue => _xValue ??= Animate.ValueOf(_xProp).Map(v => Utils.Round(v, _engine.Precision));

    /// <inheritdoc cref="XValue"/>
    public MotionValue YValue => _yValue ??= Animate.ValueOf(_yProp).Map(v => Utils.Round(v, _engine.Precision));

    /// <summary>Where the target sits between the container bounds on that axis, 0 at the start edge and 1 at the end.</summary>
    public double ProgressX
    {
        get => Utils.MapRange(X, _boundsLeft, _boundsRight, 0, 1);
        set => SetX(Utils.MapRange(value, 0, 1, _boundsLeft, _boundsRight), false);
    }

    /// <inheritdoc cref="ProgressX"/>
    public double ProgressY
    {
        get => Utils.MapRange(Y, _boundsTop, _boundsBottom, 0, 1);
        set => SetY(Utils.MapRange(value, 0, 1, _boundsTop, _boundsBottom), false);
    }

    /// <summary>Pointer speed over the last three sampled frames, in units per millisecond; the release throw is scaled by it.</summary>
    public double Velocity { get; private set; }

    /// <summary>Direction of the last pointer move, in radians.</summary>
    public double Angle { get; private set; }

    /// <summary>The <see cref="DraggableParams.DropGroup"/> zone the target is over, tracked through the drag and the release throw.</summary>
    public Control? Zone { get; private set; }

    /// <summary>Stops the drag and every animation running on the target's axes, leaving it where it is.</summary>
    /// <remarks>
    /// Only the draggable's own release, overshoot and scroll animations are canceled; animations on other targets keep running.
    /// <see cref="Zone"/> is cleared without a drop or a miss.
    /// </remarks>
    public Draggable Stop()
    {
        Halt();
        _hovering = false;
        MoveZone(null);
        return this;
    }

    /// <summary>Stops the drag and puts both axes back to 0, unclamped even when the container's bounds exclude it.</summary>
    public Draggable Reset()
    {
        Stop();
        _resizeTicker.Pause();
        _grabbed = _dragged = _updated = _released = false;
        StopListeningForEscape();
        SetX(0, true);
        SetY(0, true);
        _coordX = _coordY = 0;
        _pointerX = _pointerY = _pointerPrevX = _pointerPrevY = _appliedX = _appliedY = _grabX = _grabY = 0;
        ClearVelocity();
        return this;
    }

    /// <summary>
    /// Moves the target into <paramref name="panel"/> at <paramref name="index"/> (appended when null), resets its axes to 0 and
    /// flies it into its new slot from where it is drawn now, position only, over the release's settling duration and ease.
    /// </summary>
    /// <remarks>Meant for <see cref="DraggableParams.OnDrop"/>. An <c>AnimateLayout</c> on the target is armed again once the flight lands.</remarks>
    /// <exception cref="InvalidOperationException">The target's parent is not a <see cref="Panel"/>.</exception>
    public Draggable Reparent(Panel panel, int? index = null)
    {
        if (_target.Parent is not Panel parent) throw new InvalidOperationException("Reparent needs the target's parent to be a Panel.");
        // Taken before the flight replaces it; a XAML Animate.Layout (Replaced set) re-arms itself.
        if (LayoutTransition.Live(_target) is { Once: false, Replaced: null } layout) _displacedLayout = layout;
        _releaseSpringX.Velocity = 0;
        var flight = _flight = PrestoAvalonia.AnimateSharedElement(
            _target, _target, SettlingDuration(_releaseSpringX), _hasReleaseSpring ? _releaseSpringX.Ease : _releaseEase, LayoutAnimateMode.Position);
        parent.Children.Remove(_target);
        panel.Children.Insert(index ?? panel.Children.Count, _target);
        Reset();
        if (_displacedLayout is not null) RearmLayoutAfter(flight);
        return this;
    }

    /// <summary>Reads the parameters that can change — container, padding, snapping, threshold, friction, velocity, cursors — and measures the bounds again.</summary>
    /// <remarks>
    /// A release still in flight is re-aimed at its destination clamped to the new bounds, and settles there; an
    /// <see cref="DraggableParams.ReleaseInertia"/> release is resolved again from its current value and velocity instead.
    /// </remarks>
    public Draggable Refresh()
    {
        var currentX = X;
        var currentY = Y;
        _padding = _parameters.ContainerPadding ?? default;
        _snapX = _parameters.X?.Snap ?? _parameters.Snap;
        _snapY = _parameters.Y?.Snap ?? _parameters.Snap;
        _dragThreshold = _parameters.DragThreshold ?? GestureGate.DefaultDragThreshold;
        _containerFriction = Utils.Clamp(_parameters.ContainerFriction ?? .8, 0, 1);
        _releaseContainerFriction = Utils.Clamp(_parameters.ReleaseContainerFriction ?? _containerFriction, 0, 1);
        _minVelocity = _parameters.MinVelocity ?? 0;
        _maxVelocity = _parameters.MaxVelocity ?? 50;
        _velocityMultiplier = _parameters.VelocityMultiplier ?? 1;
        _scrollSpeed = _parameters.ScrollSpeed ?? DefaultScrollSpeed;
        _scrollThreshold = _parameters.ScrollThreshold ?? DefaultScrollThreshold;
        var cursor = _parameters.Cursor;
        _hoverCursor = cursor is { Enabled: false } ? null : cursor?.OnHover ?? s_grab;
        _grabCursor = cursor is { Enabled: false } ? null : cursor?.OnGrab ?? s_grabbing;
        UpdateBoundingValues();
        if (!_grabbed && ReleaseRemaining() is { } remaining)
        {
            RetargetRelease(remaining);
            return this;
        }
        SetX(Utils.Clamp(currentX, _boundsLeft, _boundsRight), true);
        SetY(Utils.Clamp(currentY, _boundsTop, _boundsBottom), true);
        return this;
    }

    /// <summary>Scrolls the nearest <see cref="ScrollViewer"/> until the target's destination is <paramref name="gap"/> inside its viewport.</summary>
    public Draggable ScrollInView(double? duration = null, double gap = 0, EaseValue? ease = null)
    {
        if (_scroller is not { } scroller || OutOfView(gap, _destX - X, _destY - Y) is not { } overflow) return this;
        if (overflow.X == 0 && overflow.Y == 0) return this;
        var offset = scroller.Offset;
        _scroll.X = offset.X;
        _scroll.Y = offset.Y;
        _scrollLocked = true;
        using var origin = PrestoDiagnostics.Origin("Draggable");
        _scrolling = Presto.Animate(_scroll, new AnimationParams
        {
            ["X"] = ClampScroll(offset.X + overflow.X, scroller.Extent.Width, scroller.Viewport.Width),
            ["Y"] = ClampScroll(offset.Y + overflow.Y, scroller.Extent.Height, scroller.Viewport.Height),
            Duration = duration ?? ScrollInViewDuration * Globals.TimeScale,
            Ease = ease ?? (EaseValue)Eases.InOutQuad,
            OnUpdate = _ => scroller.Offset = new Vector(_scroll.X, _scroll.Y),
            OnComplete = _ => _scrollLocked = false,
        });
        return this;
    }

    /// <summary>Moves the target itself until it is <paramref name="gap"/> inside the nearest <see cref="ScrollViewer"/>'s viewport, snapped and clamped like a release.</summary>
    public Draggable AnimateInView(double? duration = null, double gap = 0, EaseValue? ease = null)
    {
        Stop();
        UpdateBoundingValues();
        if (OutOfView(gap, 0, 0) is not { } overflow) return this;
        var easeValue = ease ?? (EaseValue)Eases.InOutQuad;
        var length = duration ?? ScrollInViewDuration * Globals.TimeScale;
        SetAxes(
            overflow.X != 0 ? Snapped(X - overflow.X, _snapX, _boundsLeft, _boundsRight) : null, length, easeValue,
            overflow.Y != 0 ? Snapped(Y - overflow.Y, _snapY, _boundsTop, _boundsBottom) : null, length, easeValue);
        return this;
    }

    /// <summary>Starts listening for pointers on the trigger.</summary>
    public Draggable Enable()
    {
        if (_enabled) return this;
        _enabled = true;
        _trigger.PointerEntered += HandleEntered;
        _trigger.PointerExited += HandleExited;
        _trigger.PointerPressed += HandlePressed;
        _trigger.PointerMoved += HandleMoved;
        _trigger.PointerReleased += HandleReleased;
        _trigger.PointerCaptureLost += HandleCaptureLost;
        return this;
    }

    /// <summary>Stops listening for pointers and drops the drag in progress, leaving the target where it is.</summary>
    public Draggable Disable()
    {
        _enabled = false;
        _grabbed = _dragged = _updated = _released = _hovering = false;
        MoveZone(null);
        ReleasePointer();
        RestoreCursor();
        _trigger.PointerEntered -= HandleEntered;
        _trigger.PointerExited -= HandleExited;
        _trigger.PointerPressed -= HandlePressed;
        _trigger.PointerMoved -= HandleMoved;
        _trigger.PointerReleased -= HandleReleased;
        _trigger.PointerCaptureLost -= HandleCaptureLost;
        return this;
    }

    /// <summary>Undoes everything the draggable did: the axes and the cursor go back to what they were before it existed and every handler is detached.</summary>
    public Draggable Revert()
    {
        Reset();
        Disable();
        _updateTicker.Revert();
        _overshootTicker.Revert();
        _resizeTicker.Revert();
        Animate.Revert();
        _flight?.Revert();
        _target.SizeChanged -= HandleSizeChanged;
        if (_parameters.Container is { } container && container != _target) container.SizeChanged -= HandleSizeChanged;
        GestureOwners.Release(_target, this);
        return this;
    }

    void IRevertible.Revert() => Revert();

    private void Halt()
    {
        _updateTicker.Pause();
        _overshootTicker.Pause();
        foreach (var animation in Animate.Animations.Values) animation.Pause();
        Presto.Remove(this);
        // Canceled rather than removed: unlinking the engine's next child mid-frame drops every child after it.
        _overshootOut?.Cancel();
        _overshootBack?.Cancel();
        _scrolling?.Cancel();
        _overshootOut = _overshootBack = _scrolling = null;
    }

    private static Spring NewReleaseSpring(DraggableParams parameters) =>
        new(parameters.ReleaseMass ?? 1, parameters.ReleaseStiffness ?? 80, parameters.ReleaseDamping ?? 20);

    private static Point? LayoutOffset(Visual from, Visual to)
    {
        double x = 0, y = 0;
        for (Visual? visual = from; visual != null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, to)) return new Point(x, y);
            x += visual.Bounds.X;
            y += visual.Bounds.Y;
        }
        return null;
    }

    private static double Snapped(double value, Func<double, double>? snap, double min, double max) =>
        Utils.Clamp(Utils.Round(snap?.Invoke(value) ?? value, 5), min, max);

    internal static double ClampScroll(double value, double extent, double viewport) =>
        Utils.Clamp(value, 0, Math.Max(0, extent - viewport));

    /// <summary>How far one frame scrolls <paramref name="viewport"/> for a pointer at <paramref name="point"/>, which ramps up across the edge bands.</summary>
    internal static Vector EdgeScroll(Point point, Size viewport, double speed, double threshold) => new Vector(
        Utils.Clamp((point.X - threshold) / threshold, -1, 0) + Utils.Clamp((point.X - (viewport.Width - threshold)) / threshold, 0, 1),
        Utils.Clamp((point.Y - threshold) / threshold, -1, 0) + Utils.Clamp((point.Y - (viewport.Height - threshold)) / threshold, 0, 1)) * (speed * 10);

    private void SetX(double x, bool muteUpdateCallback)
    {
        if (_disabledX) return;
        var value = Utils.Round(x, 5);
        _overshootTicker.Pause();
        // A zero duration write completes inside Set, so _manual keeps that completion from reading as a settle.
        _manual = true;
        _updated = !muteUpdateCallback;
        _destX = value;
        _snappedX = _snapX?.Invoke(value) ?? value;
        Animate.Set(_xProp, value, 0);
        _manual = false;
    }

    private void SetY(double y, bool muteUpdateCallback)
    {
        if (_disabledY) return;
        var value = Utils.Round(y, 5);
        _overshootTicker.Pause();
        _manual = true;
        _updated = !muteUpdateCallback;
        _destY = value;
        _snappedY = _snapY?.Invoke(value) ?? value;
        Animate.Set(_yProp, value, 0);
        _manual = false;
    }

    private void UpdateBoundingValues()
    {
        _scroller = _target.FindAncestorOfType<ScrollViewer>();
        if (_parameters.ContainerBounds is { } bounds)
        {
            var rect = bounds(this);
            _boundsTop = rect.Top + _padding.Top;
            _boundsRight = rect.Right - _padding.Right;
            _boundsBottom = rect.Bottom - _padding.Bottom;
            _boundsLeft = rect.Left + _padding.Left;
            return;
        }
        // Keeps the last measurement rather than throwing when the container has left the tree mid-drag.
        if (_parameters.Container is not { } container || LayoutOffset(_target, container) is not { } offset) return;
        // Bounds are the target at axis value 0; a layout property moves the target's own bounds, a render transform does not.
        var originX = offset.X - (_xIsLayout ? X : 0);
        var originY = offset.Y - (_yIsLayout ? Y : 0);
        _boundsTop = _padding.Top - originY;
        _boundsRight = container.Bounds.Width - _padding.Right - _target.Bounds.Width - originX;
        _boundsBottom = container.Bounds.Height - _padding.Bottom - _target.Bounds.Height - originY;
        _boundsLeft = _padding.Left - originX;
    }

    private Vector? OutOfView(double gap, double dx, double dy)
    {
        if (_scroller is not { } scroller || _target.TranslatePoint(default, scroller) is not { } origin) return null;
        var left = origin.X + dx - gap;
        var top = origin.Y + dy - gap;
        var right = origin.X + dx + _target.Bounds.Width + gap - scroller.Viewport.Width;
        var bottom = origin.Y + dy + _target.Bounds.Height + gap - scroller.Viewport.Height;
        return new Vector(left < 0 ? left : Math.Max(right, 0), top < 0 ? top : Math.Max(bottom, 0));
    }

    private double ComputeVelocity(double dx, double dy)
    {
        if (_velocityTracker.AddDisplacement(Math.Sqrt(dx * dx + dy * dy), _engine.Now, _velocityMultiplier, _minVelocity, _maxVelocity))
            Velocity = _velocityTracker.Velocity;
        return Velocity;
    }

    private void ClearVelocity()
    {
        _velocityTracker.Reset();
        Velocity = 0;
        Angle = 0;
    }

    private void AutoScroll()
    {
        if (_scrollLocked || _scrollSpeed == 0 || _scrollThreshold == 0) return;
        if (_scroller is not { } scroller) return;
        if (TopLevel.GetTopLevel(scroller) is not { } root || root.TranslatePoint(new Point(_pointerX, _pointerY), scroller) is not { } point) return;
        var viewport = scroller.Viewport;
        var delta = EdgeScroll(point, viewport, _scrollSpeed, _scrollThreshold);
        if (delta.X == 0 && delta.Y == 0) return;
        var offset = scroller.Offset;
        var x = _disabledX ? offset.X : ClampScroll(offset.X + delta.X, scroller.Extent.Width, viewport.Width);
        var y = _disabledY ? offset.Y : ClampScroll(offset.Y + delta.Y, scroller.Extent.Height, viewport.Height);
        // The content slides under the pointer, so the target follows it to stay where the pointer left it.
        _coordX += x - offset.X;
        _coordY += y - offset.Y;
        scroller.Offset = new Vector(x, y);
    }

    private void Update()
    {
        AutoScroll();
        var snappedX = _snappedX;
        var snappedY = _snappedY;
        var movedX = _pointerX - _appliedX;
        var movedY = _pointerY - _appliedY;
        _coordX += movedX;
        _coordY += movedY;
        _appliedX = _pointerX;
        _appliedY = _pointerY;
        var friction = 1 - _containerFriction;
        SetX(TransformMath.Cushion(_coordX, _boundsLeft, _boundsRight, friction), false);
        SetY(TransformMath.Cushion(_coordY, _boundsTop, _boundsBottom, friction), false);
        ComputeVelocity(movedX, movedY);
        Angle = Math.Atan2(_pointerY - _pointerPrevY, _pointerX - _pointerPrevX);
        if ((_snapX != null && _snappedX != snappedX) || (_snapY != null && _snappedY != snappedY)) _parameters.OnSnap?.Invoke(this);
        HoverZones();
    }

    private void HoverZones()
    {
        if (!_hovering || _parameters.DropGroup is not { } group || TopLevel.GetTopLevel(_target) is not { } root) return;
        var zone = FindZone(group, root);
        if (zone == Zone) return;
        MoveZone(zone);
        _parameters.OnZoneChange?.Invoke(this);
    }

    // Subscribed only while a zone is held, so the static event never keeps an idle draggable alive.
    private void MoveZone(Control? zone)
    {
        if (Zone is { } left)
        {
            ((IPseudoClasses)left.Classes).Set(":dragover", false);
            DropZones.Left -= HandleZoneLeft;
        }
        if (zone is not null)
        {
            ((IPseudoClasses)zone.Classes).Set(":dragover", true);
            DropZones.Left += HandleZoneLeft;
        }
        Zone = zone;
    }

    private void HandleZoneLeft(Control zone)
    {
        if (zone != Zone) return;
        MoveZone(null);
        _parameters.OnZoneChange?.Invoke(this);
    }

    // Zones are walked in registration order, so on a tie the one registered last wins.
    private Control? FindZone(string group, TopLevel root)
    {
        if (DropZones.Of(root) is not { } zones || _target.TransformToVisual(root) is not { } targetToRoot) return null;
        var drawn = new Rect(_target.Bounds.Size).TransformToAABB(targetToRoot);
        var hit = _parameters.DropHit;
        var point = hit == DropHit.Pointer ? new Point(_pointerX, _pointerY) : drawn.Center;
        Control? found = null;
        var largest = 0d;
        for (var i = 0; i < zones.Count; i++)
        {
            var zone = zones[i];
            if (!DropZones.IsUsable(zone, group) || zone == _target || _target.IsVisualAncestorOf(zone)) continue;
            if (zone.TransformToVisual(root) is not { } zoneToRoot) continue;
            var bounds = new Rect(zone.Bounds.Size).TransformToAABB(zoneToRoot);
            if (hit == DropHit.Overlap)
            {
                var overlap = bounds.Intersect(drawn);
                var area = overlap.Width * overlap.Height;
                if (area <= 0 || area < largest || !Accepts(zone)) continue;
                largest = area;
                found = zone;
            }
            else if (bounds.Contains(point) && Accepts(zone))
            {
                found = zone;
            }
        }
        return found;
    }

    private bool Accepts(Control zone) => _parameters.CanDrop?.Invoke(this, zone) ?? true;

    private void HandleRender(Animatable _)
    {
        var hasReleased = _released && !(_grabbed && _updated);
        var x = X;
        var y = Y;
        var dx = x - _prevX;
        var dy = y - _prevY;
        _prevX = x;
        _prevY = y;
        if (_updated && (dx != 0 || dy != 0)) _parameters.OnUpdate?.Invoke(this);
        if (!hasReleased)
        {
            _updated = false;
        }
        else
        {
            ComputeVelocity(dx, dy);
            Angle = Math.Atan2(dy, dx);
        }
        if (!_grabbed && _parameters.DropHit != DropHit.Pointer) HoverZones();
    }

    private void HandleSettle(Animatable _)
    {
        // Cleared before the callback so a drag started from inside it is not taken for the tail of this one.
        if (!_grabbed && _released) _released = false;
        if (_manual) return;
        // A grab pauses the release into a settle here; its drop waits for the next release.
        var dropping = _hovering && !_grabbed;
        // Taken before OnSettle, so a Reset there does not swallow the drop.
        var zone = Zone;
        if (dropping) _hovering = false;
        ClearVelocity();
        _parameters.OnSettle?.Invoke(this);
        if (!dropping) return;
        if (zone is null)
        {
            if (_parameters.DropMiss == DropMiss.ReturnHome && (X != _grabValueX || Y != _grabValueY)) ReturnToGrab();
            _parameters.OnDropMiss?.Invoke(this);
            return;
        }
        Zone = zone;
        _parameters.OnDrop?.Invoke(this);
        MoveZone(null);
    }

    private void HandleSizeChanged(object? sender, SizeChangedEventArgs e) => _resizeTicker.Restart();

    private void HandleEntered(object? sender, PointerEventArgs e)
    {
        if (!_grabbed) TakeCursor(_hoverCursor);
    }

    private void HandleExited(object? sender, PointerEventArgs e)
    {
        if (!_grabbed) RestoreCursor();
    }

    private void HandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (_grabbed) return;
        e.Handled = true;
        _pointer = e.Pointer;
        e.Pointer.Capture(_trigger);
        _grabbed = true;
        _released = false;
        // Not Stop: a throw grabbed again keeps its zone, and the drop resolves when the new release settles.
        Halt();
        UpdateBoundingValues();
        var position = e.GetPosition(null);
        var friction = 1 - _containerFriction;
        var x = _grabValueX = X;
        var y = _grabValueY = Y;
        // The raw coordinate the cushioned position came from, so the next drag picks up where the friction left off.
        _coordX = _prevX = friction == 0 ? x : TransformMath.Cushion(x, _boundsLeft, _boundsRight, 1 / friction);
        _coordY = _prevY = friction == 0 ? y : TransformMath.Cushion(y, _boundsTop, _boundsBottom, 1 / friction);
        _pointerX = _pointerPrevX = _appliedX = _grabX = position.X;
        _pointerY = _pointerPrevY = _appliedY = _grabY = position.Y;
        ClearVelocity();
        TakeCursor(_grabCursor);
        ScrollInView(100, 0, Eases.Out(3));
        if (_parameters.CancelOnEscape && TopLevel.GetTopLevel(_trigger) is { } root)
        {
            _escapeRoot = root;
            root.AddHandler(InputElement.KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        }
        _parameters.OnGrab?.Invoke(this);
    }

    private void HandleMoved(object? sender, PointerEventArgs e)
    {
        if (!_grabbed || e.Pointer != _pointer) return;
        var position = e.GetPosition(null);
        var passed = _dragged
            || (!_disabledX && Math.Abs(position.X - _grabX) > _dragThreshold)
            || (!_disabledY && Math.Abs(position.Y - _grabY) > _dragThreshold);
        if (!passed) return;
        _updateTicker.Resume();
        _pointerPrevX = _pointerX;
        _pointerPrevY = _pointerY;
        _pointerX = position.X;
        _pointerY = position.Y;
        _dragged = true;
        _hovering = true;
        _released = false;
        _parameters.OnDrag?.Invoke(this);
    }

    private void HandleReleased(object? sender, PointerReleasedEventArgs e) => Release();

    private void HandleCaptureLost(object? sender, PointerCaptureLostEventArgs e) => Release();

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !_grabbed) return;
        e.Handled = true;
        Release(cancel: true);
    }

    private void Release(bool cancel = false)
    {
        if (!_grabbed) return;
        // Cleared first: dropping the capture raises PointerCaptureLost, which lands back here.
        _grabbed = false;
        _dragged = false;
        _updateTicker.Pause();
        ReleasePointer();
        TakeCursor(_hoverCursor);

        if (cancel)
        {
            var missed = _hovering;
            // Cleared before the return, which settles at once when the target is already home.
            _hovering = false;
            MoveZone(null);
            ReturnToGrab();
            _parameters.OnRelease?.Invoke(this);
            if (missed) _parameters.OnDropMiss?.Invoke(this);
            return;
        }

        var velocity = ComputeVelocity(_pointerX - _appliedX, _pointerY - _appliedY);
        var angle = Angle = Math.Atan2(_pointerY - _pointerPrevY, _pointerX - _pointerPrevX);
        var longest = _parameters.ReleaseInertia is { } inertia
            ? FlingRelease(inertia, velocity * Math.Cos(angle), velocity * Math.Sin(angle))
            : Throw(velocity, angle);

        ScrollInView(longest, _scrollThreshold, _releaseEase);

        var snapped = (_snapX != null && _destX != _snappedX) || (_snapY != null && _destY != _snappedY);
        _snappedX = _destX;
        _snappedY = _destY;
        if (snapped) _parameters.OnSnap?.Invoke(this);

        _updated = true;
        _released = true;
        _parameters.OnRelease?.Invoke(this);
    }

    // The upstream release: projected along the throw, cushioned past the bounds and snapped. Returns the longest axis duration.
    private double Throw(double velocity, double angle)
    {
        var x = X;
        var y = Y;
        var thrown = velocity * ReleaseDistance;
        var friction = 1 - _releaseContainerFriction;
        var thrownX = x + Math.Cos(angle) * thrown;
        var thrownY = y + Math.Sin(angle) * thrown;
        var cushionedX = TransformMath.Cushion(thrownX, _boundsLeft, _boundsRight, friction);
        var cushionedY = TransformMath.Cushion(thrownY, _boundsTop, _boundsBottom, friction);
        var destX = _destX = Snapped(cushionedX, _snapX, _boundsLeft, _boundsRight);
        var destY = _destY = Snapped(cushionedY, _snapY, _boundsTop, _boundsBottom);
        var outOfBounds = (!_disabledX && (thrownX < _boundsLeft || thrownX > _boundsRight))
            || (!_disabledY && (thrownY < _boundsTop || thrownY > _boundsBottom));

        double durationX = 0, durationY = 0, longest = 0;
        Ease easeX = _releaseEase, easeY = _releaseEase;
        _overshoot.X = x;
        _overshoot.Y = y;

        if (!_disabledX)
        {
            durationX = ReleaseDuration(_releaseSpringX, x, destX, _boundsLeft, _boundsRight, thrown, velocity, _disabledY);
            if (_hasReleaseSpring) easeX = _releaseSpringX.Ease;
            longest = Math.Max(longest, durationX);
        }

        if (!_disabledY)
        {
            durationY = ReleaseDuration(_releaseSpringY, y, destY, _boundsTop, _boundsBottom, thrown, velocity, _disabledX);
            if (_hasReleaseSpring) easeY = _releaseSpringY.Ease;
            longest = Math.Max(longest, durationY);
        }

        if (!_hasReleaseSpring && outOfBounds && friction != 0 && (durationX != 0 || durationY != 0))
        {
            // Two additive tweens: out to where the throw would have landed, then back to the clamped destination.
            using var origin = PrestoDiagnostics.Origin("Draggable");
            _overshootOut = Presto.Animate(_overshoot, new AnimationParams
            {
                ["X"] = new TweenParams { To = cushionedX, Duration = durationX * .65 },
                ["Y"] = new TweenParams { To = cushionedY, Duration = durationY * .65 },
                Ease = _releaseEase,
                Composition = Composition.Additive,
            });
            _overshootBack = Presto.Animate(_overshoot, new AnimationParams
            {
                ["X"] = new TweenParams { To = destX, Duration = durationX },
                ["Y"] = new TweenParams { To = destY, Duration = durationY },
                Ease = _releaseEase,
                Composition = Composition.Additive,
            });
            _overshootTicker.Stretch(Math.Max(durationX, durationY)).Restart();
        }
        else
        {
            SetAxes(destX, durationX, easeX, destY, durationY, easeY);
        }
        return longest;
    }

    private void ReturnToGrab()
    {
        _updated = true;
        _released = true;
        _destX = _snappedX = _grabValueX;
        _destY = _snappedY = _grabValueY;
        SetAxes(
            _grabValueX, ReturnDuration(_releaseSpringX, X, _grabValueX), _hasReleaseSpring ? _releaseSpringX.Ease : _releaseEase,
            _grabValueY, ReturnDuration(_releaseSpringY, Y, _grabValueY), _hasReleaseSpring ? _releaseSpringY.Ease : _releaseEase);
    }

    // A zero duration write completes inside Set, so a move on one axis alone would settle before the other axis is written.
    private void SetAxes(double? x, double durationX, EaseValue? easeX, double? y, double durationY, EaseValue? easeY)
    {
        var writesX = !_disabledX && x is not null;
        var writesY = !_disabledY && y is not null;
        var manual = _manual;
        _manual = true;
        if (writesX) Animate.Set(_xProp, x!.Value, durationX, easeX);
        if (writesY) Animate.Set(_yProp, y!.Value, durationY, easeY);
        _manual = manual;
        if (!manual && (writesX || writesY) && (!writesX || durationX == 0) && (!writesY || durationY == 0)) HandleSettle(Animate);
    }

    private double ReturnDuration(Spring spring, double from, double to)
    {
        spring.Velocity = 0;
        return from == to ? 0 : SettlingDuration(spring);
    }

    private double SettlingDuration(Spring spring) =>
        _hasReleaseSpring ? spring.SettlingDuration : spring.SettlingDuration - Spring.RestDuration * Globals.TimeScale;

    // Skipped once a later Reparent or a revert has replaced the flight.
    private async void RearmLayoutAfter(LayoutTransition flight)
    {
        await flight.Then();
        if (LayoutTransition.Live(_target) != flight || _displacedLayout is not { } layout) return;
        _displacedLayout = null;
        layout.Rearm();
    }

    // A null velocity carries on at the axis's current speed. Returns the longest axis duration.
    private double FlingRelease(Inertia template, double? velocityX, double? velocityY)
    {
        var longest = 0d;
        if (!_disabledX) _destX = Fling(_xProp, velocityX, template, _snapX, _boundsLeft, _boundsRight, ref longest);
        if (!_disabledY) _destY = Fling(_yProp, velocityY, template, _snapY, _boundsTop, _boundsBottom, ref longest);
        return longest;
    }

    // Returns where the axis comes to rest.
    private double Fling(string property, double? velocity, Inertia template, Func<double, double>? snap, double min, double max, ref double longest)
    {
        // A target larger than its container inverts its bounds, which Inertia rejects.
        var settings = template with { ModifyTarget = template.ModifyTarget ?? snap, Min = Math.Min(min, max), Max = Math.Max(min, max) };
        if (velocity is { } v) Animate.Fling(property, v, settings);
        else Animate.Fling(property, settings);
        var animation = Animate.Animations[Presto.Canonicalize(_target, property)];
        longest = Math.Max(longest, animation.Duration);
        var tween = animation._headTween!;
        // The ease's end rather than the to-number: a release on a bound moving outward peaks there and comes back.
        return Utils.Round(tween._fromNumber + (tween._toNumber - tween._fromNumber) * tween._ease(1), 5);
    }

    // Null when no release is in flight; the overshoot clears _released mid-throw, so its ticker is checked first.
    private double? ReleaseRemaining()
    {
        if (!_overshootTicker.Paused) return _overshootTicker.Duration - _overshootTicker.CurrentTime;
        if (!_released) return null;
        double? remaining = null;
        foreach (var animation in Animate.Animations.Values)
        {
            if (!animation.Paused) remaining = Math.Max(remaining ?? 0, animation.Duration - animation.CurrentTime);
        }
        return remaining;
    }

    private void RetargetRelease(double remaining)
    {
        if (_parameters.ReleaseInertia is { } inertia)
        {
            if (Utils.Clamp(_destX, _boundsLeft, _boundsRight) == _destX && Utils.Clamp(_destY, _boundsTop, _boundsBottom) == _destY) return;
            FlingRelease(inertia, null, null);
            _snappedX = _destX;
            _snappedY = _destY;
            return;
        }
        var destX = Snapped(_destX, _snapX, _boundsLeft, _boundsRight);
        var destY = Snapped(_destY, _snapY, _boundsTop, _boundsBottom);
        if (destX == _destX && destY == _destY) return;
        _overshootTicker.Pause();
        _overshootOut?.Cancel();
        _overshootBack?.Cancel();
        _overshootOut = _overshootBack = null;
        // The overshoot ticker leaves _manual set, which would swallow the settle this write ends in.
        _manual = false;
        _destX = _snappedX = destX;
        _destY = _snappedY = destY;
        SetAxes(destX, remaining, _releaseEase, destY, remaining, _releaseEase);
    }

    private double ReleaseDuration(Spring spring, double from, double to, double min, double max, double thrown, double velocity, bool otherAxisDisabled)
    {
        // On a single axis the spring is launched along the throw instead of at the raw pointer speed.
        var direction = to == max ? from > max ? -1 : 1 : from < min ? -1 : 1;
        var distance = Utils.Round(from - to, 0);
        spring.Velocity = otherAxisDisabled && _hasReleaseSpring
            ? distance != 0 ? thrown * direction / Math.Abs(distance) : 0
            : velocity;
        return from == to ? 0 : SettlingDuration(spring);
    }

    private void TakeCursor(Cursor? cursor)
    {
        if (cursor is null) return;
        if (!_cursorTaken)
        {
            _originalCursor = _trigger.Cursor;
            _cursorTaken = true;
        }
        _trigger.Cursor = cursor;
    }

    private void RestoreCursor()
    {
        if (!_cursorTaken) return;
        _trigger.Cursor = _originalCursor;
        _originalCursor = null;
        _cursorTaken = false;
    }

    private void ReleasePointer()
    {
        if (_pointer is { } pointer && ReferenceEquals(pointer.Captured, _trigger)) pointer.Capture(null);
        _pointer = null;
        StopListeningForEscape();
    }

    private void StopListeningForEscape()
    {
        _escapeRoot?.RemoveHandler(InputElement.KeyDownEvent, HandleKeyDown);
        _escapeRoot = null;
    }

    private sealed class Coords
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}
