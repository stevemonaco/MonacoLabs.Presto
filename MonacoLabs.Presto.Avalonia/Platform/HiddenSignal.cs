using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Platform;

/// <summary>
/// Sets <see cref="Engine.Hidden"/> on <see cref="Presto.Engine"/> while every desktop window is minimized, the mobile app
/// is in the background or the Browser page is hidden (GAPS-2-PLAN §6.3).
/// </summary>
internal static class HiddenSignal
{
    private static bool s_minimized;
    private static bool s_backgrounded;

    /// <summary>The windows that must all be minimized, the desktop lifetime's by default; headless tests, which have no lifetime, substitute theirs.</summary>
    internal static Func<IReadOnlyList<Window>?> Windows = static () => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows;

    public static bool Hidden => s_minimized || s_backgrounded;

    /// <summary>Raised on the UI thread after <see cref="Hidden"/> changes.</summary>
    public static event Action? Changed;

    public static void Install()
    {
        Window.WindowStateProperty.Changed.AddClassHandler<Window>((_, _) => Update());
        // Posted so the lifetime has added or removed the window by the time the list is read.
        Window.WindowOpenedEvent.AddClassHandler(typeof(Window), (_, _) => Dispatcher.UIThread.Post(Update));
        Window.WindowClosedEvent.AddClassHandler(typeof(Window), (_, _) => Dispatcher.UIThread.Post(Update));
        Update();
    }

    internal static void OnActivation(ActivationKind kind, bool activated)
    {
        if (kind == ActivationKind.Background) Set(s_minimized, backgrounded: !activated);
    }

    internal static void Update()
    {
        var windows = Windows();
        var minimized = windows is { Count: > 0 };
        for (var i = 0; minimized && i < windows!.Count; i++) minimized = windows[i].WindowState == WindowState.Minimized;
        Set(minimized, s_backgrounded);
    }

    private static void Set(bool minimized, bool backgrounded)
    {
        var was = Hidden;
        s_minimized = minimized;
        s_backgrounded = backgrounded;
        if (Hidden == was) return;
        Presto.Engine.Hidden = Hidden;
        Changed?.Invoke();
    }
}
