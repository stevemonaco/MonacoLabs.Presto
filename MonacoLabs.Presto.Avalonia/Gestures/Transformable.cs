using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto.Gestures;

/// <summary>
/// Direct manipulation of a control: one pointer pans it, and two pan, pinch-zoom and rotate it about their midpoint,
/// written through an <see cref="Animatable"/> over the X, Y, Scale and Rotate transform shorthands.
/// </summary>
/// <remarks>
/// Pointers are read in the target's parent space, which the transform does not move, and a third one is ignored. A mouse
/// is one pointer, pressed with the left button; a pen counts as a finger.
/// </remarks>
public sealed class Transformable : IRevertible, IReleasable
{
    private static readonly Inertia s_defaultInertia = new();

    // Pointer events arrive long after construction, when the ambient engine may be another one.
    private readonly Engine _engine;
    private readonly Control _target;
    private readonly Control _trigger;
    private readonly TransformableParams _parameters;
    private readonly VelocityTracker _velocityX = new();
    private readonly VelocityTracker _velocityY = new();
    private readonly VelocityTracker _velocityRotation = new();
    private readonly IPointer?[] _pointers = new IPointer?[2];
    private readonly Point[] _positions = new Point[2];
    private int _count;
    private bool _enabled;
    // The translation and scale the pointers alone would give; what is shown is them rubber-banded into the limits.
    private Vector _translation;
    private double _scale;
    private Point _focal;
    private bool _settling;
    // Where a release or a settle comes to rest, which Refresh re-aims an in-flight pan at.
    private Vector _rest;
    private double _restScale;
    private double _restRotation;
    private bool _reverted;

    internal Transformable(Control target, TransformableParams parameters)
    {
        GestureOwners.Claim(target, this);
        _engine = Engine.Current;
        _engine.CurrentScope?.Register(this);
        _target = target;
        _trigger = parameters.Trigger ?? target;
        _parameters = parameters;
        Animate = target.CreateAnimatable(new AvaloniaAnimatableParams
        {
            ["X"] = 0,
            ["Y"] = 0,
            ["Scale"] = 0,
            ["Rotate"] = 0,
            OnUpdate = _ =>
            {
                if (_settling) _parameters.OnUpdate?.Invoke(this);
            },
            OnComplete = _ =>
            {
                if (_settling) Settled();
            },
        });
        Enable();
    }

    /// <summary>The animatable the transform is written through.</summary>
    public Animatable Animate { get; }

    /// <summary>The horizontal translation, in the parent's units.</summary>
    public double X => Utils.Round(Animate.Get("X"), _engine.Precision);

    /// <summary>The vertical translation, in the parent's units.</summary>
    public double Y => Utils.Round(Animate.Get("Y"), _engine.Precision);

    /// <summary>The uniform scale about the target's <see cref="Visual.RenderTransformOrigin"/>.</summary>
    public double Scale => Utils.Round(Animate.Get("Scale"), _engine.Precision);

    /// <summary>The rotation about the target's <see cref="Visual.RenderTransformOrigin"/>, in degrees clockwise.</summary>
    public double Rotation => Utils.Round(Animate.Get("Rotate"), _engine.Precision);

    /// <summary>Speed of the translation over the last few sampled frames, in units per millisecond.</summary>
    public double Velocity => new Vector(_velocityX.Velocity, _velocityY.Velocity).Length;

    /// <summary>Whether pointers hold the target.</summary>
    public bool IsManipulating { get; private set; }

    /// <summary>
    /// Zooms to <paramref name="scale"/>, clamped to the scale limits, keeping the content under <paramref name="focal"/>
    /// (in the parent's space, the container's center when null) fixed. Animated moves play through <see cref="TransformableParams.Settle"/>.
    /// </summary>
    public Transformable ZoomTo(double scale, Point? focal = null, bool animate = true)
    {
        if (_target.GetVisualParent() is not { } parent) return this;
        return Transform(parent, Math.Clamp(scale, _parameters.MinScale, _parameters.MaxScale), Animate.Get("Rotate"), focal, animate);
    }

    /// <summary>Pans to the translation (<paramref name="x"/>, <paramref name="y"/>), clamped into the pan bounds, through <see cref="TransformableParams.Settle"/> when animated.</summary>
    public Transformable PanTo(double x, double y, bool animate = true)
    {
        if (_target.GetVisualParent() is not { } parent) return this;
        var scale = Animate.Get("Scale");
        var rotation = Animate.Get("Rotate");
        return MoveTo(TransformMath.Clamp(new Vector(x, y), PanRange(parent, scale, rotation)), scale, rotation, animate);
    }

    /// <summary>
    /// Rotates to <paramref name="degrees"/> clockwise, keeping the content under <paramref name="focal"/> (in the parent's
    /// space, the container's center when null) fixed. Animated moves play through <see cref="TransformableParams.Settle"/>.
    /// </summary>
    public Transformable RotateTo(double degrees, Point? focal = null, bool animate = true)
    {
        if (_target.GetVisualParent() is not { } parent) return this;
        return Transform(parent, Animate.Get("Scale"), degrees, focal, animate);
    }

    /// <summary>Returns the target to no translation, a scale of 1 and no rotation, through <see cref="TransformableParams.Settle"/> when animated.</summary>
    public Transformable Reset(bool animate = true) => MoveTo(default, 1, 0, animate);

    /// <summary>Stops every animation running on the transform, the release included, leaving the target where it is.</summary>
    public Transformable Stop()
    {
        _settling = false;
        foreach (var animation in Animate.Animations.Values) animation.Pause();
        return this;
    }

    /// <summary>Measures the container again and puts the target back inside the scale limits and pan bounds at once.</summary>
    /// <remarks>A release still in flight is re-aimed into the new bounds from its current value and velocity instead.</remarks>
    public Transformable Refresh()
    {
        if (_target.GetVisualParent() is not { } parent) return this;
        if (_settling)
        {
            var range = PanRange(parent, _restScale, _restRotation);
            if (TransformMath.Clamp(_rest, range) != _rest) _rest = FlingPan(null, null, range);
            return this;
        }
        var scale = Math.Clamp(Animate.Get("Scale"), _parameters.MinScale, _parameters.MaxScale);
        var rotation = Animate.Get("Rotate");
        var translation = new Vector(Animate.Get("X"), Animate.Get("Y"));
        Write(TransformMath.Clamp(translation, PanRange(parent, scale, rotation)), scale, rotation);
        return this;
    }

    /// <summary>Starts listening for pointers, double-taps, the wheel and trackpad gestures on the trigger.</summary>
    public Transformable Enable()
    {
        if (_enabled) return this;
        _enabled = true;
        _reverted = false;
        _trigger.PointerPressed += HandlePressed;
        _trigger.PointerMoved += HandleMoved;
        _trigger.PointerReleased += HandleReleased;
        _trigger.PointerCaptureLost += HandleCaptureLost;
        _trigger.DoubleTapped += HandleDoubleTapped;
        _trigger.PointerWheelChanged += HandleWheel;
        _trigger.PointerTouchPadGestureMagnify += HandleMagnify;
        _trigger.PointerTouchPadGestureRotate += HandleTouchPadRotate;
        return this;
    }

    /// <summary>Stops listening on the trigger and drops the gesture in progress, leaving the target where it is.</summary>
    public Transformable Disable()
    {
        _enabled = false;
        _trigger.PointerPressed -= HandlePressed;
        _trigger.PointerMoved -= HandleMoved;
        _trigger.PointerReleased -= HandleReleased;
        _trigger.PointerCaptureLost -= HandleCaptureLost;
        _trigger.DoubleTapped -= HandleDoubleTapped;
        _trigger.PointerWheelChanged -= HandleWheel;
        _trigger.PointerTouchPadGestureMagnify -= HandleMagnify;
        _trigger.PointerTouchPadGestureRotate -= HandleTouchPadRotate;
        Drop();
        return this;
    }

    /// <summary>Undoes everything the transformable did: the transform goes back to what it was, every handler is detached and the target is free for another gesture.</summary>
    public Transformable Revert()
    {
        Disable();
        Animate.Revert();
        GestureOwners.Release(_target, this);
        _reverted = true;
        return this;
    }

    void IRevertible.Revert() => Revert();

    bool IReleasable.IsReleased => _reverted;

    private static double Angle(Vector v) => Math.Atan2(v.Y, v.X) * 180 / Math.PI;

    private Rect ContainerBox(Visual parent) =>
        _parameters.Container is { } control && control.TranslatePoint(default, parent) is { } at
            ? new Rect(at, control.Bounds.Size)
            : new Rect(parent.Bounds.Size);

    private Rect PanRange(Visual parent, double scale, double rotation)
    {
        var size = _target.Bounds.Size;
        return TransformMath.PanRange(_parameters.PanBounds, ContainerBox(parent), _target.Bounds.Position, size,
            _target.RenderTransformOrigin.ToPixels(size), scale, rotation);
    }

    private Vector FocalTranslation(Vector translation, double scale, double rotation, Point focal, double newScale, double newRotation) =>
        TransformMath.FocalTranslation(_target.Bounds.Position, _target.RenderTransformOrigin.ToPixels(_target.Bounds.Size),
            translation, scale, rotation, focal, newScale, newRotation);

    // Scales and rotates about `focal`, or the container's center, with the translation clamped into the pan bounds.
    private Transformable Transform(Visual parent, double scale, double rotation, Point? focal, bool animate)
    {
        var translation = FocalTranslation(new Vector(Animate.Get("X"), Animate.Get("Y")), Animate.Get("Scale"), Animate.Get("Rotate"),
            focal ?? ContainerBox(parent).Center, scale, rotation);
        return MoveTo(TransformMath.Clamp(translation, PanRange(parent, scale, rotation)), scale, rotation, animate);
    }

    private Transformable MoveTo(Vector translation, double scale, double rotation, bool animate)
    {
        Drop();
        Stop();
        if (!animate)
        {
            Write(translation, scale, rotation);
            return this;
        }
        (_rest, _restScale, _restRotation) = (translation, scale, rotation);
        var (duration, ease) = SettleTiming();
        Animate.Set("X", translation.X, duration, ease).Set("Y", translation.Y, duration, ease)
            .Set("Scale", scale, duration, ease).Set("Rotate", rotation, duration, ease);
        _settling = true;
        SettleWhenIdle();
        return this;
    }

    // Lets go of every pointer without a release.
    private void Drop()
    {
        while (_count > 0) Lift(_pointers[0]!, release: false);
    }

    private void Grab(Visual parent)
    {
        Stop();
        IsManipulating = true;
        for (var i = 0; i < _count; i++) _pointers[i]!.Capture(_trigger);
        // Inverting the rubber band picks up where the friction left off, as Draggable does.
        var inverse = _parameters.ScaleFriction == 0 ? 1 : 1 / _parameters.ScaleFriction;
        var scale = Animate.Get("Scale");
        _scale = TransformMath.RubberBand(scale, _parameters.MinScale, _parameters.MaxScale, inverse);
        _translation = TransformMath.RubberBand(new Vector(Animate.Get("X"), Animate.Get("Y")),
            PanRange(parent, scale, Animate.Get("Rotate")), inverse);
        ResetVelocity();
        _parameters.OnGrab?.Invoke(this);
    }

    // Pans by `pan`, then zooms by `ratio` and turns by `turn` about `focal`.
    private void Manipulate(Visual parent, Vector pan, Point focal, double ratio, double turn)
    {
        var scale = Animate.Get("Scale");
        var rotation = Animate.Get("Rotate");
        _focal = focal;
        _scale *= ratio;
        var newScale = TransformMath.RubberBand(_scale, _parameters.MinScale, _parameters.MaxScale, _parameters.ScaleFriction);
        var newRotation = rotation + turn;
        _translation = FocalTranslation(_translation + pan, scale, rotation, focal, newScale, newRotation);
        Write(TransformMath.RubberBand(_translation, PanRange(parent, newScale, newRotation), _parameters.ScaleFriction), newScale, newRotation);
    }

    private void Write(Vector translation, double scale, double rotation)
    {
        Animate.Set("X", translation.X, 0).Set("Y", translation.Y, 0).Set("Scale", scale, 0).Set("Rotate", rotation, 0);
        var now = _engine.Now;
        _velocityX.Add(translation.X, now);
        _velocityY.Add(translation.Y, now);
        _velocityRotation.Add(rotation, now);
        _parameters.OnUpdate?.Invoke(this);
    }

    private void ResetVelocity()
    {
        _velocityX.Reset();
        _velocityY.Reset();
        _velocityRotation.Reset();
    }

    private int IndexOf(IPointer pointer) =>
        ReferenceEquals(_pointers[0], pointer) ? 0 : _count == 2 && ReferenceEquals(_pointers[1], pointer) ? 1 : -1;

    private void HandlePressed(object? sender, PointerPressedEventArgs e)
    {
        var pointer = e.Pointer;
        if (_count == 2 || _target.GetVisualParent() is not { } parent) return;
        if (pointer.Type == PointerType.Mouse && (_count != 0 || !e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)) return;
        if (_count == 1 && _pointers[0]!.Type == PointerType.Mouse) return;
        _pointers[_count] = pointer;
        _positions[_count] = e.GetPosition(parent);
        _count++;
        if (IsManipulating)
        {
            pointer.Capture(_trigger);
            ResetVelocity();
        }
        else if (_count == 2 || (_parameters.Pan && (_settling || _parameters.PanAtRest || Scale > _parameters.MinScale)))
        {
            Grab(parent);
        }
        else
        {
            return;
        }
        e.Handled = true;
        e.PreventGestureRecognition();
    }

    private void HandleMoved(object? sender, PointerEventArgs e)
    {
        var index = IndexOf(e.Pointer);
        if (index < 0 || _target.GetVisualParent() is not { } parent) return;
        var position = e.GetPosition(parent);
        var previous = _positions[index];
        _positions[index] = position;
        if (!IsManipulating) return;
        e.Handled = true;
        // Gesture recognizers see handled events too, and an ancestor's swipe would steal the capture.
        e.PreventGestureRecognition();
        if (_count == 1)
        {
            if (_parameters.Pan) Manipulate(parent, position - previous, position, 1, 0);
            return;
        }
        var other = _positions[1 - index];
        Vector before = previous - other;
        Vector after = position - other;
        var focal = (position + other) / 2;
        var pan = _parameters.Pan ? (position - previous) / 2 : default;
        var ratio = _parameters.Zoom && before.Length > 0 ? after.Length / before.Length : 1;
        var turn = _parameters.Rotate ? Math.IEEERemainder(Angle(after) - Angle(before), 360) : 0;
        Manipulate(parent, pan, focal, ratio, turn);
    }

    private void HandleReleased(object? sender, PointerReleasedEventArgs e) => Lift(e.Pointer);

    private void HandleCaptureLost(object? sender, PointerCaptureLostEventArgs e) => Lift(e.Pointer);

    private void HandleDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!_parameters.Zoom || _parameters.DoubleTapScale is not { } scale || _target.GetVisualParent() is not { } parent) return;
        e.Handled = true;
        if (Scale <= 1.01) ZoomTo(scale, e.GetPosition(parent));
        else Reset();
    }

    private void HandleWheel(object? sender, PointerWheelEventArgs e)
    {
        var zoom = _parameters.WheelZoom == WheelZoom.Always
            || (_parameters.WheelZoom == WheelZoom.WithControl && e.KeyModifiers.HasFlag(KeyModifiers.Control));
        if (!zoom || !_parameters.Zoom || e.Delta.Y == 0 || _target.GetVisualParent() is not { } parent) return;
        e.Handled = true;
        // Avalonia reports a notch (120 raw units) as a delta of 1.
        ZoomTo(Animate.Get("Scale") * Math.Pow(_parameters.WheelZoomStep, e.Delta.Y), e.GetPosition(parent), false);
    }

    private void HandleMagnify(object? sender, PointerDeltaEventArgs e)
    {
        if (!_parameters.Zoom || _target.GetVisualParent() is not { } parent) return;
        e.Handled = true;
        ZoomTo(Animate.Get("Scale") * (1 + e.Delta.X), e.GetPosition(parent), false);
    }

    private void HandleTouchPadRotate(object? sender, PointerDeltaEventArgs e)
    {
        if (!_parameters.Rotate || _target.GetVisualParent() is not { } parent) return;
        e.Handled = true;
        // AppKit reports rotation counter-clockwise; RotateTransform turns clockwise.
        RotateTo(Animate.Get("Rotate") - e.Delta.X, e.GetPosition(parent), false);
    }

    private void Lift(IPointer pointer, bool release = true)
    {
        var index = IndexOf(pointer);
        if (index < 0) return;
        // Removed first: dropping the capture raises PointerCaptureLost, which lands back here.
        _count--;
        _pointers[index] = _pointers[_count];
        _positions[index] = _positions[_count];
        _pointers[_count] = null;
        if (!IsManipulating) return;
        if (ReferenceEquals(pointer.Captured, _trigger)) pointer.Capture(null);
        if (_count > 0)
        {
            ResetVelocity();
            return;
        }
        IsManipulating = false;
        if (release && _target.GetVisualParent() is { } parent) Release(parent);
    }

    // Rotation lands first because the pan bounds depend on the angle it lands at. Scale never coasts.
    private void Release(Visual parent)
    {
        var template = _parameters.ReleaseInertia ?? s_defaultInertia;
        var reduced = _engine.Defaults.ReducedMotion;
        var scale = Animate.Get("Scale");
        var rotation = Animate.Get("Rotate");
        var translation = new Vector(Animate.Get("X"), Animate.Get("Y"));
        _restScale = Math.Clamp(scale, _parameters.MinScale, _parameters.MaxScale);
        _restRotation = rotation;
        if (_parameters.Rotate)
        {
            var snap = template.ModifyTarget ?? (_parameters.RotationSnap > 0 ? (Func<double, double>)Utils.Snap(_parameters.RotationSnap) : null);
            if (reduced) Animate.Set("Rotate", _restRotation = snap?.Invoke(rotation) ?? rotation, 0);
            else _restRotation = Fling("Rotate", _velocityRotation.Velocity, template with { ModifyTarget = snap, Min = null, Max = null });
        }
        var range = PanRange(parent, _restScale, _restRotation);
        if (_restScale != scale)
        {
            // Translation is affine in scale about a fixed point, so easing both ends alike keeps the focal point still throughout.
            _rest = TransformMath.Clamp(FocalTranslation(translation, scale, rotation, _focal, _restScale, rotation), range);
            var (duration, ease) = SettleTiming();
            Animate.Set("X", _rest.X, duration, ease).Set("Y", _rest.Y, duration, ease).Set("Scale", _restScale, duration, ease);
        }
        else if (reduced)
        {
            _rest = TransformMath.Clamp(translation, range);
            Animate.Set("X", _rest.X, 0).Set("Y", _rest.Y, 0);
        }
        else
        {
            _rest = _parameters.Pan ? FlingPan(_velocityX.Velocity, _velocityY.Velocity, range) : FlingPan(0, 0, range);
        }

        _settling = true;
        _parameters.OnRelease?.Invoke(this);
        if (_settling) SettleWhenIdle();
    }

    private void SettleWhenIdle()
    {
        foreach (var animation in Animate.Animations.Values)
        {
            if (!animation.Paused) return;
        }
        Settled();
    }

    private void Settled()
    {
        _settling = false;
        _parameters.OnSettle?.Invoke(this);
    }

    private (double Duration, EaseValue Ease) SettleTiming()
    {
        var tokens = Recipes.Tokens;
        var settle = _parameters.Settle ?? new AnimationBuilder().Duration(tokens.Medium).Ease(tokens.Ease).WhenReduced(r => r.Duration(0));
        var p = settle.Resolve();
        return ((p.Duration ?? _engine.Defaults.Duration).AsDouble ?? 0, p.Ease ?? _engine.Defaults.Ease);
    }

    // A null velocity carries on at the axis's current speed. Returns where the pan comes to rest.
    private Vector FlingPan(double? velocityX, double? velocityY, Rect range)
    {
        var template = _parameters.ReleaseInertia ?? s_defaultInertia;
        return new Vector(Fling("X", velocityX, template with { Min = range.Left, Max = range.Right }),
            Fling("Y", velocityY, template with { Min = range.Top, Max = range.Bottom }));
    }

    // Returns where the property comes to rest.
    private double Fling(string property, double? velocity, Inertia settings)
    {
        if (velocity is { } v) Animate.Fling(property, v, settings);
        else Animate.Fling(property, settings);
        var tween = Animate.Animations[Presto.Canonicalize(_target, property)]._headTween!;
        // The ease's end rather than the to-number: a release on a bound moving outward peaks there and comes back.
        return tween._fromNumber + (tween._toNumber - tween._fromNumber) * tween._ease(1);
    }
}
