using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Headless;
using AvaloniaCompositor = Avalonia.Rendering.Composition.Compositor;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Freezes the headless compositor's clock, the <c>Stopwatch</c> behind <c>ServerCompositor.ServerNow</c> that Avalonia 12.1
/// exposes no seam for (docs/ARCHITECTURE.md §4), so tests move compositor animations with <see cref="Advance"/> instead of real time.
/// </summary>
/// <remarks>Headless isolation is per test, so each test has a fresh compositor that runs in real time until it installs.</remarks>
internal static class CompositorClock
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static Stopwatch? s_clock;

    public static void Install()
    {
        Field(typeof(Stopwatch), "_elapsed");
        s_clock = Find();
        s_clock.Stop();
    }

    /// <summary>Forgets the installed clock, so <see cref="Advance"/> fails until the next test installs its own.</summary>
    public static void Uninstall() => s_clock = null;

    public static void Advance(double ms)
    {
        if (s_clock is null) throw new InvalidOperationException("CompositorClock.Install() has not run in this test.");
        Elapsed(s_clock) += (long)Math.Round(ms * Stopwatch.Frequency / 1000);
    }

    // Reflection on every frame allocates, which BridgeAllocationTests counts; Install has checked the field exists.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_elapsed")]
    private static extern ref long Elapsed(Stopwatch stopwatch);

    /// <summary>Whether the compositor has a key frame animation running or a scene that asked for another frame.</summary>
    public static bool IsAnimating()
    {
        var server = Server();
        var animations = Property(server.GetType(), "Animations").GetValue(server)!;
        return (bool)Property(animations.GetType(), "NeedNextTick").GetValue(animations)!;
    }

    private static Stopwatch Find()
    {
        var server = Server();
        return (Stopwatch)Property(server.GetType(), "Clock").GetValue(server)!;
    }

    private static object Server()
    {
        var compositor = (AvaloniaCompositor)Property(typeof(AvaloniaHeadlessPlatform), "Compositor").GetValue(null)!;
        return Property(typeof(AvaloniaCompositor), "Server").GetValue(compositor)!;
    }

    internal static PropertyInfo Property(Type type, string name) => type.GetProperty(name, Any) ?? throw Missing(type, name);

    internal static FieldInfo Field(Type type, string name) => type.GetField(name, Any) ?? throw Missing(type, name);

    private static InvalidOperationException Missing(Type type, string name) => new(
        $"CompositorClock reaches into {type.FullName}.{name}, which this Avalonia or .NET version no longer has. " +
        "Find where ServerCompositor now reads its time (see docs/ARCHITECTURE.md §4) and update CompositorClock.");
}
