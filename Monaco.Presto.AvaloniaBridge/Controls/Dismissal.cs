using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Monaco.Presto.AvaloniaBridge.Controls;

/// <summary>Closes an overlay on Escape or the platform's back request while it is open.</summary>
internal static class Dismissal
{
    internal static readonly IBrush Scrim = new ImmutableSolidColorBrush(Color.FromArgb(0x52, 0, 0, 0));

    /// <summary>Listens on <paramref name="owner"/>'s top level while it is in a visual tree; disposing stops listening.</summary>
    internal static IDisposable Attach(Control owner, Func<bool> isOpen, Action close)
    {
        TopLevel? root = null;

        void Dismiss(object? sender, RoutedEventArgs e)
        {
            if (!isOpen()) return;
            e.Handled = true;
            close();
        }

        void KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Dismiss(sender, e);
        }

        void Subscribe()
        {
            root = TopLevel.GetTopLevel(owner);
            root?.AddHandler(InputElement.KeyDownEvent, KeyDown);
            root?.AddHandler(TopLevel.BackRequestedEvent, Dismiss);
        }

        void Unsubscribe()
        {
            root?.RemoveHandler(InputElement.KeyDownEvent, KeyDown);
            root?.RemoveHandler(TopLevel.BackRequestedEvent, Dismiss);
            root = null;
        }

        EventHandler<VisualTreeAttachmentEventArgs> attached = (_, _) => Subscribe();
        EventHandler<VisualTreeAttachmentEventArgs> detached = (_, _) => Unsubscribe();
        owner.AttachedToVisualTree += attached;
        owner.DetachedFromVisualTree += detached;
        Subscribe();
        return new Disposable(() =>
        {
            owner.AttachedToVisualTree -= attached;
            owner.DetachedFromVisualTree -= detached;
            Unsubscribe();
        });
    }

    private sealed class Disposable(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
