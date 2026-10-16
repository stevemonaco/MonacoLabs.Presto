using Avalonia;
using Avalonia.Media;
using Kind = MonacoLabs.Presto.Compositor.ParticleShape.ShapeKind;
using Timer = MonacoLabs.Presto.Timer;

namespace MonacoLabs.Presto.Compositor;

/// <summary>
/// Emitted particles simulated by <see cref="ParticleSystem"/>s, and <see cref="ParticleFormation"/>s of particles held on
/// points, drawn on the render thread; each system or formation is dropped once its last particle dies.
/// </summary>
/// <remarks>
/// Particle positions are in canvas coordinates. Like a plain timer, the scene is the app's own drawing, so reduced motion
/// does not affect it; emitted particles cannot be sought or reversed.
/// </remarks>
public sealed class ParticleScene : CompositorScene
{
    // How many ms of travel a streak trails behind its particle.
    private const double StreakTime = 24;

    private readonly List<(ParticleSystem System, Action? Finished)> _systems = [];
    private readonly List<(ParticleFormation Formation, Action? Finished)> _formations = [];

    /// <summary>The most particles alive across every system at once; further spawns are dropped.</summary>
    public int MaxLive { get; init; } = OperatingSystem.IsBrowser() || OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() ? 1000 : 4000;

    /// <summary>The particles alive across every system and formation; read it on the render thread.</summary>
    public int Live
    {
        get
        {
            var live = 0;
            foreach (var (system, _) in _systems) live += system.Live;
            foreach (var (formation, _) in _formations) live += formation.Count;
            return live;
        }
    }

    internal int SystemCount => _systems.Count;

    /// <summary>Starts a system emitting from <paramref name="origin"/>; call on the render thread, from a scene callback or <see cref="CompositorCanvas.Invoke{TScene}"/>.</summary>
    public ParticleSystem Add(EmitterParams parameters, Point origin) => Add(parameters, origin, null);

    /// <summary>
    /// Places one particle of <paramref name="color"/>, a square <paramref name="size"/> DIPs wide, on each of
    /// <paramref name="points"/> in canvas coordinates, as far as <see cref="MaxLive"/> allows (beyond that, points are taken
    /// evenly across the list); each then springs back to its point when pushed.
    /// Call on the render thread, as <see cref="Add(EmitterParams, Point)"/>.
    /// </summary>
    public ParticleFormation AddFormation(IReadOnlyList<Point> points, Color color, double size = 3) => AddFormation(points, null, color, size, null, null);

    /// <inheritdoc/>
    protected override void OnAttached() => Presto.CreateTimer(new TimerParams { OnUpdate = Step });

    /// <inheritdoc/>
    protected override void OnRender(ImmediateDrawingContext context, Size size)
    {
        foreach (var (system, _) in _systems)
        {
            var parameters = system.Params;
            var shape = parameters.Shape as ParticleShape ?? ParticleShape.Circle;
            var mixed = parameters is { ColorMix: not null, EndColor: not null };
            var flutter = parameters.Flutter != 0;
            foreach (ref readonly var particle in system.Particles)
            {
                var color = particle.Color.ToAvalonia();
                var brush = Brush(mixed ? Quantize(color) : color, particle.Opacity);
                using (context.PushPreTransform(Placement(shape.Kind, particle, flutter))) Draw(context, shape, particle, brush);
            }
        }
        foreach (var (formation, _) in _formations)
        {
            var side = formation.Size;
            foreach (ref readonly var dot in formation.Dots)
            {
                context.FillRectangle(Brush(formation.Color, dot.Opacity), new Rect(dot.X - side / 2, dot.Y - side / 2, side, side));
            }
        }
    }

    /// <summary>
    /// A formation whose particles start on <paramref name="starts"/>, when given, fading in; once every particle rests on its
    /// point, they fade out and <paramref name="settled"/> runs on the UI thread. <paramref name="finished"/> runs there once the formation is dropped.
    /// </summary>
    internal ParticleFormation AddFormation(IReadOnlyList<Point> points, IReadOnlyList<Point>? starts, Color color, double size, Action? settled, Action? finished)
    {
        var formation = new ParticleFormation(this, points, starts, color, size, settled);
        _formations.Add((formation, finished));
        return formation;
    }

    internal ParticleSystem Add(EmitterParams parameters, Point origin, Action? finished)
    {
        var system = new ParticleSystem(parameters, origin.X, origin.Y) { SpawnBudget = MaxLive - Live };
        system.Start();
        _systems.Add((system, finished));
        return system;
    }

    private void Step(Timer timer)
    {
        var dt = Engine.DeltaTime;
        var live = Live;
        for (var i = 0; i < _systems.Count; i++)
        {
            var (system, finished) = _systems[i];
            if (system.Paused) continue;
            var before = system.Live;
            system.SpawnBudget = MaxLive - live;
            system.Step(dt);
            live += system.Live - before;
            if (!system.IsFinished) continue;
            _systems.RemoveAt(i--);
            if (finished is not null) PostToUI(finished);
        }
        for (var i = 0; i < _formations.Count; i++)
        {
            var (formation, finished) = _formations[i];
            if (formation.Step(dt) is { } settled) PostToUI(settled);
            if (formation.Count > 0) continue;
            _formations.RemoveAt(i--);
            if (finished is not null) PostToUI(finished);
        }
        Invalidate();
    }

    private static Matrix Placement(Kind kind, in ParticleState particle, bool flutter)
    {
        var translation = Matrix.CreateTranslation(particle.X, particle.Y);
        if (kind == Kind.Streak) return Matrix.CreateRotation(Math.Atan2(particle.VelocityY, particle.VelocityX)) * translation;
        var scale = kind == Kind.Glyph ? particle.Size / ParticleShape.GlyphFontSize : 1;
        var scaleX = flutter ? scale * Math.Cos(particle.FlutterPhase) : scale;
        return Matrix.CreateScale(scaleX, scale) * Matrix.CreateRotation(Utils.DegToRad(particle.Rotation)) * translation;
    }

    private static void Draw(ImmediateDrawingContext context, ParticleShape shape, in ParticleState particle, IImmutableBrush brush)
    {
        var size = particle.Size;
        var box = new Rect(-size / 2, -size / 2, size, size);
        switch (shape.Kind)
        {
            case Kind.Rect:
                context.FillRectangle(brush, box);
                break;
            case Kind.RoundedRect:
                context.FillRectangle(brush, box, (float)shape.Radius);
                break;
            case Kind.Circle:
                context.DrawEllipse(brush, null, default, size / 2, size / 2);
                break;
            case Kind.Streak:
                var speed = Math.Sqrt((particle.VelocityX * particle.VelocityX) + (particle.VelocityY * particle.VelocityY));
                var length = Math.Max(size, speed * StreakTime);
                context.FillRectangle(brush, new Rect(-length, -size / 2, length, size), (float)(size / 2));
                break;
            case Kind.Glyph:
                var text = shape.Text!;
                text.Draw(context, shape.Character, new Point(-text.Measure(shape.Character) / 2, -text.Height / 2), brush);
                break;
            case Kind.Bitmap:
                var image = shape.Image!;
                var height = size * image.Size.Height / image.Size.Width;
                var bounds = new Rect(-size / 2, -height / 2, size, height);
                using (context.PushOpacity(particle.Opacity, bounds)) context.DrawBitmap(image, bounds);
                break;
        }
    }

    // A color mixed over life takes a new value every frame, so it is coarsened to 4 bits per channel to reuse its brushes.
    private static Color Quantize(Color color) => Color.FromArgb(Quantize(color.A), Quantize(color.R), Quantize(color.G), Quantize(color.B));

    private static byte Quantize(byte channel) => (byte)((channel + 8) / 17 * 17);
}
