using Avalonia.Headless;
using Avalonia.Threading;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Tests;

/// <summary>
/// The frame source the tests step the engine with. One for the whole assembly: <see cref="Presto.Engine"/> outlives
/// the headless sessions, and a source whose clock starts behind the engine's last tick stops it ticking at all.
/// </summary>
internal static class TestFrames
{
    private static readonly ManualFrameSource Source = new();

    /// <summary>Points the engine at the test source; <see cref="PrestoAvalonia.EnsureInitialized"/> installs the Avalonia one, which no test can step.</summary>
    public static void Install()
    {
        PrestoAvalonia.EnsureInitialized();
        Presto.Engine.FrameSource = Source;
    }

    /// <summary>Advances one 60 fps frame and lets the dispatcher and the headless renderer catch up with it.</summary>
    public static void Tick()
    {
        Source.Advance(16);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    /// <summary>Advances <paramref name="frames"/> 60 fps frames.</summary>
    public static void Tick(int frames)
    {
        for (var i = 0; i < frames; i++) Tick();
    }
}
