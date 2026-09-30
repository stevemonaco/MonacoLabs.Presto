using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Emitted particles stepped and drawn on the render thread by a <see cref="ParticleScene"/>.</summary>
public sealed class ParticleSceneTests : CompositorTestBase
{
    private static readonly EmitterParams Still = new()
    {
        Speed = (0, 0),
        Size = new(8, 8),
        Opacity = new(1, 1),
        Lifetime = (60000, 60000),
        Seed = 1,
    };

    private static readonly Point Origin = new(32, 32);

    [AvaloniaFact]
    public async Task ABurstDrawsAroundItsOriginAndNothingOnceItFinishes()
    {
        var scene = new ParticleScene();
        var canvas = ShowParticles(scene);
        var finished = false;

        canvas.Invoke<ParticleScene>(s => s.Add(Still with { Burst = 12, Lifetime = (500, 500) }, new Point(20, 24), () => finished = true));

        Assert.True(await Pump(() => CountPixels(canvas, new Rect(10, 14, 20, 20), Colors.Black) > 0), "the burst never drew");
        Assert.Equal(0, CountPixels(canvas, new Rect(40, 0, 24, 64), Colors.Black));
        Assert.True(await Pump(() => finished, 300), "the burst never finished");
        Tick();
        Assert.Equal(0, CountPixels(canvas, new Rect(canvas.Bounds.Size), Colors.Black));
    }

    [AvaloniaFact]
    public void SeveralSystemsNeverExceedTheSceneCap()
    {
        var scene = new ParticleScene { MaxLive = 50 };
        var canvas = ShowParticles(scene);

        for (var i = 0; i < 3; i++) canvas.Invoke<ParticleScene>(s => s.Add(Still with { Burst = 40, Rate = 2000 }, Origin));
        Tick();

        Assert.Equal(50, scene.Live);
        for (var i = 0; i < 10; i++)
        {
            Tick();
            Assert.True(scene.Live <= 50, $"{scene.Live} live");
        }
        Assert.Equal(4000, new ParticleScene().MaxLive);
    }

    [AvaloniaTheory]
    [InlineData("Rect")]
    [InlineData("RoundedRect")]
    [InlineData("Circle")]
    [InlineData("Streak")]
    [InlineData("Glyph")]
    [InlineData("Bitmap")]
    [InlineData("FromGeometry")]
    public async Task EveryShapeDraws(string name)
    {
        var shape = name switch
        {
            "Rect" => ParticleShape.Rect,
            "RoundedRect" => ParticleShape.RoundedRect(4),
            "Circle" => ParticleShape.Circle,
            "Streak" => ParticleShape.Streak,
            "Glyph" => ParticleShape.Glyph('W'),
            "Bitmap" => ParticleShape.Bitmap(WhiteSquare()),
            _ => ParticleShape.FromGeometry(Geometry.Parse("M 0,0 L 10,0 L 5,10 Z"), new Size(10, 10)),
        };
        var scene = new ParticleScene();
        var canvas = ShowParticles(scene);

        canvas.Invoke<ParticleScene>(s => s.Add(Still with { Burst = 1, Size = new(16, 16), Spin = (0.1, 0.1), Shape = shape }, Origin));

        Assert.True(await Pump(() => CountPixels(canvas, new Rect(8, 8, 48, 48), Colors.Black) > 0), $"{name} drew nothing");
    }

    [AvaloniaFact]
    public async Task EachSystemReportsItsFinishOnTheUIThreadAndIsDropped()
    {
        var scene = new ParticleScene();
        var canvas = ShowParticles(scene);
        var finishes = new List<(string Name, bool OnUIThread)>();

        canvas.Invoke<ParticleScene>(s => s.Add(Still with { Burst = 1, Lifetime = (100, 100) }, Origin, () => finishes.Add(("short", Dispatcher.UIThread.CheckAccess()))));
        canvas.Invoke<ParticleScene>(s => s.Add(Still with { Burst = 1, Lifetime = (400, 400) }, Origin, () => finishes.Add(("long", Dispatcher.UIThread.CheckAccess()))));

        Assert.True(await Pump(() => finishes.Count == 2, 300), "a system never finished");
        Assert.Equal([("short", true), ("long", true)], finishes);
        Assert.Equal(0, scene.SystemCount);
    }

    private static CompositorCanvas ShowParticles(ParticleScene scene)
    {
        var canvas = new CompositorCanvas { Scene = scene, Width = 64, Height = 64 };
        new Window { Content = canvas, Background = Brushes.Black, Width = 64, Height = 64 }.Show();
        Tick();
        return canvas;
    }

    private static RenderTargetBitmap WhiteSquare()
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(8, 8));
        using (var context = bitmap.CreateDrawingContext()) context.FillRectangle(Brushes.White, new Rect(0, 0, 8, 8));
        return bitmap;
    }
}
