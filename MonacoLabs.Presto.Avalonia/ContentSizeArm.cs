using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto;

/// <summary>The content size transition <see cref="Animate.ContentSizeProperty"/> keeps armed on a control while it is attached.</summary>
internal sealed class ContentSizeArm
{
    private readonly Control _control;
    private ContentSizeTransition? _transition;
    private bool _reduced;

    public ContentSizeArm(Control control)
    {
        _control = control;
        control.AttachedToVisualTree += HandleAttached;
        control.DetachedFromVisualTree += HandleDetached;
        control.LayoutUpdated += HandleLayoutUpdated;
    }

    /// <summary>
    /// Reverts the live transition and, while the control is attached, arms a new one. It records the size the control has
    /// now, unless the control is <paramref name="attaching"/> or not yet laid out: then its first pass only records.
    /// </summary>
    /// <exception cref="NotSupportedException">The builder sets something a content size transition cannot honor.</exception>
    public void Arm(bool attaching)
    {
        Disarm();
        if (Animate.GetContentSize(_control) is not { } builder) return;
        _reduced = Engine.Current.Defaults.ReducedMotion;
        // Read even while detached, so an invalid builder throws where it is set.
        var motion = ContentSizeTransition.Motion(builder);
        if (!_control.IsAttachedToVisualTree()) return;
        PrestoAvalonia.EnsureInitialized();
        _transition = new ContentSizeTransition(_control, motion, record: !attaching, scoped: false);
    }

    public void Release()
    {
        _control.AttachedToVisualTree -= HandleAttached;
        _control.DetachedFromVisualTree -= HandleDetached;
        _control.LayoutUpdated -= HandleLayoutUpdated;
        Disarm();
    }

    private void Disarm()
    {
        _transition?.Revert();
        _transition = null;
    }

    private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e) => Arm(attaching: true);

    private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e) => Disarm();

    private void HandleLayoutUpdated(object? sender, EventArgs e)
    {
        if (_transition is not null && _reduced != Engine.Current.Defaults.ReducedMotion) Arm(attaching: false);
    }
}
