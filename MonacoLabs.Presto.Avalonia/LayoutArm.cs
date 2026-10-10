using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto;

/// <summary>
/// The layout transition <see cref="Animate.LayoutProperty"/>, or a list's <see cref="Animate.ItemLayoutProperty"/>, keeps
/// armed on a control while it is attached. A one-shot flight that takes the control over wins its move, and the
/// transition is armed again once the flight has landed.
/// </summary>
internal sealed class LayoutArm
{
    private readonly Control _control;
    private readonly ItemsControl? _list;
    private LayoutTransition? _transition;
    private bool _reduced;
    private bool _suspended;
    private int _generation;

    public LayoutArm(Control control, ItemsControl? list = null)
    {
        _control = control;
        _list = list;
        control.AttachedToVisualTree += HandleAttached;
        control.DetachedFromVisualTree += HandleDetached;
        control.LayoutUpdated += HandleLayoutUpdated;
    }

    private AnimationBuilder? Builder => _list is null ? Animate.GetLayout(_control) : Animate.GetItemLayout(_list);

    /// <summary>
    /// Reverts the live transition and, while the control is attached, arms a new one. It measures where the control is now,
    /// unless the control is <paramref name="attaching"/> or not yet laid out, whose bounds may be stale: then its first pass only records.
    /// </summary>
    /// <exception cref="NotSupportedException">The builder sets something a layout transition cannot honor.</exception>
    public void Arm(bool attaching)
    {
        Disarm();
        if (_suspended || Builder is not { } builder) return;
        _reduced = Engine.Current.Defaults.ReducedMotion;
        // Read even while detached, so an invalid builder throws where it is set.
        var motion = LayoutMotion.From(builder);
        if (!_control.IsAttachedToVisualTree()) return;
        if (LayoutTransition.Live(_control) is { Once: true, InFlight: true } flight)
        {
            Follow(flight);
            return;
        }
        PrestoAvalonia.EnsureInitialized();
        var list = _list ?? Presence.OwnerOfItem(_control);
        _transition = new LayoutTransition(
            _control, first: null, firstOnScreen: false, motion, Animate.GetLayoutMode(_list ?? _control), once: false,
            recordFirst: attaching || !_control.IsArrangeValid, scoped: false)
        {
            Replaced = HandleReplaced,
            List = list is null ? null : ItemChanges.For(list),
        };
    }

    /// <summary>Reverts the live transition and keeps the control unarmed until <see cref="Resume"/>, whatever attaches or changes meanwhile.</summary>
    public void Suspend()
    {
        Disarm();
        _suspended = true;
    }

    /// <summary>Arms again as if attaching, so the control records where it is rather than flying there.</summary>
    public void Resume()
    {
        _suspended = false;
        Arm(attaching: true);
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
        _generation++;
        _transition?.Revert();
        _transition = null;
    }

    private void HandleReplaced(LayoutTransition next)
    {
        _transition = null;
        if (next.Once) Follow(next);
    }

    // A flight plays on its first layout pass, and until then its Then() has nothing to wait for.
    private void Follow(LayoutTransition flight)
    {
        var generation = _generation;
        ItemChanges.AfterLayout(_control, async () =>
        {
            await flight.Then();
            if (_generation == generation) Arm(attaching: false);
        });
    }

    private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e) => Arm(attaching: true);

    private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e) => Disarm();

    private void HandleLayoutUpdated(object? sender, EventArgs e)
    {
        if (_transition is not null && _reduced != Engine.Current.Defaults.ReducedMotion) Arm(attaching: false);
    }
}
