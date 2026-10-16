using System.Reflection;
using Avalonia.Controls.ApplicationLifetimes;
using MonacoLabs.Presto.Platform;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// An activatable lifetime the tests raise by hand; the headless platform offers none. A proxy because the reference
/// assembly hides an unimplementable member on the interface and its base class.
/// </summary>
internal class FakeActivatableLifetime : DispatchProxy, IDisposable
{
    private EventHandler<ActivatedEventArgs>? _activated;
    private EventHandler<ActivatedEventArgs>? _deactivated;
    private Func<IActivatableLifetime?>? _findLifetime;
    private IActivatableLifetime? _hooked;

    public IActivatableLifetime Lifetime => (IActivatableLifetime)(object)this;

    /// <summary>Whether <see cref="ActivationHook"/>'s lookup finds this lifetime; until set, it finds none.</summary>
    public bool Found { get; set; }

    public int DeactivatedHandlers => _deactivated?.GetInvocationList().Length ?? 0;

    /// <summary>Makes <see cref="ActivationHook"/> look its lifetime up through this fake until disposed.</summary>
    public static FakeActivatableLifetime Install()
    {
        var fake = (FakeActivatableLifetime)DispatchProxy.Create<IActivatableLifetime, FakeActivatableLifetime>();
        fake._findLifetime = ActivationHook.FindLifetime;
        fake._hooked = ActivationHook.Hooked;
        ActivationHook.FindLifetime = () => fake.Found ? fake.Lifetime : null;
        ActivationHook.Hooked = null;
        return fake;
    }

    public void Background(bool activated) =>
        (activated ? _activated : _deactivated)?.Invoke(this, new ActivatedEventArgs(ActivationKind.Background));

    public void Dispose()
    {
        ActivationHook.FindLifetime = _findLifetime!;
        ActivationHook.Hooked = _hooked;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var handler = args is [EventHandler<ActivatedEventArgs> h] ? h : null;
        switch (targetMethod!.Name)
        {
            case "add_Activated": _activated += handler; break;
            case "remove_Activated": _activated -= handler; break;
            case "add_Deactivated": _deactivated += handler; break;
            case "remove_Deactivated": _deactivated -= handler; break;
            default: return false;
        }
        return null;
    }
}
