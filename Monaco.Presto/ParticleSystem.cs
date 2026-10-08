using Monaco.Presto.Values;

namespace Monaco.Presto;

/// <summary>
/// A pooled particle emitter simulated by <see cref="Step"/> rather than tweened, so particles can be spawned and retired
/// every frame without allocating. Emitted particles cannot be sought or reversed.
/// </summary>
public sealed class ParticleSystem
{
    private readonly ParticleState[] _particles;
    private readonly RandomGenerator _random;
    private readonly ParticleCurve? _colorMix;
    private readonly PrestoColor _endColor;
    private bool _emitting;
    private double _emitted;
    private double _pending;

    /// <summary>Creates a stopped system emitting from (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public ParticleSystem(EmitterParams parameters, double x = 0, double y = 0)
    {
        Params = parameters;
        X = x;
        Y = y;
        _particles = new ParticleState[parameters.MaxParticles];
        _random = Utils.CreateSeededRandom(parameters.Seed);
        if (parameters is { ColorMix: { } mix, EndColor: { } end })
        {
            _colorMix = mix;
            _endColor = end;
        }
    }

    /// <summary>The parameters the system was built from.</summary>
    public EmitterParams Params { get; }

    /// <summary>The horizontal origin new particles spawn around.</summary>
    public double X { get; set; }

    /// <summary>The vertical origin new particles spawn around.</summary>
    public double Y { get; set; }

    /// <summary>The number of live particles.</summary>
    public int Live { get; private set; }

    /// <summary>True once emission is over and no particle is live.</summary>
    public bool IsFinished => !_emitting && Live == 0;

    /// <summary>The live particles, in no particular order.</summary>
    public ReadOnlySpan<ParticleState> Particles => _particles.AsSpan(0, Live);

    /// <summary>Spawns remaining under a scene-wide cap; each spawn takes one and none happens at 0.</summary>
    internal int SpawnBudget { get; set; } = int.MaxValue;

    internal bool Paused { get; set; }

    /// <summary>Spawns <see cref="EmitterParams.Burst"/> particles, then emits at <see cref="EmitterParams.Rate"/> for <see cref="EmitterParams.Duration"/>.</summary>
    public void Start()
    {
        _emitting = Params.Rate > 0;
        _emitted = 0;
        _pending = 0;
        for (var i = 0; i < Params.Burst; i++) Spawn();
    }

    /// <summary>Stops emitting and lets the live particles finish.</summary>
    public void Stop() => _emitting = false;

    /// <summary>Removes every live particle at once.</summary>
    public void Clear() => Live = 0;

    /// <summary>Advances every live particle by <paramref name="dt"/> ms, retires the ones past their lifetime and emits new ones.</summary>
    public void Step(double dt)
    {
        var gravity = Params.Gravity;
        var damping = Math.Exp(-Params.Drag * dt / 1000);
        var flutter = Params.Flutter * 2 * Math.PI * dt / 1000;
        var i = 0;
        while (i < Live)
        {
            ref var p = ref _particles[i];
            p.Age += dt;
            if (p.Age >= p.Lifetime)
            {
                p = _particles[--Live];
                continue;
            }
            // Half a step of gravity in the position keeps constant acceleration exact at any step size.
            p.X += p.VelocityX * dt;
            p.Y += (p.VelocityY + gravity * dt / 2) * dt;
            p.VelocityY += gravity * dt;
            p.VelocityX *= damping;
            p.VelocityY *= damping;
            p.Rotation += p.Spin * dt;
            p.FlutterPhase += flutter;
            Sample(ref p);
            i++;
        }
        if (_emitting) Emit(dt);
    }

    private void Emit(double dt)
    {
        var duration = Params.Duration > 0 ? Params.Duration : double.PositiveInfinity;
        var span = Math.Min(dt, duration - _emitted);
        _emitted += dt;
        _pending += Params.Rate * span / 1000;
        for (; _pending >= 1; _pending--) Spawn();
        if (_emitted >= duration) _emitting = false;
    }

    private void Spawn()
    {
        if (Live >= _particles.Length || SpawnBudget <= 0) return;
        SpawnBudget--;
        var parameters = Params;
        ref var p = ref _particles[Live++];
        var angle = Utils.DegToRad(parameters.Angle + parameters.Spread * (Draw() * 2 - 1));
        var speed = Draw(parameters.Speed);
        var spin = Draw(parameters.Spin);
        var colors = parameters.Colors;
        p.X = X + (Draw() - .5) * parameters.Area.Width;
        p.Y = Y + (Draw() - .5) * parameters.Area.Height;
        p.VelocityX = Math.Cos(angle) * speed;
        p.VelocityY = Math.Sin(angle) * speed;
        p.Age = 0;
        p.Lifetime = Draw(parameters.Lifetime);
        p.Spin = spin;
        p.Rotation = spin == 0 ? 0 : Draw() * 360;
        p.FlutterPhase = Draw() * 2 * Math.PI;
        p.BaseColor = colors[Math.Min((int)(Draw() * colors.Count), colors.Count - 1)];
        Sample(ref p);
    }

    private void Sample(ref ParticleState p)
    {
        var age = p.Age / p.Lifetime;
        p.Size = Params.Size.At(age);
        p.Opacity = Params.Opacity.At(age);
        if (_colorMix is null)
        {
            p.Color = p.BaseColor;
            return;
        }
        var mix = _colorMix.At(age);
        var from = p.BaseColor;
        p.Color = new(
            from.R + (_endColor.R - from.R) * mix,
            from.G + (_endColor.G - from.G) * mix,
            from.B + (_endColor.B - from.B) * mix,
            from.A + (_endColor.A - from.A) * mix);
    }

    private double Draw() => _random(0, 1, 9);

    private double Draw((double Min, double Max) range) => range.Min + (range.Max - range.Min) * Draw();
}
