using Avalonia;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.Compositor;

/// <summary>
/// Particles of a <see cref="ParticleScene"/> that each spring towards a point of their own, so together they draw a shape
/// such as text sampled with <see cref="Text.GlyphPoints"/>; made by <see cref="ParticleScene.AddFormation(IReadOnlyList{Point}, Color, double)"/>.
/// </summary>
/// <remarks>
/// It belongs to the render thread, as its scene does: reach it from a scene callback or
/// <see cref="CompositorCanvas.Invoke{TScene}"/>. The scene drops it once it holds no particle.
/// </remarks>
public sealed class ParticleFormation
{
    internal const double FadeTime = 300;

    private const double Stiffness = 0.0004;
    private const double Damping = 0.028;
    private const double RepelStrength = 0.04;
    private const double MaxStep = 16;
    private const double Turbulence = 0.0008;

    private readonly ParticleScene _scene;
    private Action? _settled;
    private Dot[] _dots;
    private double _time;
    private double _releaseFade;

    internal ParticleFormation(ParticleScene scene, IReadOnlyList<Point> points, IReadOnlyList<Point>? starts, Color color, double size, Action? settled)
    {
        _scene = scene;
        Color = color;
        Size = size;
        _settled = settled;
        var count = Math.Min(points.Count, Math.Max(0, scene.MaxLive - scene.Live));
        _dots = new Dot[count];
        for (var i = 0; i < count; i++)
        {
            var start = starts?[i] ?? points[i];
            _dots[i] = new Dot(start, points[i], starts is null ? 1 : 0, starts is null ? 0 : 1 / FadeTime, i);
        }
        Count = count;
    }

    /// <summary>The live particles.</summary>
    public int Count { get; private set; }

    /// <summary>A point, in canvas coordinates, that pushes away the particles within <see cref="RepelRadius"/> of it; null for none.</summary>
    public Point? Repel { get; set; }

    /// <summary>How far <see cref="Repel"/> reaches, in DIPs.</summary>
    public double RepelRadius { get; set; } = 80;

    internal bool Paused { get; set; }

    /// <summary>
    /// Sends the formation to <paramref name="points"/>: each point, in order, takes the nearest particle not yet taken; points
    /// left over spawn a particle there that fades in, as far as <see cref="ParticleScene.MaxLive"/> allows, and particles
    /// left over fade out. An empty list fades the whole formation out.
    /// </summary>
    public void Retarget(IReadOnlyList<Point> points)
    {
        var taken = new bool[Count];
        var spawns = new List<int>();
        for (var p = 0; p < points.Count; p++)
        {
            var nearest = -1;
            var best = double.MaxValue;
            for (var i = 0; i < Count; i++)
            {
                ref var dot = ref _dots[i];
                if (taken[i] || dot.Leaving) continue;
                var distance = (dot.X - points[p].X) * (dot.X - points[p].X) + (dot.Y - points[p].Y) * (dot.Y - points[p].Y);
                if (distance < best) (best, nearest) = (distance, i);
            }
            if (nearest < 0)
            {
                spawns.Add(p);
                continue;
            }
            taken[nearest] = true;
            (_dots[nearest].TargetX, _dots[nearest].TargetY) = (points[p].X, points[p].Y);
        }
        for (var i = 0; i < taken.Length; i++)
        {
            if (!taken[i] && !_dots[i].Leaving) _dots[i].FadeRate = -1 / FadeTime;
        }
        var room = Math.Min(spawns.Count, Math.Max(0, _scene.MaxLive - _scene.Live));
        if (Count + room > _dots.Length) Array.Resize(ref _dots, Count + room);
        for (var k = 0; k < room; k++) _dots[Count++] = new Dot(points[spawns[k]], points[spawns[k]], 0, 1 / FadeTime, Count);
    }

    /// <summary>Releases every particle into a drift with turbulence, left to right over the first half of <paramref name="duration"/>, each fading out over the second half.</summary>
    internal void Disperse(Vector drift, double duration)
    {
        double left = double.MaxValue, right = double.MinValue;
        for (var i = 0; i < Count; i++) (left, right) = (Math.Min(left, _dots[i].X), Math.Max(right, _dots[i].X));
        var width = Math.Max(right - left, 1);
        _releaseFade = -2 / duration;
        for (var i = 0; i < Count; i++)
        {
            ref var dot = ref _dots[i];
            dot.Release = (dot.X - left) / width * duration / 2;
            (dot.VelocityX, dot.VelocityY) = (drift.X * (0.5 + dot.Phase % 1), drift.Y * (0.5 + dot.Phase * 7 % 1));
        }
    }

    internal Color Color { get; }

    internal double Size { get; }

    internal ReadOnlySpan<Dot> Dots => _dots.AsSpan(0, Count);

    // An empty formation counts as settled, so a cleared one must not run its settled callback.
    internal void Clear()
    {
        Count = 0;
        _settled = null;
    }

    /// <summary>Advances the particles by <paramref name="dt"/> ms; returns the settled callback on the step every particle has come to rest, which also starts them fading out.</summary>
    internal Action? Step(double dt)
    {
        if (Paused) return null;
        for (var remaining = Math.Min(dt, 100); remaining > 0; remaining -= MaxStep) Advance(Math.Min(remaining, MaxStep));
        if (_settled is not { } settled || !Settled()) return null;
        _settled = null;
        for (var i = 0; i < Count; i++) _dots[i].FadeRate = -1 / FadeTime;
        return settled;
    }

    private void Advance(double dt)
    {
        _time += dt;
        var repel = Repel;
        var radius = RepelRadius;
        var i = 0;
        while (i < Count)
        {
            ref var dot = ref _dots[i];
            dot.Opacity = Math.Clamp(dot.Opacity + dot.FadeRate * dt, 0, 1);
            if (dot.Opacity <= 0 && dot.Leaving)
            {
                dot = _dots[--Count];
                continue;
            }
            double ax, ay;
            if (dot.Release <= 0)
            {
                dot.FadeRate = _releaseFade;
                ax =Turbulence * Math.Sin(dot.Y / 20 + _time / 300 + dot.Phase);
                ay = Turbulence * Math.Cos(dot.X / 20 + _time / 300 + dot.Phase);
            }
            else
            {
                dot.Release -= dt;
                ax = Stiffness * (dot.TargetX - dot.X) - Damping * dot.VelocityX;
                ay = Stiffness * (dot.TargetY - dot.Y) - Damping * dot.VelocityY;
                if (repel is { } at)
                {
                    var (dx, dy) = (dot.X - at.X, dot.Y - at.Y);
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance < radius && distance > 1e-6)
                    {
                        var push = RepelStrength * (1 - distance / radius) / distance;
                        (ax, ay) = (ax + dx * push, ay + dy * push);
                    }
                }
            }
            dot.VelocityX += ax * dt;
            dot.VelocityY += ay * dt;
            dot.X += dot.VelocityX * dt;
            dot.Y += dot.VelocityY * dt;
            i++;
        }
    }

    private bool Settled()
    {
        for (var i = 0; i < Count; i++)
        {
            ref readonly var dot = ref _dots[i];
            if (dot.Opacity < 1 || Math.Abs(dot.X - dot.TargetX) + Math.Abs(dot.Y - dot.TargetY) > 0.5 || Math.Abs(dot.VelocityX) + Math.Abs(dot.VelocityY) > 0.02) return false;
        }
        return true;
    }

    internal struct Dot(Point at, Point target, double opacity, double fadeRate, int index)
    {
        public double X = at.X;
        public double Y = at.Y;
        public double VelocityX;
        public double VelocityY;
        public double TargetX = target.X;
        public double TargetY = target.Y;
        public double Opacity = opacity;
        public double FadeRate = fadeRate;

        // Ms until the particle leaves its spring for the drift; infinite while it holds.
        public double Release = double.PositiveInfinity;

        // A golden-ratio sequence, so neighbors drift out of phase.
        public readonly double Phase = index * 0.618034 % 1 * 2 * Math.PI;

        public readonly bool Leaving => FadeRate < 0 || Release <= 0;
    }
}
