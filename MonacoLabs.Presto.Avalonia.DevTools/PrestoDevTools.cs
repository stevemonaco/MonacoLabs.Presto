using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MonacoLabs.Presto.DevTools;

/// <summary>Shows the Presto DevTools overlay, an inspector of what <see cref="Presto.Engine"/> runs, over a window.</summary>
/// <remarks>While any overlay is open the engine's diagnostics are enabled; closing the last one disables them and thaws and undilates the engine.</remarks>
public static class PrestoDevTools
{
    private static readonly ConditionalWeakTable<TopLevel, EventHandler<KeyEventArgs>> s_attached = new();
    private static readonly ConditionalWeakTable<TopLevel, DevToolsOverlay> s_open = new();
    private static int s_openCount;

    /// <summary>Toggles the overlay on <paramref name="top"/> whenever <paramref name="gesture"/> is pressed; the default is Ctrl+Shift+F12, since F12 belongs to Avalonia DevTools.</summary>
    public static void Attach(TopLevel top, KeyGesture? gesture = null)
    {
        if (s_attached.TryGetValue(top, out _)) return;
        gesture ??= new KeyGesture(Key.F12, KeyModifiers.Control | KeyModifiers.Shift);
        EventHandler<KeyEventArgs> handler = (_, e) =>
        {
            if (!gesture.Matches(e)) return;
            Toggle(top);
            e.Handled = true;
        };
        top.AddHandler(InputElement.KeyDownEvent, handler, RoutingStrategies.Tunnel);
        s_attached.Add(top, handler);
    }

    /// <summary>Closes the overlay on <paramref name="top"/> and stops listening for its gesture.</summary>
    public static void Detach(TopLevel top)
    {
        Close(top);
        if (!s_attached.TryGetValue(top, out var handler)) return;
        top.RemoveHandler(InputElement.KeyDownEvent, handler);
        s_attached.Remove(top);
    }

    /// <summary>Calls <see cref="Attach"/> on every <see cref="TopLevel"/> as it loads.</summary>
    public static AppBuilder UsePrestoDevTools(this AppBuilder builder, KeyGesture? gesture = null) =>
        builder.AfterSetup(_ => Control.LoadedEvent.AddClassHandler<TopLevel>((top, _) => Attach(top, gesture)));

    internal static bool IsOpen(TopLevel top) => s_open.TryGetValue(top, out _);

    /// <summary>Shows the overlay in <paramref name="top"/>'s <see cref="OverlayLayer"/>.</summary>
    public static void Open(TopLevel top)
    {
        if (IsOpen(top) || OverlayLayer.GetOverlayLayer(top) is not { } layer) return;
        var overlay = new DevToolsOverlay(() => Close(top));
        layer.Children.Add(overlay);
        s_open.Add(top, overlay);
        if (s_openCount++ == 0) Presto.Engine.Diagnostics.Enabled = true;
    }

    /// <summary>Removes the overlay from <paramref name="top"/>.</summary>
    public static void Close(TopLevel top)
    {
        if (!s_open.TryGetValue(top, out var overlay)) return;
        s_open.Remove(top);
        (overlay.Parent as Panel)?.Children.Remove(overlay);
        if (--s_openCount > 0) return;
        var engine = Presto.Engine;
        engine.Diagnostics.Enabled = false;
        engine.Thaw();
        engine.TimeDilation = 1;
    }

    /// <summary>Opens the overlay on <paramref name="top"/> when it is closed, and closes it otherwise.</summary>
    public static void Toggle(TopLevel top)
    {
        if (IsOpen(top)) Close(top);
        else Open(top);
    }
}
