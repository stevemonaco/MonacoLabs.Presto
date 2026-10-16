using Avalonia;
using Avalonia.Media;

namespace MonacoLabs.Presto.Compositor;

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

    // At DefaultDuration, a damping ratio of 0.7 that settles from 150 DIPs in about that long.
    private const double DefaultDuration = 400;
    private const double DefaultStiffness = 0.0004;
    private const double DefaultDamping = 0.028;
    private const double SweepShare = 0.6;
    private const double BurstReach = 0.35;
    private const double RepelStrength = 0.04;
    private const double MaxStep = 16;
    private const double Turbulence = 0.0008;

    private readonly ParticleScene _scene;
    private Action? _settled;
    private Dot[] _dots;
    private double _time;
    private double _releaseFade;
    private double _stiffness = DefaultStiffness;
    private double _damping = DefaultDamping;
    private double _duration = DefaultDuration;

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
            var p = Spread(i, count, points.Count);
            _dots[i] = new Dot(starts?[p] ?? points[p], points[p], starts is null ? 1 : 0, starts is null ? 0 : 1 / FadeTime, i);
        }
        Count = count;
    }

    /// <summary>The live particles.</summary>
    public int Count { get; private set; }

    /// <summary>A point, in canvas coordinates, that pushes away the particles within <see cref="RepelRadius"/> of it; null for none.</summary>
    public Point? Repel { get; set; }

    /// <summary>How far <see cref="Repel"/> reaches, in DIPs.</summary>
    public double RepelRadius { get; set; } = 80;

    /// <summary>
    /// About how long, in ms, a particle takes to settle on a new point: the formation's spring is scaled in time so it keeps
    /// its damping ratio. Defaults to 400.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public double Duration
    {
        get => _duration;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _duration = value;
            var scale = DefaultDuration / value;
            (_stiffness, _damping) = (DefaultStiffness * scale * scale, DefaultDamping * scale);
        }
    }

    internal bool Paused { get; set; }

    /// <summary>
    /// Sends the formation to <paramref name="points"/>, pairing them with particles as <paramref name="transition"/> says;
    /// points left over spawn a particle there that fades in, and particles left over fade out. When <see cref="ParticleScene.MaxLive"/>
    /// leaves too few particles for every point, the points are first thinned evenly across the list. An empty list fades
    /// the whole formation out.
    /// </summary>
    public void Retarget(IReadOnlyList<Point> points, FormationTransition transition = FormationTransition.Nearest)
    {
        var taken = new bool[Count];
        var spawns = new List<int>();
        var shuffled = transition == FormationTransition.Swarm ? Shuffled() : null;
        var grid = shuffled is null ? new DotGrid(_dots.AsSpan(0, Count)) : null;
        var next = 0;
        var active = 0;
        for (var i = 0; i < Count; i++)
        {
            if (!_dots[i].Leaving) active++;
        }
        var used = Math.Min(points.Count, active + Math.Max(0, _scene.MaxLive - _scene.Live));
        for (var q = 0; q < used; q++)
        {
            var p = Spread(q, used, points.Count);
            var nearest = grid?.TakeNearest(_dots, points[p]) ?? (next < shuffled!.Count ? shuffled[next++] : -1);
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
        if (Count + spawns.Count > _dots.Length) Array.Resize(ref _dots, Count + spawns.Count);
        foreach (var p in spawns) _dots[Count++] = new Dot(points[p], points[p], 0, 1 / FadeTime, Count);
        if (transition == FormationTransition.Sweep) Sweep();
        else if (transition == FormationTransition.Burst) Burst();
    }

    private static int Spread(int i, int count, int total) => (int)((long)i * total / count);

    private List<int> Shuffled()
    {
        var order = new List<int>();
        for (var i = 0; i < Count; i++)
        {
            if (!_dots[i].Leaving) order.Add(i);
        }
        var random = Utils.CreateSeededRandom(0);
        for (var i = order.Count - 1; i > 0; i--)
        {
            var j = (int)Math.Min(i, Math.Floor(random(0, 1, 9) * (i + 1)));
            (order[i], order[j]) = (order[j], order[i]);
        }
        return order;
    }

    // Holds each particle's spring and fade for a share of the duration that grows from the formation's left edge to its right.
    private void Sweep()
    {
        var (left, right) = (double.MaxValue, double.MinValue);
        for (var i = 0; i < Count; i++) (left, right) = (Math.Min(left, _dots[i].X), Math.Max(right, _dots[i].X));
        var width = Math.Max(right - left, 1);
        for (var i = 0; i < Count; i++) _dots[i].Hold = (_dots[i].X - left) / width * SweepShare * _duration;
    }

    // An undamped spring thrown at speed v swings out v / sqrt(stiffness), so each particle flies out about BurstReach of the size.
    private void Burst()
    {
        if (Count == 0) return;
        double cx = 0, cy = 0, left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
        for (var i = 0; i < Count; i++)
        {
            ref readonly var dot = ref _dots[i];
            (cx, cy) = (cx + dot.X, cy + dot.Y);
            (left, top, right, bottom) = (Math.Min(left, dot.X), Math.Min(top, dot.Y), Math.Max(right, dot.X), Math.Max(bottom, dot.Y));
        }
        (cx, cy) = (cx / Count, cy / Count);
        var speed = BurstReach * Math.Max(right - left, bottom - top) * Math.Sqrt(_stiffness);
        for (var i = 0; i < Count; i++)
        {
            ref var dot = ref _dots[i];
            var (dx, dy) = (dot.X - cx, dot.Y - cy);
            var length = Math.Sqrt(dx * dx + dy * dy);
            (dx, dy) = length > 1e-6 ? (dx / length, dy / length) : (Math.Cos(dot.Phase), Math.Sin(dot.Phase));
            var spread = 0.6 + 0.8 * (dot.Phase / (2 * Math.PI));
            (dot.VelocityX, dot.VelocityY) = (dot.VelocityX + dx * speed * spread, dot.VelocityY + dy * speed * spread);
        }
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
            var held = dot.Hold > 0;
            if (held) dot.Hold -= dt;
            else dot.Opacity = Math.Clamp(dot.Opacity + dot.FadeRate * dt, 0, 1);
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
                var stiffness = held ? 0 : _stiffness;
                ax = stiffness * (dot.TargetX - dot.X) - _damping * dot.VelocityX;
                ay = stiffness * (dot.TargetY - dot.Y) - _damping * dot.VelocityY;
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

    // Buckets the staying particles into square cells about two particles wide and searches outward ring by ring, scanning
    // what is left directly once a ring would cover more cells than that; ties go to the lower index, as a full scan would.
    private sealed class DotGrid
    {
        private readonly Dictionary<(int, int), List<int>> _cells = [];
        private readonly List<int> _left = [];
        private readonly Dictionary<int, int> _slots = [];
        private readonly double _cell;
        private readonly int _minX, _minY, _maxX, _maxY;

        public DotGrid(ReadOnlySpan<Dot> dots)
        {
            double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
            for (var i = 0; i < dots.Length; i++)
            {
                if (dots[i].Leaving) continue;
                (left, top, right, bottom) = (Math.Min(left, dots[i].X), Math.Min(top, dots[i].Y), Math.Max(right, dots[i].X), Math.Max(bottom, dots[i].Y));
                _slots[i] = _left.Count;
                _left.Add(i);
            }
            _cell = _left.Count == 0 ? 1 : Math.Max(1, 2 * Math.Sqrt((right - left) * (bottom - top) / _left.Count));
            (_minX, _minY, _maxX, _maxY) = (int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
            foreach (var i in _left)
            {
                var key = CellOf(dots[i].X, dots[i].Y);
                if (!_cells.TryGetValue(key, out var cell)) _cells[key] = cell = [];
                cell.Add(i);
                (_minX, _minY, _maxX, _maxY) = (Math.Min(_minX, key.Item1), Math.Min(_minY, key.Item2), Math.Max(_maxX, key.Item1), Math.Max(_maxY, key.Item2));
            }
        }

        public int TakeNearest(Dot[] dots, Point point)
        {
            if (_left.Count == 0) return -1;
            var (cx, cy) = CellOf(point.X, point.Y);
            var rings = Math.Max(Math.Max(cx - _minX, _maxX - cx), Math.Max(cy - _minY, _maxY - cy));
            var (nearest, best) = (-1, double.MaxValue);
            for (var ring = 0; ring <= rings; ring++)
            {
                if ((2 * ring + 1) * (2 * ring + 1) > _left.Count)
                {
                    foreach (var i in _left) Consider(i);
                    break;
                }
                for (var x = cx - ring; x <= cx + ring; x++)
                {
                    Scan(x, cy - ring);
                    if (ring > 0) Scan(x, cy + ring);
                }
                for (var y = cy - ring + 1; y < cy + ring; y++)
                {
                    Scan(cx - ring, y);
                    Scan(cx + ring, y);
                }
                // Every particle in a farther ring is more than ring cells away on some axis.
                if (nearest >= 0 && best < ring * _cell * (ring * _cell)) break;
            }
            if (nearest >= 0) Take(dots, nearest);
            return nearest;

            void Scan(int x, int y)
            {
                if (!_cells.TryGetValue((x, y), out var cell)) return;
                foreach (var i in cell) Consider(i);
            }

            void Consider(int i)
            {
                var distance = (dots[i].X - point.X) * (dots[i].X - point.X) + (dots[i].Y - point.Y) * (dots[i].Y - point.Y);
                if (distance < best || (distance == best && i < nearest)) (nearest, best) = (i, distance);
            }
        }

        private void Take(Dot[] dots, int i)
        {
            _cells[CellOf(dots[i].X, dots[i].Y)].Remove(i);
            var slot = _slots[i];
            var last = _left[^1];
            _left[slot] = last;
            _slots[last] = slot;
            _left.RemoveAt(_left.Count - 1);
            _slots.Remove(i);
        }

        private (int, int) CellOf(double x, double y) => ((int)Math.Floor(x / _cell), (int)Math.Floor(y / _cell));
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

        // Ms before a sweep lets the particle move or fade.
        public double Hold;

        // A golden-ratio sequence, so neighbors drift out of phase.
        public readonly double Phase = index * 0.618034 % 1 * 2 * Math.PI;

        public readonly bool Leaving => FadeRate < 0 || Release <= 0;
    }
}
