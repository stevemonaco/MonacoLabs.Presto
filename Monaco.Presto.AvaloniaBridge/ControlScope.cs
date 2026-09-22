using Avalonia;
using Avalonia.Controls;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// A <see cref="Scope"/> whose lifetime is a control's: the animations, timers, timelines, animatables and nested scopes
/// created inside it are reverted when the root leaves the visual tree, or sooner if <see cref="Revert"/> is called by hand.
/// </summary>
public sealed class ControlScope : Scope
{
    private readonly Control _root;

    internal ControlScope(Control root, ScopeParams? parameters)
        : base((parameters ?? new ScopeParams()) with { Root = root })
    {
        _root = root;
        // Not Unloaded: a root detached before its Loaded job runs never raises it, and would leak the scope.
        root.DetachedFromVisualTree += HandleDetached;
    }

    /// <inheritdoc cref="Scope.Revert"/>
    /// <remarks>Also stops listening for the root's detach, so a later detach does nothing.</remarks>
    public override Scope Revert()
    {
        _root.DetachedFromVisualTree -= HandleDetached;
        return base.Revert();
    }

    private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e) => Revert();
}
