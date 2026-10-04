using Avalonia;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Showcase.Pages;

/// <summary>
/// The SAMPLES-PLAN §4.3 field: thousands of <see cref="Particle"/>s drifting on one staggered, blended animation,
/// parting around the pointer and bursting from a click, all ticked and drawn on the render thread.
/// </summary>
public sealed class ParticleFieldScene : CompositorScene
{
    /// <summary>The count the field starts at, until a <see cref="SetCount"/> says otherwise; measured in SAMPLES-PLAN §4.3.</summary>
    public const int DefaultCount = 5000;

    private const int Seed = 7;
    private const double Wander = 14;
    private const double PartingRadius = 120;
    private const double PartingPush = 52;
    private const double PartingFollow = .3;
    private const double BurstRadius = 260;
    private const double BurstPush = 170;

    private static readonly Point ReadoutOrigin = new(10, 8);

    private readonly SceneText _readout = new(13);

    private Particle[] _particles = [];
    private Animation? _drift;
    private Size _field;
    private Point _pointer;
    private bool _pointerInside;
    private int _count = DefaultCount;
    private int _frames;
    private double _windowStart;
    private string _fps = "scene 0 fps";

    /// <summary>Called on the UI thread once per measuring window with the render thread's frame rate.</summary>
    public Action<double>? FpsChanged { get; set; }

    /// <summary>The field as it stands, for reading a frame back through <see cref="CompositorCanvas.Invoke{TScene}"/>.</summary>
    public IReadOnlyList<Particle> Particles => _particles;

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        Presto.CreateTimer(new TimerParams { OnUpdate = Step });
    }

    /// <inheritdoc/>
    protected override void OnMessage(object message)
    {
        switch (message)
        {
            case SetCount(var count):
                _count = Math.Max(1, count);
                Rebuild();
                break;
            case MovePointer(var position, var inside):
                (_pointer, _pointerInside) = (position, inside);
                break;
            case BurstAt(var position):
                Burst(position);
                break;
        }
    }

    /// <inheritdoc/>
    protected override void OnRender(ImmediateDrawingContext context, Size size)
    {
        context.FillRectangle(Brush(Palette.Background.Color), new Rect(size), 0);
        for (var i = 0; i < _particles.Length; i++)
        {
            var particle = _particles[i];
            context.DrawEllipse(
                Brush(Quantize(particle.Tint), particle.Opacity),
                null,
                new Point(particle.HomeX + particle.X + particle.PushX, particle.HomeY + particle.Y + particle.PushY),
                particle.Size,
                particle.Size);
        }
        _readout.Draw(context, _fps, ReadoutOrigin, Brush(Palette.InkDim.Color));
    }

    private void Step(Timer timer)
    {
        // The canvas has no size yet when the attach message reaches the scene, and gains a new one on every resize.
        if (Size != _field) Rebuild();
        Part(Engine.DeltaTime);
        Measure(timer);
    }

    private void Rebuild()
    {
        _drift?.Cancel();
        _field = Size;
        var columns = Math.Max(1, (int)Math.Round(Math.Sqrt(_count * (_field.Height > 0 ? _field.Width / _field.Height : 1))));
        var rows = Math.Max(1, (int)Math.Ceiling((double)_count / columns));
        var random = Utils.CreateSeededRandom(Seed);
        var particles = new Particle[_count];
        for (var i = 0; i < particles.Length; i++)
        {
            particles[i] = new Particle
            {
                HomeX = ((i % columns) + .5) * _field.Width / columns,
                HomeY = ((i / columns) + .5) * _field.Height / rows,
                Size = random(.9, 2.4, 2),
                Opacity = random(.3, .9, 2),
                Tint = Palette.AccentRamp[(int)random(0, Palette.AccentRamp.Count - 1)],
            };
        }
        _particles = particles;
        #region snippet ParticleFieldDrift
        _drift = Presto.Animate(particles, new AnimationParams
        {
            ["X"] = (FunctionValue)((_, _, _) => random(-Wander, Wander, 1)),
            ["Y"] = (FunctionValue)((_, _, _) => random(-Wander, Wander, 1)),
            ["Opacity"] = new TweenParams { To = (FunctionValue)((_, _, _) => random(.12, 1, 2)), Modifier = Utils.Snap(.05) },
            // Adding colour channels together means nothing, so the tint replaces while the rest of the animation blends.
            ["Tint"] = new TweenParams
            {
                To = (FunctionValue)((_, _, _) => TweenValue.Of(Palette.AccentRamp[(int)random(0, Palette.AccentRamp.Count - 1)])),
                Composition = Composition.Replace,
            },
            Duration = 2800,
            Delay = Presto.Stagger(9, new StaggerParams { Grid = [columns, rows], From = StaggerFrom.Center }),
            Ease = Eases.InOutSine,
            Composition = Composition.Blend,
            Loop = true,
            Alternate = true,
        });
        #endregion
    }

    private void Burst(Point at)
    {
        var hit = new List<Particle>();
        foreach (var particle in _particles)
        {
            if (Offset(particle, at).Length < BurstRadius) hit.Add(particle);
        }
        if (hit.Count == 0) return;
        #region snippet ParticleFieldBurst
        Presto.Animate(hit, new AnimationParams
        {
            ["X"] = new TweenParams[]
            {
                new() { To = (FunctionValue)((target, _, _) => Kick((Particle)target, at).X), Duration = 150, Ease = Eases.OutQuad },
                new() { To = 0, Ease = Eases.Spring(stiffness: 60, damping: 12) },
            },
            ["Y"] = new TweenParams[]
            {
                new() { To = (FunctionValue)((target, _, _) => Kick((Particle)target, at).Y), Duration = 150, Ease = Eases.OutQuad },
                new() { To = 0, Ease = Eases.Spring(stiffness: 60, damping: 12) },
            },
            Composition = Composition.Blend,
        });
        #endregion
    }

    private void Part(double deltaTime)
    {
        for (var i = 0; i < _particles.Length; i++)
        {
            var particle = _particles[i];
            var (pushX, pushY) = _pointerInside ? Displace(particle, _pointer, PartingRadius, PartingPush) : default;
            particle.PushX = Utils.Damp(particle.PushX, pushX, deltaTime, PartingFollow);
            particle.PushY = Utils.Damp(particle.PushY, pushY, deltaTime, PartingFollow);
        }
    }

    private void Measure(Timer timer)
    {
        if (!FpsMeter.TrySample(timer, ref _frames, ref _windowStart, out var fps)) return;
        _fps = FpsMeter.Format("scene", fps);
        PostToUI(() => FpsChanged?.Invoke(fps));
    }

    private static Point Kick(Particle particle, Point at)
    {
        var (dx, dy) = Displace(particle, at, BurstRadius, BurstPush);
        return new Point(particle.X + dx, particle.Y + dy);
    }

    private static (double X, double Y) Displace(Particle particle, Point at, double radius, double push)
    {
        var (x, y, length) = Offset(particle, at);
        if (!(length > .001 && length < radius)) return default;
        var strength = (1 - (length / radius)) * push / length;
        return (x * strength, y * strength);
    }

    private static (double X, double Y, double Length) Offset(Particle particle, Point point)
    {
        var x = particle.HomeX + particle.X - point.X;
        var y = particle.HomeY + particle.Y - point.Y;
        return (x, y, Math.Sqrt((x * x) + (y * y)));
    }

    // Brush caches a small palette, so a tweened per-particle tint is coarsened to keep reusing its brushes.
    private static Color Quantize(Color tint) => Color.FromRgb((byte)(tint.R & 0xF0), (byte)(tint.G & 0xF0), (byte)(tint.B & 0xF0));

    /// <summary>Resizes the field, which lays it out again and rebuilds the drift.</summary>
    public sealed record SetCount(int Count);

    /// <summary>Where the pointer is in canvas coordinates, and whether it is over the canvas at all.</summary>
    public sealed record MovePointer(Point Position, bool Inside);

    /// <summary>Bursts the field away from a point in canvas coordinates.</summary>
    public sealed record BurstAt(Point Position);
}
