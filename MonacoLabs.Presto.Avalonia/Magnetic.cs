using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Bindings;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto;

/// <summary>
/// Magnetic pull: while the pointer is within <see cref="RadiusProperty"/> of an element's edge, the element is pulled towards
/// it by its offset from the element's center × <see cref="StrengthProperty"/> × its proximity, and springs back when it leaves.
/// </summary>
/// <remarks>
/// The pull follows <see cref="PrestoAvalonia.PointerOffsetValue"/> and <see cref="PrestoAvalonia.PointerProximityValue"/>
/// through <see cref="SpringProperty"/> (<see cref="Springs.Snappy"/> unset) into <see cref="Follow.XProperty"/> and <see cref="Follow.YProperty"/>, so an animation
/// of <c>"X"</c> or <c>"Y"</c> layers over it; detaching or disabling clears both. Nothing moves while
/// <see cref="PrestoDefaults.ReducedMotion"/> is set.
/// </remarks>
public static class Magnetic
{
    /// <summary>Whether the element is pulled towards the pointer.</summary>
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Enabled", typeof(Magnetic));

    /// <summary>The fraction of the pointer's offset from the element's center the element moves by, reached with the pointer at its edge.</summary>
    public static readonly AttachedProperty<double> StrengthProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("Strength", typeof(Magnetic), 0.35);

    /// <summary>How far from the element's edge, in DIPs, the pull reaches.</summary>
    public static readonly AttachedProperty<double> RadiusProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("Radius", typeof(Magnetic), 48);

    /// <summary>
    /// The spring the pull follows: a <see cref="Springs"/> preset name, such as <c>"Gentle"</c>, or <c>"bounce,duration"</c>,
    /// such as <c>"0,900"</c>, as <see cref="FollowParams.Spring"/> takes it; unset, <see cref="Springs.Snappy"/>.
    /// </summary>
    public static readonly AttachedProperty<string?> SpringProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Spring", typeof(Magnetic), validate: spring => spring is null || FollowTiming.ParseSpring(spring) is not null);

    private static readonly AttachedProperty<Tracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, Tracker?>("Tracker", typeof(Magnetic));

    static Magnetic()
    {
        EnabledProperty.Changed.AddClassHandler<Control>(HandleEnabledChanged);
        RadiusProperty.Changed.AddClassHandler<Control>((control, _) => control.GetValue(TrackerProperty)?.Rebuild());
        SpringProperty.Changed.AddClassHandler<Control>((control, _) => control.GetValue(TrackerProperty)?.Rebuild());
    }

    /// <summary>Gets whether <paramref name="control"/> is pulled towards the pointer.</summary>
    public static bool GetEnabled(Control control) => control.GetValue(EnabledProperty);

    /// <summary>Sets whether <paramref name="control"/> is pulled towards the pointer.</summary>
    public static void SetEnabled(Control control, bool value) => control.SetValue(EnabledProperty, value);

    /// <summary>Gets the fraction of the pointer's offset <paramref name="control"/> moves by.</summary>
    public static double GetStrength(Control control) => control.GetValue(StrengthProperty);

    /// <summary>Sets the fraction of the pointer's offset <paramref name="control"/> moves by.</summary>
    public static void SetStrength(Control control, double value) => control.SetValue(StrengthProperty, value);

    /// <summary>Gets how far from <paramref name="control"/>'s edge the pull reaches.</summary>
    public static double GetRadius(Control control) => control.GetValue(RadiusProperty);

    /// <summary>Sets how far from <paramref name="control"/>'s edge the pull reaches.</summary>
    public static void SetRadius(Control control, double value) => control.SetValue(RadiusProperty, value);

    /// <summary>Gets the spring <paramref name="control"/>'s pull follows.</summary>
    public static string? GetSpring(Control control) => control.GetValue(SpringProperty);

    /// <summary>Sets the spring <paramref name="control"/>'s pull follows.</summary>
    /// <exception cref="FormatException"><paramref name="value"/> names no spring.</exception>
    public static void SetSpring(Control control, string? value) => control.SetValue(SpringProperty, value);

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
        private Scope? _scope;

        public Tracker(Control control)
        {
            _control = control;
            control.AttachedToVisualTree += HandleAttached;
            control.DetachedFromVisualTree += HandleDetached;
            if (control.IsAttachedToVisualTree()) HandleAttached(null, null);
        }

        public void Release()
        {
            _control.AttachedToVisualTree -= HandleAttached;
            _control.DetachedFromVisualTree -= HandleDetached;
            HandleDetached(null, null);
        }

        // The proximity value's radius and the springs are fixed when made, so a new Radius or Spring rebuilds the graph.
        // The new graph is built first so the shared pointer tracker, and the pointer it holds, outlive the old one.
        public void Rebuild()
        {
            if (_scope is not { } old) return;
            HandleAttached(null, null);
            old.Revert();
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs? e)
        {
            using var origin = PrestoDiagnostics.Origin("Magnetic");
            _scope = new Scope();
            _scope.Execute(_ =>
            {
                var proximity = PrestoAvalonia.PointerProximityValue(_control, GetRadius(_control));
                Follow.SetX(_control, Pull(PrestoAvalonia.PointerOffsetValue(_control, Orientation.Horizontal), proximity));
                Follow.SetY(_control, Pull(PrestoAvalonia.PointerOffsetValue(_control, Orientation.Vertical), proximity));
            });
        }

        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs? e)
        {
            if (_scope is null) return;
            _scope.Revert();
            _scope = null;
            _control.ClearValue(Follow.XProperty);
            _control.ClearValue(Follow.YProperty);
        }

        // Strength and reduced motion are read as the sources move, so a change applies from the next pointer move.
        private MotionValue Pull(MotionValue offset, MotionValue proximity) =>
            MotionValue.Combine(offset, proximity, (o, p) => Engine.Current.Defaults.ReducedMotion ? 0 : o * GetStrength(_control) * p)
                .Spring(GetSpring(_control) is { } spring ? FollowTiming.ParseSpring(spring) : Springs.Snappy);
    }
}
