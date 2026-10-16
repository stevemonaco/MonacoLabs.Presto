using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace MonacoLabs.Presto.Platform;

/// <summary>
/// Routes the application's <see cref="IActivatableLifetime"/> to the hidden signal and the reduced-motion refresh. The
/// bridge's entry points retry the lookup until it succeeds, since <see cref="Application.Current"/> is bound only by <c>AppBuilder</c>.
/// </summary>
internal static class ActivationHook
{
    internal static Func<IActivatableLifetime?> FindLifetime = static () => Application.Current?.TryGetFeature<IActivatableLifetime>();

    internal static IActivatableLifetime? Hooked;

    public static void TryHook()
    {
        if (Hooked is not null || FindLifetime() is not { } lifetime) return;
        Hooked = lifetime;
        // Mobile backgrounding and Browser page visibility both arrive as Background activations; File and OpenUri share the events.
        lifetime.Activated += static (_, e) =>
        {
            HiddenSignal.OnActivation(e.Kind, activated: true);
            PrestoAvalonia.RefreshSystemReducedMotion();
        };
        lifetime.Deactivated += static (_, e) => HiddenSignal.OnActivation(e.Kind, activated: false);
    }
}
