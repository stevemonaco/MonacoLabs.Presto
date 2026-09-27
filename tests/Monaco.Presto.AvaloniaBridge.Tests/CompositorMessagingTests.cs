using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Covers the UI thread ↔ render thread message path. Messages are batched by the compositor, so each one lands on the
/// tick after it was posted.
/// </summary>
public sealed class CompositorMessagingTests : CompositorTestBase
{
    [AvaloniaFact]
    public void MessagesArriveInOrderIncludingTheOnesPostedBeforeAttach()
    {
        var scene = new ProbeScene(4000);
        var canvas = new CompositorCanvas { Scene = scene, Width = 64, Height = 64 };
        canvas.Post("a");
        canvas.Post("b");

        Show(canvas);
        canvas.Post("c");
        Tick();

        Assert.Equal<object>(["a", "b", "c"], scene.Messages);
    }

    [AvaloniaFact]
    public void InvokeReachesTheTypedSceneOnItsOwnEngine()
    {
        var scene = new ProbeScene(4000);
        var (_, canvas) = ShowScene(scene);

        canvas.Invoke<ProbeScene>(s => s.InvokedOn = Engine.Current);
        Tick();

        Assert.Same(scene.Engine, scene.InvokedOn);
        Assert.Empty(scene.Messages);
    }

    [AvaloniaFact]
    public async Task AThrowingOnAttachedLeavesTheAnimationItCreatedRunning()
    {
        var scene = new ProbeScene(4000, throwOnAttached: true);

        ShowScene(scene);

        Assert.Same(Presto.Engine, Engine.Current);
        Assert.True(await Pump(() => scene.Target.Opacity < 1), "the scene animation never advanced");
    }

    [AvaloniaFact]
    public async Task AThrowingOnMessageStopsNeitherLaterMessagesNorLaterFrames()
    {
        var scene = new ProbeScene(4000);
        var (_, canvas) = ShowScene(scene);

        canvas.Post(ProbeScene.Boom);
        canvas.Post("after");
        Tick();

        Assert.Equal<object>(["after"], scene.Messages);
        Assert.Same(Presto.Engine, Engine.Current);
        Assert.True(await Pump(() => scene.Target.Opacity < 1), "the scene animation stopped advancing");
    }

    [AvaloniaFact]
    public void PostToUIRunsItsActionOnTheUIThread()
    {
        var (_, canvas) = ShowScene(new ProbeScene(4000));
        bool? onUIThread = null;

        canvas.Invoke<ProbeScene>(s => s.RelayToUI(() => onUIThread = Dispatcher.UIThread.CheckAccess()));
        Tick();

        Assert.Null(onUIThread);

        Dispatcher.UIThread.RunJobs();

        Assert.True(onUIThread);
    }
}
