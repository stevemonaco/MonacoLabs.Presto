using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Transforms;
using Monaco.Presto.Core;

namespace Monaco.Presto.AvaloniaBridge.Draggables;

/// <summary>
/// Port of anime.js <c>createDraggable</c>: pointer dragging of a control, written through an <see cref="Animatable"/>
/// over the two mapped axis properties, with per-axis enabling and modifiers, a drag threshold, snapping, container
/// bounds and container friction, and a release that throws the target on with the pointer's velocity.
/// </summary>
/// <remarks>One pointer drags at a time, mouse and touch alike, through a pointer capture on the trigger.</remarks>
public sealed class Draggable : IRevertible
{
    private const double DefaultDragThreshold = 3;
    private const double ResizeDebounce = 150;
    private const double ScrollInViewDuration = 350;
    private const double VelocityInterval = 17;
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
    private readonly double[] _velocityStack = new double[3];
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
    private Cursor? _hoverCursor;
    private Cursor? _grabCursor;
    private Cursor? _originalCursor;
    private bool _cursorTaken;
    private double _dragThreshold = DefaultDragThreshold;
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
    private double _snappedX;
    private double _snappedY;
    private double _destX;
    private double _destY;
    private double _velocityTime;
    private int _velocityStackIndex;
    private IPointer? _pointer;
    private bool _manual;
    private bool _enabled;
    private bool _grabbed;
    private bool _dragged;
    private bool _updated;
    private bool _released;
    private bool _scrollLocked;

    internal Draggable(Control target, DraggableParams parameters)
    {
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
        _velocityTime = _engine.FrameSource.Now;

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
                if (!_disabledX) Animate.Set(_xProp, _overshoot.X, 0);
                if (!_disabledY) Animate.Set(_yProp, _overshoot.Y, 0);
            },
        });
        _resizeTicker = Presto.CreateTimer(new TimerParams
        {
            Autoplay = false,
            Duration = ResizeDebounce,
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

    /// <summary>Stops the drag and every animation running on the target's axes, leaving it where it is.</summary>
    public Draggable Stop()
    {
        _updateTicker.Pause();
        _overshootTicker.Pause();
        foreach (var animation in Animate.Animations.Values) animation.Pause();
        Presto.Remove(this);
        Presto.Remove(_overshoot);
        Presto.Remove(_scroll);
        return this;
    }

    /// <summary>Stops the drag and puts both axes back to 0.</summary>
    public Draggable Reset()
    {
        Stop();
        _resizeTicker.Pause();
        _grabbed = _dragged = _updated = _released = false;
        SetX(0, true);
        SetY(0, true);
        _coordX = _coordY = 0;
        _pointerX = _pointerY = _pointerPrevX = _pointerPrevY = _appliedX = _appliedY = _grabX = _grabY = 0;
        ClearVelocity();
        return this;
    }

    /// <summary>Reads the parameters that can change — container, padding, snapping, threshold, friction, velocity, cursors — and measures the bounds again.</summary>
    public Draggable Refresh()
    {
        var currentX = X;
        var currentY = Y;
        _padding = _parameters.ContainerPadding ?? default;
        _snapX = _parameters.X?.Snap ?? _parameters.Snap;
        _snapY = _parameters.Y?.Snap ?? _parameters.Snap;
        _dragThreshold = _parameters.DragThreshold ?? DefaultDragThreshold;
        _containerFriction = Utils.Clamp(_parameters.ContainerFriction ?? .8, 0, 1);
        _releaseContainerFriction = Utils.Clamp(_parameters.ReleaseContainerFriction ?? _containerFriction, 0, 1);
        _minVelocity = _parameters.MinVelocity ?? 0;
        _maxVelocity = _parameters.MaxVelocity ?? 50;
        _velocityMultiplier = _parameters.VelocityMultiplier ?? 1;
        _scrollSpeed = _parameters.ScrollSpeed ?? 1.5;
        _scrollThreshold = _parameters.ScrollThreshold ?? 20;
        var cursor = _parameters.Cursor;
        _hoverCursor = cursor is { Enabled: false } ? null : cursor?.OnHover ?? s_grab;
        _grabCursor = cursor is { Enabled: false } ? null : cursor?.OnGrab ?? s_grabbing;
        UpdateBoundingValues();
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
        Presto.Animate(_scroll, new AnimationParams
        {
            ["X"] = ClampScroll(offset.X + overflow.X, scroller.Extent.Width, scroller.Viewport.Width),
            ["Y"] = ClampScroll(offset.Y + overflow.Y, scroller.Extent.Height, scroller.Viewport.Height),
            Duration = duration ?? ScrollInViewDuration,
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
        if (!_disabledX && overflow.X != 0)
        {
            Animate.Set(_xProp, Snapped(X - overflow.X, _snapX, _boundsLeft, _boundsRight), duration ?? ScrollInViewDuration, easeValue);
        }
        if (!_disabledY && overflow.Y != 0)
        {
            Animate.Set(_yProp, Snapped(Y - overflow.Y, _snapY, _boundsTop, _boundsBottom), duration ?? ScrollInViewDuration, easeValue);
        }
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
        _grabbed = _dragged = _updated = _released = false;
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
        _target.SizeChanged -= HandleSizeChanged;
        if (_parameters.Container is { } container && container != _target) container.SizeChanged -= HandleSizeChanged;
        return this;
    }

    void IRevertible.Revert() => Revert();

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

    // Past the bounds only `factor` of the overshoot is kept.
    private static double Cushion(double value, double min, double max, double factor) =>
        value > max ? max + (value - max) * factor : value < min ? min + (value - min) * factor : value;

    private static double Snapped(double value, Func<double, double>? snap, double min, double max) =>
        Utils.Clamp(Utils.Round(snap?.Invoke(value) ?? value, 5), min, max);

    private static double ClampScroll(double value, double extent, double viewport) =>
        Utils.Clamp(value, 0, Math.Max(0, extent - viewport));

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
        var time = _engine.FrameSource.Now;
        var elapsed = time - _velocityTime;
        if (elapsed < VelocityInterval) return Velocity;
        _velocityTime = time;
        _velocityStack[_velocityStackIndex] =
            Utils.Round(Utils.Clamp(Math.Sqrt(dx * dx + dy * dy) / elapsed * _velocityMultiplier, _minVelocity, _maxVelocity), 5);
        _velocityStackIndex = (_velocityStackIndex + 1) % _velocityStack.Length;
        Velocity = Math.Max(_velocityStack[0], Math.Max(_velocityStack[1], _velocityStack[2]));
        return Velocity;
    }

    private void ClearVelocity()
    {
        Array.Clear(_velocityStack);
        _velocityStackIndex = 0;
        Velocity = 0;
        Angle = 0;
    }

    private void AutoScroll()
    {
        if (_scrollLocked || _scrollSpeed == 0 || _scrollThreshold == 0) return;
        if (_scroller is not { } scroller) return;
        if (TopLevel.GetTopLevel(scroller) is not { } root || root.TranslatePoint(new Point(_pointerX, _pointerY), scroller) is not { } point) return;
        var viewport = scroller.Viewport;
        var speed = _scrollSpeed * 10;
        var dx = (Utils.Clamp((point.X - _scrollThreshold) / _scrollThreshold, -1, 0)
            + Utils.Clamp((point.X - (viewport.Width - _scrollThreshold)) / _scrollThreshold, 0, 1)) * speed;
        var dy = (Utils.Clamp((point.Y - _scrollThreshold) / _scrollThreshold, -1, 0)
            + Utils.Clamp((point.Y - (viewport.Height - _scrollThreshold)) / _scrollThreshold, 0, 1)) * speed;
        if (dx == 0 && dy == 0) return;
        var offset = scroller.Offset;
        var x = _disabledX ? offset.X : ClampScroll(offset.X + dx, scroller.Extent.Width, viewport.Width);
        var y = _disabledY ? offset.Y : ClampScroll(offset.Y + dy, scroller.Extent.Height, viewport.Height);
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
        SetX(Cushion(_coordX, _boundsLeft, _boundsRight, friction), false);
        SetY(Cushion(_coordY, _boundsTop, _boundsBottom, friction), false);
        ComputeVelocity(movedX, movedY);
        Angle = Math.Atan2(_pointerY - _pointerPrevY, _pointerX - _pointerPrevX);
        if ((_snapX != null && _snappedX != snappedX) || (_snapY != null && _snappedY != snappedY)) _parameters.OnSnap?.Invoke(this);
    }

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
    }

    private void HandleSettle(Animatable _)
    {
        // Cleared before the callback so a drag started from inside it is not taken for the tail of this one.
        if (!_grabbed && _released) _released = false;
        if (_manual) return;
        ClearVelocity();
        _parameters.OnSettle?.Invoke(this);
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
        Stop();
        UpdateBoundingValues();
        var position = e.GetPosition(null);
        var friction = 1 - _containerFriction;
        var x = X;
        var y = Y;
        // The raw coordinate the cushioned position came from, so the next drag picks up where the friction left off.
        _coordX = _prevX = friction == 0 ? x : Cushion(x, _boundsLeft, _boundsRight, 1 / friction);
        _coordY = _prevY = friction == 0 ? y : Cushion(y, _boundsTop, _boundsBottom, 1 / friction);
        _pointerX = _pointerPrevX = _appliedX = _grabX = position.X;
        _pointerY = _pointerPrevY = _appliedY = _grabY = position.Y;
        ClearVelocity();
        TakeCursor(_grabCursor);
        ScrollInView(100, 0, Eases.Out(3));
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
        _released = false;
        _parameters.OnDrag?.Invoke(this);
    }

    private void HandleReleased(object? sender, PointerReleasedEventArgs e) => Release();

    private void HandleCaptureLost(object? sender, PointerCaptureLostEventArgs e) => Release();

    private void Release()
    {
        if (!_grabbed) return;
        // Cleared first: dropping the capture raises PointerCaptureLost, which lands back here.
        _grabbed = false;
        _dragged = false;
        _updateTicker.Pause();
        ReleasePointer();
        TakeCursor(_hoverCursor);

        var x = X;
        var y = Y;
        var velocity = ComputeVelocity(_pointerX - _appliedX, _pointerY - _appliedY);
        var angle = Angle = Math.Atan2(_pointerY - _pointerPrevY, _pointerX - _pointerPrevX);
        var thrown = velocity * ReleaseDistance;
        var friction = 1 - _releaseContainerFriction;
        var thrownX = x + Math.Cos(angle) * thrown;
        var thrownY = y + Math.Sin(angle) * thrown;
        var cushionedX = Cushion(thrownX, _boundsLeft, _boundsRight, friction);
        var cushionedY = Cushion(thrownY, _boundsTop, _boundsBottom, friction);
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
            // Two blended tweens: out to where the throw would have landed, then back to the clamped destination.
            Presto.Animate(_overshoot, new AnimationParams
            {
                ["X"] = new TweenParams { To = cushionedX, Duration = durationX * .65 },
                ["Y"] = new TweenParams { To = cushionedY, Duration = durationY * .65 },
                Ease = _releaseEase,
                Composition = Composition.Blend,
            });
            Presto.Animate(_overshoot, new AnimationParams
            {
                ["X"] = new TweenParams { To = destX, Duration = durationX },
                ["Y"] = new TweenParams { To = destY, Duration = durationY },
                Ease = _releaseEase,
                Composition = Composition.Blend,
            });
            _overshootTicker.Stretch(Math.Max(durationX, durationY)).Restart();
        }
        else
        {
            if (!_disabledX) Animate.Set(_xProp, destX, durationX, easeX);
            if (!_disabledY) Animate.Set(_yProp, destY, durationY, easeY);
        }

        ScrollInView(longest, _scrollThreshold, _releaseEase);

        var snapped = (_snapX != null && destX != _snappedX) || (_snapY != null && destY != _snappedY);
        _snappedX = destX;
        _snappedY = destY;
        if (snapped) _parameters.OnSnap?.Invoke(this);

        _updated = true;
        _released = true;
        _parameters.OnRelease?.Invoke(this);
    }

    private double ReleaseDuration(Spring spring, double from, double to, double min, double max, double thrown, double velocity, bool otherAxisDisabled)
    {
        // On a single axis the spring is launched along the throw instead of at the raw pointer speed.
        var direction = to == max ? from > max ? -1 : 1 : from < min ? -1 : 1;
        var distance = Utils.Round(from - to, 0);
        spring.Velocity = otherAxisDisabled && _hasReleaseSpring
            ? distance != 0 ? thrown * direction / Math.Abs(distance) : 0
            : velocity;
        if (from == to) return 0;
        return _hasReleaseSpring ? spring.SettlingDuration : spring.SettlingDuration - Spring.RestDuration * Globals.TimeScale;
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
    }

    private sealed class Coords
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}
