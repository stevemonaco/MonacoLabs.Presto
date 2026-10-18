using System.Diagnostics;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto.Tests;

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
    public void AThrowingOnAttachedLeavesTheAnimationItCreatedRunning()
    {
        var scene = new ProbeScene(4000, throwOnAttached: true);

        ShowScene(scene);

        Assert.Same(Presto.Engine, Engine.Current);
        Assert.True(Pump(() => scene.Target.Opacity < 1), "the scene animation never advanced");
    }

    [AvaloniaFact]
    public void AThrowingOnMessageStopsNeitherLaterMessagesNorLaterFrames()
    {
        var scene = new ProbeScene(4000);
        var (_, canvas) = ShowScene(scene);

        canvas.Post(ProbeScene.Boom);
        canvas.Post("after");
        Tick();

        Assert.Equal<object>(["after"], scene.Messages);
        Assert.Same(Presto.Engine, Engine.Current);
        Assert.True(Pump(() => scene.Target.Opacity < 1), "the scene animation stopped advancing");
    }

    [AvaloniaFact]
    public void PostingOffTheUIThreadFailsAnAssertion()
    {
        var (_, canvas) = ShowScene(new ProbeScene(4000));
        var listeners = Trace.Listeners.Cast<TraceListener>().ToArray();
        Trace.Listeners.Clear();
        Trace.Listeners.Add(new ThrowingListener());
        try
        {
            Exception? failed = null;
            var thread = new Thread(() => failed = Record.Exception(() => canvas.Post("off")));
            thread.Start();
            thread.Join();
            Assert.IsType<DebugAssertException>(failed);
        }
        finally
        {
            Trace.Listeners.Clear();
            Trace.Listeners.AddRange(listeners);
        }
    }

    [AvaloniaFact]
    public void ASceneTimersThenCompletesOnTheThreadPool()
    {
        var scene = new ThenScene();
        ShowScene(scene);

        Assert.True(Pump(() => scene.Timer?.Completed == true), "the scene timer never completed");
        Assert.True(scene.Resumed.Wait(5000));
        Assert.True(scene.OnThreadPool);
    }

    [AvaloniaFact]
    public void PostToUIRunsItsActionOnTheUIThread()
    {
        var (_, canvas) = ShowScene(new ProbeScene(4000));
        bool? onUIThread = null;
        bool? ranInline = null;

        // Checked inside the render pass: the headless render timer may also render within RunJobs, which then drains the post.
        canvas.Invoke<ProbeScene>(s =>
        {
            s.RelayToUI(() => onUIThread = Dispatcher.UIThread.CheckAccess());
            ranInline = onUIThread is not null;
        });
        Tick();
        Dispatcher.UIThread.RunJobs();

        Assert.False(ranInline);
        Assert.True(onUIThread);
    }

    private sealed class DebugAssertException(string? message) : Exception(message);

    private sealed class ThrowingListener : TraceListener
    {
        public override void Fail(string? message, string? detailMessage) => throw new DebugAssertException(message);

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }
    }

    private sealed class ThenScene : CompositorScene
    {
        public ManualResetEventSlim Resumed { get; } = new();

        public bool OnThreadPool { get; private set; }

        public Timer? Timer { get; private set; }

        protected override void OnAttached()
        {
            Timer = Presto.Animate(new SceneTarget(), new() { ["Opacity"] = 0, Duration = 50 });
            Timer.Then().ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
            {
                OnThreadPool = Thread.CurrentThread.IsThreadPoolThread;
                Resumed.Set();
            });
        }

        protected override void OnRender(ImmediateDrawingContext context, Size size)
        {
        }
    }
}
