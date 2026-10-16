using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace MonacoLabs.Presto.Controls;

/// <summary>Closes the most recently opened overlay on Escape or the platform's back request, ahead of the app's top-level handlers.</summary>
internal static class Dismissal
{
    internal static readonly IBrush Scrim = new ImmutableSolidColorBrush(Color.FromArgb(0x52, 0, 0, 0));

    private static readonly ConditionalWeakTable<TopLevel, List<Overlay>> s_open = new();

    // Class handlers run before every instance handler on the top level, whenever that one subscribed.
    static Dismissal()
    {
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>((root, e) =>
        {
            if (e.Key == Key.Escape) Dismiss(root, e);
        });
        TopLevel.BackRequestedEvent.AddClassHandler<TopLevel>(Dismiss);
    }

    /// <summary>Makes <paramref name="owner"/> dismissible, clearing <paramref name="isOpen"/> when it is the newest open overlay on its top level.</summary>
    internal static void Attach(Control owner, StyledProperty<bool> isOpen)
    {
        var overlay = new Overlay(owner, isOpen);
        owner.AttachedToVisualTree += (_, _) => overlay.Track();
        owner.DetachedFromVisualTree += (_, _) => overlay.Track();
        owner.PropertyChanged += (_, e) =>
        {
            if (e.Property == isOpen) overlay.Track();
        };
        overlay.Track();
    }

    private static void Dismiss(TopLevel root, RoutedEventArgs e)
    {
        if (!s_open.TryGetValue(root, out var open) || open.Count == 0) return;
        e.Handled = true;
        open[^1].Close();
    }

    private sealed class Overlay(Control owner, StyledProperty<bool> isOpen)
    {
        private TopLevel? _root;

        // Re-adding moves a reopened overlay to the end, so the list stays in opening order.
        public void Track()
        {
            if (_root != null && s_open.TryGetValue(_root, out var previous)) previous.Remove(this);
            _root = TopLevel.GetTopLevel(owner);
            if (_root != null && owner.GetValue(isOpen)) s_open.GetOrCreateValue(_root).Add(this);
        }

        public void Close() => owner.SetValue(isOpen, false);
    }
}
