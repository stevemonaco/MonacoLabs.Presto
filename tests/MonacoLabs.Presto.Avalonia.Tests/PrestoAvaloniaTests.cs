using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Tests;

public sealed class PrestoAvaloniaTests : BridgeTestBase
{
    [AvaloniaFact]
    public void EnsureInitializedInstallsOneAvaloniaFrameSource()
    {
        PrestoAvalonia.Initialized = false;

        PrestoAvalonia.EnsureInitialized();
        var installed = Presto.Engine.FrameSource;
        PrestoAvalonia.EnsureInitialized();

        Assert.IsType<AvaloniaFrameSource>(installed);
        Assert.Same(installed, Presto.Engine.FrameSource);
    }

    [AvaloniaFact]
    public void TimerRunsToCompletionOnTheUiThread()
    {
        var completedOnUiThread = false;
        var timer = Presto.CreateTimer(new() { Duration = 100, OnComplete = _ => completedOnUiThread = Dispatcher.UIThread.CheckAccess() });

        RunUntil(() => timer.Completed);

        Assert.True(timer.Completed);
        Assert.True(completedOnUiThread);
    }
}
