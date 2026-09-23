using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Rendering.Composition;

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
    public async Task TheSceneAdvancesWhileTheMainEngineNeverTicks()
    {
        var scene = new ProbeScene();

        ShowScene(scene);

        Assert.True(await Pump(() => scene.Target.Opacity < 1), "the scene animation never advanced");
        var advanced = scene.Target.Opacity;
        Assert.True(await Pump(() => scene.Target.Opacity < advanced), "the scene animation stopped advancing");
    }

    [AvaloniaFact]
    public async Task DetachRevertsTheSceneScopeAndEmptiesItsEngine()
    {
        var scene = new ProbeScene();
        var (panel, canvas) = ShowScene(scene);
        Assert.True(await Pump(() => scene.Target.Opacity < 1), "the scene animation never advanced");

        panel.Children.Remove(canvas);
        Tick();

        Assert.Equal(1, scene.Target.Opacity);
        Assert.Null(scene.Engine._head);
    }

    [AvaloniaFact]
    public async Task TheFrameSourceStopsRenewingOnceTheEngineIsIdle()
    {
        var scene = new ProbeScene(80);
        ShowScene(scene);
        var attached = scene.Engine._lastTickTime;

        Assert.True(await Pump(() => scene.Engine._head is null), "the scene animation never completed");
        await Pump(static () => false, 3);
        var idle = scene.Engine._lastTickTime;
        await Pump(static () => false, 5);

        Assert.True(idle > attached, "the scene engine never ticked");
        Assert.Equal(idle, scene.Engine._lastTickTime);
    }

    [AvaloniaFact]
    public async Task TheSceneRendersAtTheCanvasSizeAndFollowsItWhenItChanges()
    {
        var scene = new ProbeScene(4000);
        var (_, canvas) = ShowScene(scene);

        Assert.True(await Pump(() => scene.Rendered == new Size(64, 64)), $"the scene rendered at {scene.Rendered}");

        canvas.Width = canvas.Height = 32;

        Assert.True(await Pump(() => scene.Rendered == new Size(32, 32)), $"the scene kept rendering at {scene.Rendered}");
    }
}
