using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

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

    private sealed class BrushScene : CompositorScene
    {
        public IImmutableBrush BrushFor(Color color, double opacity = 1) => Brush(color, opacity);

        protected override void OnRender(ImmediateDrawingContext context, Size size)
        {
        }
    }
}