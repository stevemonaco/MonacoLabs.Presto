using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Pointer-follow tilt: while the pointer moves over an element it turns in 3D towards the pointer, up to
/// <see cref="StrengthProperty"/> degrees at its edges, and springs flat again when the pointer leaves.
/// </summary>
/// <remarks>
/// The tilt drives the <c>"RotateX"</c>, <c>"RotateY"</c> and <c>"Perspective"</c> shorthands through an <see cref="Animatable"/>
/// that lives while the element is attached; detaching or disabling reverts them. Nothing tilts while
/// <see cref="PrestoDefaults.ReducedMotion"/> is set.
/// </remarks>
public static class Tilt
{
    /// <summary>Whether the element tilts towards the pointer.</summary>
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Enabled", typeof(Tilt));

    /// <summary>The largest angle, in degrees, the element turns on either axis, reached with the pointer at its edge.</summary>
    public static readonly AttachedProperty<double> StrengthProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("Strength", typeof(Tilt), 10);

    private static readonly AttachedProperty<Tracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, Tracker?>("Tracker", typeof(Tilt));

    static Tilt() => EnabledProperty.Changed.AddClassHandler<Control>(HandleEnabledChanged);

    /// <summary>Gets whether <paramref name="control"/> tilts towards the pointer.</summary>
    public static bool GetEnabled(Control control) => control.GetValue(EnabledProperty);

    /// <summary>Sets whether <paramref name="control"/> tilts towards the pointer.</summary>
    public static void SetEnabled(Control control, bool value) => control.SetValue(EnabledProperty, value);

    /// <summary>Gets the largest angle <paramref name="control"/> tilts by.</summary>
    public static double GetStrength(Control control) => control.GetValue(StrengthProperty);

    /// <summary>Sets the largest angle <paramref name="control"/> tilts by.</summary>
    public static void SetStrength(Control control, double value) => control.SetValue(StrengthProperty, value);

    private static void HandleEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        control.GetValue(TrackerProperty)?.Release();
        if (e.NewValue is true) control.SetValue(TrackerProperty, new Tracker(control));
        else control.ClearValue(TrackerProperty);
    }

    private sealed class Tracker
    {
        private readonly Control _control;
        private Animatable? _animatable;

        public Tracker(Control control)
        {
            _control = control;
            control.AttachedToVisualTree += HandleAttached;
            control.DetachedFromVisualTree += HandleDetached;
            control.PointerMoved += HandleMoved;
            control.PointerExited += HandleExited;
            if (control.IsAttachedToVisualTree()) HandleAttached(null, null);
        }

        public void Release()
        {
            _control.AttachedToVisualTree -= HandleAttached;
            _control.DetachedFromVisualTree -= HandleDetached;
            _control.PointerMoved -= HandleMoved;
            _control.PointerExited -= HandleExited;
            HandleDetached(null, null);
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs? e)
        {
            _animatable = _control.CreateAnimatable(new AvaloniaAnimatableParams
            {
                ["RotateX"] = new AnimatableProperty { Ease = Eases.Spring(bounce: .25, duration: 500), PreserveVelocity = true },
                ["RotateY"] = new AnimatableProperty { Ease = Eases.Spring(bounce: .25, duration: 500), PreserveVelocity = true },
                ["Perspective"] = 0,
            });
            _animatable.Set("Perspective", Recipes.FlipPerspective);
        }

        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs? e)
        {
            _animatable?.Revert();
            _animatable = null;
        }

        private void HandleMoved(object? sender, PointerEventArgs e)
        {
            // Measured against the untransformed layout slot, since the element's own tilt would skew a position relative to it.
            if (_animatable is null || Engine.Current.Defaults.ReducedMotion || _control.GetVisualParent() is not { } parent) return;
            var bounds = _control.Bounds;
            var position = e.GetPosition(parent);
            var strength = GetStrength(_control);
            _animatable
                .Set("RotateY", Math.Clamp((position.X - bounds.Center.X) / (bounds.Width / 2), -1, 1) * strength)
                .Set("RotateX", -Math.Clamp((position.Y - bounds.Center.Y) / (bounds.Height / 2), -1, 1) * strength);
        }

        private void HandleExited(object? sender, PointerEventArgs e) => _animatable?.Set("RotateX", 0).Set("RotateY", 0);
    }
}
