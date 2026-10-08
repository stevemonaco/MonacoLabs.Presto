using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge.Compositor;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

/// <summary>
/// The SAMPLES-PLAN §4.3 field and the page that feeds it, headlessly. The pointer work is here because the DevTools
/// MCP input surface has Click, KeyDown, KeyUp and Text and no pointer move, so nothing can drive that in the running app.
/// </summary>
public sealed class ParticleFieldSceneTests
{
    private const int Count = 600;
    private const double Width = 320;
    private const double Height = 200;

    private static readonly Point Center = new(Width / 2, Height / 2);
    private static readonly Rect Canvas = new(0, 0, Width, Height);

    [AvaloniaFact]
    public async Task ThePointerPartsTheFieldAroundItAndItReturnsWhenThePointerLeaves()
    {
        var canvas = await ShowField();

        canvas.Post(new ParticleFieldScene.MovePointer(Center, true));
        Assert.True(await SceneHarness.Pump(() => Read(canvas).Max(p => p.Push.Length) > 8), "the pointer never parted the field");

        var parted = Read(canvas);
        var nearest = parted.MinBy(p => Distance(p));
        var farthest = parted.MaxBy(p => Distance(p));
        Assert.True(nearest.Push.Length > 8, $"the particle under the pointer moved {nearest.Push.Length}px");
        Assert.True(farthest.Push.Length < .5, $"a particle {Distance(farthest):0}px away moved {farthest.Push.Length}px");

        // Inside a cell of the pointer the drift alone can flip the direction between the push and this read.
        foreach (var particle in parted.Where(p => p.Push.Length > 1 && Distance(p) > 20))
        {
            var away = Away(particle);
            Assert.True((away.X * particle.Push.X) + (away.Y * particle.Push.Y) > 0, "a particle was pulled towards the pointer");
        }

        canvas.Post(new ParticleFieldScene.MovePointer(Center, false));
        Assert.True(await SceneHarness.Pump(() => Read(canvas).Max(p => p.Push.Length) < 1), "the field never came back");
    }

    [AvaloniaFact]
    public async Task ABurstBlendsOntoTheDriftWhereTheSameKickWithReplaceNeverLands()
    {
        var canvas = await ShowField();

        Animation? replacing = null;
        canvas.Invoke<ParticleFieldScene>(scene => replacing = Presto.Animate(scene.Particles, new AnimationParams
        {
            ["X"] = 90,
            ["Y"] = 90,
            Duration = 150,
            Composition = Composition.Replace,
        }));
        Assert.False(
            await SceneHarness.Pump(() => Read(canvas).Max(p => p.Drift.Length) > 30, rounds: 30),
            "the replacing kick reached the particles, so the drift is not blending");
        Assert.True(replacing is { Completed: true }, "the replacing kick never ran, so it proves nothing");

        canvas.Post(new ParticleFieldScene.BurstAt(Center));
        Assert.True(
            await SceneHarness.Pump(() => Read(canvas).Max(p => p.Drift.Length) > 40),
            "the blended burst never moved the field");

        Assert.True(
            await SceneHarness.Pump(() => Read(canvas).Max(p => p.Drift.Length) < 20, rounds: 240),
            "the burst never sprang back");
        var settled = Read(canvas);
        await SceneHarness.Pump(() => false, rounds: 10);
        Assert.True(
            settled.Zip(Read(canvas)).Any(pair => pair.First.Drift != pair.Second.Drift),
            "the drift did not outlive the burst that blended onto it");
    }

    [AvaloniaFact]
    public async Task GrowingAndShrinkingTheFieldLeavesItDrawing()
    {
        var canvas = await ShowField();
        var lit = SceneHarness.CountPixels(canvas, Canvas, Palette.Background.Color);

        canvas.Post(new ParticleFieldScene.SetCount(Count * 4));
        Assert.True(
            await SceneHarness.Pump(() => SceneHarness.CountPixels(canvas, Canvas, Palette.Background.Color) > lit * 2),
            "the grown field never reached the screen");

        canvas.Post(new ParticleFieldScene.SetCount(Count));
        Assert.True(
            await SceneHarness.Pump(() => SceneHarness.CountPixels(canvas, Canvas, Palette.Background.Color) < lit * 2),
            "the shrunk field never reached the screen");
        Assert.True(SceneHarness.CountPixels(canvas, Canvas, Palette.Background.Color) > 0, "the field stopped drawing");
    }

    [AvaloniaFact]
    public async Task ThePageHandsPointerInputToTheFieldInCanvasCoordinates()
    {
        var page = new ParticleFieldPage();
        var window = SceneHarness.Show(page, 980, 760);

        var field = page.GetControl<CompositorCanvas>("Field");
        field.Post(new ParticleFieldScene.SetCount(Count));
        var origin = field.TranslatePoint(default, window)!.Value;
        var middle = new Point(field.Bounds.Width / 2, field.Bounds.Height / 2);
        Assert.True(
            await SceneHarness.Pump(() => Read(field) is { Length: Count } laid && laid[^1].Home.Y > 10),
            "the field never laid itself out over the page's canvas");

        window.MouseMove(origin + middle);
        Assert.True(await SceneHarness.Pump(() => Read(field).Max(p => p.Push.Length) > 8), "the pointer never parted the page's field");
        var pushed = Read(field).MaxBy(p => p.Push.Length);
        var away = new Vector(pushed.Home.X + pushed.Drift.X - middle.X, pushed.Home.Y + pushed.Drift.Y - middle.Y);
        // The scene pushes nothing beyond its 120px parting radius, so where the push landed says which point it was given.
        Assert.True(away.Length < 120, $"the parted particle is {away.Length:0}px from the pointer");

        window.MouseDown(origin + middle, MouseButton.Left);
        window.MouseUp(origin + middle, MouseButton.Left);
        Assert.True(await SceneHarness.Pump(() => Read(field).Max(p => p.Drift.Length) > 30), "the click never burst the page's field");

        window.MouseMove(origin + new Point(middle.X, field.Bounds.Height + 20));
        Assert.True(
            await SceneHarness.Pump(() => Read(field).Max(p => p.Push.Length) < 1),
            "the field kept parting for a pointer over the rail below the canvas");

        window.Content = null;
        window.Close();
    }

    private static async Task<CompositorCanvas> ShowField()
    {
        TestFrames.Install();
        var canvas = new CompositorCanvas { Scene = new ParticleFieldScene(), Width = Width, Height = Height };
        SceneHarness.Show(canvas);
        canvas.Post(new ParticleFieldScene.SetCount(Count));

        Assert.True(
            await SceneHarness.Pump(() => Read(canvas) is { Length: Count } field && field[^1].Home.Y > Height / 2),
            "the field never laid itself out over the canvas");
        return canvas;
    }

    // Taken on the render thread because that is the only thread that may touch the field.
    private static (Point Home, Vector Drift, Vector Push)[] Read(CompositorCanvas canvas)
    {
        var state = Array.Empty<(Point Home, Vector Drift, Vector Push)>();
        canvas.Invoke<ParticleFieldScene>(scene => state =
        [
            .. scene.Particles.Select(p => (new Point(p.HomeX, p.HomeY), new Vector(p.X, p.Y), new Vector(p.PushX, p.PushY))),
        ]);
        TestFrames.Tick();
        return state;
    }

    private static Vector Away((Point Home, Vector Drift, Vector Push) particle) =>
        new(particle.Home.X + particle.Drift.X - Center.X, particle.Home.Y + particle.Drift.Y - Center.Y);

    private static double Distance((Point Home, Vector Drift, Vector Push) particle) => Away(particle).Length;
}
