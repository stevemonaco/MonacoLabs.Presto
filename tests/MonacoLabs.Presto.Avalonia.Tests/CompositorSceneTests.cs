using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto.Tests;

/// <summary>Covers the scene lifetime: attach, ticking from the render loop while the main engine is idle, and detach.</summary>
public sealed class CompositorSceneTests : CompositorTestBase
{
    [AvaloniaFact]
    public void AttachCreatesTheChildVisualAndRunsOnAttachedOnTheSceneEngine()
    {
        var scene = new ProbeScene();

        var (_, canvas) = ShowScene(scene);

        Assert.NotNull(ElementComposition.GetElementChildVisual(canvas));
        Assert.Equal(1, scene.Attachments);
        Assert.Same(scene.Engine, scene.AmbientEngine);
        Assert.NotSame(Presto.Engine, scene.Engine);
    }

    [AvaloniaFact]
    public void TheSceneAdvancesWhileTheMainEngineNeverTicks()
    {
        var scene = new ProbeScene();

        ShowScene(scene);

        Assert.True(Pump(() => scene.Target.Opacity < 1), "the scene animation never advanced");
        var advanced = scene.Target.Opacity;
        Assert.True(Pump(() => scene.Target.Opacity < advanced), "the scene animation stopped advancing");
    }

    [AvaloniaFact]
    public void DetachRevertsTheSceneScopeAndEmptiesItsEngine()
    {
        var scene = new ProbeScene();
        var (panel, canvas) = ShowScene(scene);
        Assert.True(Pump(() => scene.Target.Opacity < 1), "the scene animation never advanced");

        panel.Children.Remove(canvas);
        Tick();

        Assert.Equal(1, scene.Target.Opacity);
        Assert.Null(scene.Engine._head);
    }

    [AvaloniaFact]
    public void TheFrameSourceStopsRenewingOnceTheEngineIsIdle()
    {
        var scene = new ProbeScene(80);
        ShowScene(scene);
        var attached = scene.Engine._lastTickTime;

        Assert.True(Pump(() => scene.Engine._head is null), "the scene animation never completed");
        Pump(static () => false, 3);
        var idle = scene.Engine._lastTickTime;
        Pump(static () => false, 5);

        Assert.True(idle > attached, "the scene engine never ticked");
        Assert.Equal(idle, scene.Engine._lastTickTime);
    }

    [AvaloniaFact]
    public void TheSceneRendersAtTheCanvasSizeAndFollowsItWhenItChanges()
    {
        var scene = new ProbeScene(4000);
        var (_, canvas) = ShowScene(scene);

        Assert.True(Pump(() => scene.Rendered == new Size(64, 64)), $"the scene rendered at {scene.Rendered}");

        canvas.Width = canvas.Height = 32;

        Assert.True(Pump(() => scene.Rendered == new Size(32, 32)), $"the scene kept rendering at {scene.Rendered}");
    }

    [AvaloniaFact]
    public void TheBrushCacheNeverGrowsPastItsBoundAndHandsBackTheSameBrushForOneColor()
    {
        var scene = new BrushScene();

        for (var i = 0; i <= CompositorScene.BrushCacheLimit * 2; i++)
        {
            scene.BrushFor(Color.FromUInt32((uint)i | 0xFF000000));
            Assert.True(scene.CachedBrushCount <= CompositorScene.BrushCacheLimit, $"the cache grew to {scene.CachedBrushCount}");
        }

        Assert.Same(scene.BrushFor(Colors.Red, 0.5), scene.BrushFor(Colors.Red, 0.5));
    }

    [AvaloniaFact]
    public void ASceneAttachedToOneCanvasCannotBeAttachedToAnother()
    {
        var scene = new ProbeScene();
        var (panel, _) = ShowScene(scene);
        var empty = new CompositorCanvas();
        panel.Children.Add(empty);

        Assert.Throws<InvalidOperationException>(() => empty.Scene = scene);
        Assert.Throws<InvalidOperationException>(() => panel.Children.Add(new CompositorCanvas { Scene = scene }));
        Tick();
        Assert.Equal(1, scene.Attachments);
    }

    [AvaloniaFact]
    public void TheHandlerMembersThrowBeforeAttachAndAfterDetach()
    {
        var scene = new HandlerScene();
        AssertDetached(scene);

        var (panel, canvas) = ShowScene(scene);
        panel.Children.Remove(canvas);
        Tick();

        AssertDetached(scene);
    }

    [AvaloniaFact]
    public void ReplacingTheSceneWhileAttachedDetachesTheOldOneAndAttachesTheNew()
    {
        var first = new ProbeScene();
        var (_, canvas) = ShowScene(first);
        Assert.True(Pump(() => first.Target.Opacity < 1), "the first scene never advanced");
        var second = new ProbeScene();

        canvas.Scene = second;
        Tick();

        Assert.Equal(1, first.Target.Opacity);
        Assert.Null(first.Engine._head);
        Assert.Equal(1, second.Attachments);
        Assert.True(Pump(() => second.Target.Opacity < 1), "the second scene never advanced");

        canvas.Scene = null;
        Tick();
        Assert.Equal(1, second.Target.Opacity);
        Assert.Null(second.Engine._head);
    }

    [AvaloniaFact]
    public void AttachingAgainGivesTheSceneANewEngineAndScope()
    {
        var scene = new ProbeScene();
        var (panel, canvas) = ShowScene(scene);
        var (engine, scope) = (scene.Engine, scene.Scope);
        panel.Children.Remove(canvas);
        Tick();

        panel.Children.Add(canvas);
        Tick();

        Assert.Equal(2, scene.Attachments);
        Assert.NotSame(engine, scene.Engine);
        Assert.NotSame(scope, scene.Scope);
        Assert.Same(scene.Engine, scene.AmbientEngine);
        Assert.True(Pump(() => scene.Target.Opacity < 1), "the reattached scene never advanced");
    }

    [AvaloniaFact]
    public void TheSceneEngineStartsFromACopyOfTheDefaultsTakenAtAttach()
    {
        var scene = new ProbeScene();
        var canvas = new CompositorCanvas { Scene = scene, Width = 64, Height = 64 };
        Presto.Defaults.Duration = 123;

        Show(canvas);
        Presto.Defaults.Duration = 456;

        Assert.NotSame(Presto.Defaults, scene.Engine.Defaults);
        Assert.Equal((TweenValue)123, scene.Engine.Defaults.Duration);
    }

    [AvaloniaFact]
    public void AThrowingTickIsLoggedAndLaterFramesStillRun()
    {
        using var errors = new LogSink(LogArea.Animations, LogEventLevel.Error);
        var scene = new ThrowingTickScene();

        ShowScene(scene);

        Assert.True(Pump(() => errors.Entries.Count > 0), "the faulted tick was not logged");
        Assert.All(errors.Entries, entry => Assert.Equal(LogEventLevel.Error, entry.Level));
        var after = scene.Target.Opacity;
        Assert.True(Pump(() => scene.Target.Opacity < after), "the scene stopped ticking after the fault");
    }

    private sealed class ThrowingTickScene : CompositorScene
    {
        private bool _thrown;

        public SceneTarget Target { get; } = new();

        protected override void OnAttached() => Presto.Animate(Target, new()
        {
            ["Opacity"] = 0,
            Duration = 4000,
            Ease = "linear",
            OnUpdate = _ =>
            {
                if (_thrown) return;
                _thrown = true;
                throw new InvalidOperationException(ProbeScene.Boom);
            },
        });

        protected override void OnRender(ImmediateDrawingContext context, Size size)
        {
        }
    }

    private static void AssertDetached(HandlerScene scene)
    {
        // Read while attached, they throw Avalonia's compositor lock error instead.
        const string detached = "The scene is not attached to a CompositorCanvas.";
        Assert.Equal(detached, Assert.Throws<InvalidOperationException>(() => scene.ReadNow()).Message);
        Assert.Equal(detached, Assert.Throws<InvalidOperationException>(() => scene.ReadSize()).Message);
        Assert.Equal(detached, Assert.Throws<InvalidOperationException>(scene.Redraw).Message);
    }

    private sealed class HandlerScene : CompositorScene
    {
        public double ReadNow() => Now;

        public Size ReadSize() => Size;

        public void Redraw() => Invalidate();

        protected override void OnRender(ImmediateDrawingContext context, Size size)
        {
        }
    }

    private sealed class BrushScene : CompositorScene
    {
        public IImmutableBrush BrushFor(Color color, double opacity = 1) => Brush(color, opacity);

        protected override void OnRender(ImmediateDrawingContext context, Size size)
        {
        }
    }
}