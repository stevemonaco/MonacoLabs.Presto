using Avalonia;
using Avalonia.Controls;
using MonacoLabs.Presto;

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

    /// <summary>
    /// Runs <paramref name="place"/> inside <see cref="Scope"/> after the first layout pass that gives
    /// <paramref name="control"/> a size, and never once the page has detached.
    /// </summary>
    protected void AfterFirstArrange(Control control, Action place)
    {
        void Arranged(object? sender, EventArgs e)
        {
            if (control.Bounds is { Width: 0 } or { Height: 0 }) return;
            control.LayoutUpdated -= Arranged;
            Scope.Execute(_ => place());
        }

        Scope.Add(_ =>
        {
            control.LayoutUpdated += Arranged;
            return () => control.LayoutUpdated -= Arranged;
        });
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Scope.Execute(_ => OnAttached());
    }

    // ControlScope stops listening for detach after its first revert, and a page can be attached again.
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Scope.Revert();
    }
}
