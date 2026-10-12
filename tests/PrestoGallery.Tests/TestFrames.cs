using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Tests;

namespace PrestoGallery.Tests;

/// <summary>
/// The frame source the tests step the engine with. One for the whole assembly: <see cref="Presto.Engine"/> outlives
/// the headless sessions, and a source whose clock starts behind the engine's last tick stops it ticking at all.
/// </summary>
internal static class TestFrames
{
    private static readonly ManualFrameSource Source = new();

    private static Application? s_session;

    /// <summary>
    /// Points the engine at the test source, as <see cref="PrestoAvalonia.EnsureInitialized"/> installs the Avalonia one, which
    /// no test can step, and freezes this test's compositor clock so that only <see cref="Tick()"/> moves it.
    /// </summary>
    public static void Install()
    {
        PrestoAvalonia.EnsureInitialized();
        Presto.Engine.FrameSource = Source;
        CompositorClock.Install();
        if (ReferenceEquals(s_session, Application.Current)) return;
        s_session = Application.Current;
        // A loop an earlier test's page left running would keep the engine busy, and so every Settle at its cap.
        DropChildren(Presto.Engine);
    }

    /// <summary>Advances the engine and the compositor's clock one 60 fps frame together and lets the dispatcher and the headless renderer catch up.</summary>
    public static void Tick()
    {
        Source.Advance(16);
        Dispatcher.UIThread.RunJobs();
        CompositorClock.Advance(16);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    /// <summary>Advances <paramref name="frames"/> 60 fps frames.</summary>
    public static void Tick(int frames)
    {
        for (var i = 0; i < frames; i++) Tick();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DropChildren")]
    private static extern void DropChildren(Engine engine);
}
