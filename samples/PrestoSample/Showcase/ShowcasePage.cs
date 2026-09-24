using Avalonia;
using Avalonia.Controls;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Showcase;

/// <summary>
/// Base page for the showcase demos. Everything built inside <see cref="Scope"/> — animations, timelines, timers,
/// draggables, observers — is reverted when the page leaves the visual tree, so no page writes that cleanup itself.
/// </summary>
public abstract class ShowcasePage : UserControl
{
    protected ShowcasePage() => Scope = PrestoAvalonia.CreateScope(this);

    /// <summary>The page's lifetime, current while <see cref="OnAttached"/> runs; it reverts itself on detach.</summary>
    public ControlScope Scope { get; }

    /// <summary>Builds the page's animations, with <see cref="Scope"/> current so they register with it.</summary>
    protected virtual void OnAttached()
    {
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Scope.Execute(_ => OnAttached());
    }
}
