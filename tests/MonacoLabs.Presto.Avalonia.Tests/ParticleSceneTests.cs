using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

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
    public void ABurstDrawsAroundItsOriginAndNothingOnceItFinishes()
    {
        var scene = new ParticleScene();
        var canvas = ShowParticles(scene);
        var finished = false;

        canvas.Invoke<ParticleScene>(s => s.Add(Still with { Burst = 12, Lifetime = (500, 500) }, new Point(20, 24), () => finished = true));

        Assert.True(Pump(() => CountPixels(canvas, new Rect(10, 14, 20, 20), Colors.Black) > 0), "the burst never drew");
        Assert.Equal(0, CountPixels(canvas, new Rect(40, 0, 24, 64), Colors.Black));
        Assert.True(Pump(() => finished, 300), "the burst never finished");
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
    public void EveryShapeDraws(string name)
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

        Assert.True(Pump(() => CountPixels(canvas, new Rect(8, 8, 48, 48), Colors.Black) > 0), $"{name} drew nothing");
    }

    [AvaloniaFact]
    public void EachSystemReportsItsFinishOnTheUIThreadAndIsDropped()
    {
        var scene = new ParticleScene();
        var canvas = ShowParticles(scene);
        var finishes = new List<(string Name, bool OnUIThread)>();

        canvas.Invoke<ParticleScene>(s => s.Add(Still with { Burst = 1, Lifetime = (100, 100) }, Origin, () => finishes.Add(("short", Dispatcher.UIThread.CheckAccess()))));
        canvas.Invoke<ParticleScene>(s => s.Add(Still with { Burst = 1, Lifetime = (400, 400) }, Origin, () => finishes.Add(("long", Dispatcher.UIThread.CheckAccess()))));

        Assert.True(Pump(() => finishes.Count == 2, 300), "a system never finished");
        Assert.Equal([("short", true), ("long", true)], finishes);
        Assert.Equal(0, scene.SystemCount);
    }

    [AvaloniaFact]
    public void APausedSystemIsNeitherSteppedNorDroppedAndKeepsDrawingWhereItIs()
    {
        var scene = new ParticleScene();
        var canvas = ShowParticles(scene);
        ParticleSystem? system = null;
        canvas.Invoke<ParticleScene>(s => system = s.Add(Still with { Burst = 1, Speed = (0.02, 0.02), Lifetime = (100, 100) }, Origin));
        Tick();
        canvas.Invoke<ParticleScene>(_ => system!.Paused = true);
        Tick();
        var before = system!.Particles[0];

        TickFor(300);

        var after = system.Particles[0];
        Assert.Equal(1, scene.SystemCount);
        Assert.Equal((before.X, before.Y, before.Age), (after.X, after.Y, after.Age));
        Assert.True(CountPixels(canvas, new Rect(after.X - 3, after.Y - 3, 6, 6), Colors.Black) > 0, "the paused particle is not drawn");

        canvas.Invoke<ParticleScene>(_ => system.Paused = false);
        Assert.True(Pump(() => scene.SystemCount == 0, 30), "the resumed system never finished");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShapesTurnByTheParticlesRotationAndFlutterNarrowsThemByTheCosineOfItsPhase(bool flutter)
    {
        var scene = new ParticleScene();
        var canvas = ShowParticles(scene);
        ParticleSystem? system = null;
        var parameters = Still with { Burst = 1, Size = new(24, 24), Shape = ParticleShape.Rect, Seed = 3 };
        parameters = flutter ? parameters with { Flutter = 1e-9 } : parameters with { Spin = (1e-9, 1e-9) };

        canvas.Invoke<ParticleScene>(s => system = s.Add(parameters, Origin));
        Tick();
        Tick();

        var particle = system!.Particles[0];
        var turn = Utils.DegToRad(particle.Rotation);
        var (width, height) = flutter
            ? (24 * Math.Abs(Math.Cos(particle.FlutterPhase)), 24d)
            : (24 * (Math.Abs(Math.Cos(turn)) + Math.Abs(Math.Sin(turn))), 24 * (Math.Abs(Math.Cos(turn)) + Math.Abs(Math.Sin(turn))));
        Assert.True(Math.Abs(width - 24) > 3, $"seed 3 gives a width of {width}, too close to the unturned square");
        var drawn = Drawn(canvas);
        Assert.InRange(drawn.Width, width - 1, width + 2);
        Assert.InRange(drawn.Height, height - 1, height + 2);
    }

    [AvaloniaTheory]
    [InlineData(false, false, 100)]
    [InlineData(true, false, 100)]
    [InlineData(true, true, 102)]
    public void AColorMixedTowardsAnEndColorIsQuantizedToFourBitsPerChannel(bool mix, bool end, byte shown)
    {
        var gray = new PrestoColor(100, 100, 100);
        var scene = new ParticleScene();
        var canvas = ShowParticles(scene);

        canvas.Invoke<ParticleScene>(s => s.Add(Still with
        {
            Burst = 1, Size = new(16, 16), Shape = ParticleShape.Rect, Colors = [gray],
            ColorMix = mix ? new(0, 0) : null, EndColor = end ? gray : null,
        }, Origin));

        Assert.True(Pump(() => Row(canvas, 32)[32] != Colors.Black), "the particle never drew");
        Assert.Equal(Color.FromRgb(shown, shown, shown), Row(canvas, 32)[32]);
    }

    [AvaloniaTheory]
    [InlineData(' ')]
    [InlineData('')]
    [InlineData('é')]
    public void AGlyphOutsidePrintableAsciiIsRejected(char glyph) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ParticleShape.Glyph(glyph));

    private static Rect Drawn(Visual visual)
    {
        double left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (var y = 0; y < (int)visual.Bounds.Height; y++)
        {
            var row = Row(visual, y);
            for (var x = 0; x < (int)visual.Bounds.Width; x++)
            {
                if (row[x] == Colors.Black) continue;
                (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x + 1), Math.Max(bottom, y + 1));
            }
        }
        return new Rect(left, top, right - left, bottom - top);
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
