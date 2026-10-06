using Monaco.Presto.Values;

namespace Monaco.Presto;

/// <summary>How a <see cref="ParticleSystem"/> spawns its particles and how they move and change over their lives.</summary>
public sealed record EmitterParams
{
    /// <summary>Particles per second while emitting; 0 emits only the <see cref="Burst"/>.</summary>
    public double Rate { get; init; }

    /// <summary>Particles spawned at once when started.</summary>
    public int Burst { get; init; }

    /// <summary>Milliseconds of continuous emission; infinite when <see cref="Rate"/> is positive and this is unset.</summary>
    public double Duration { get; init; }

    /// <summary>Milliseconds each particle lives, drawn per particle.</summary>
    public (double Min, double Max) Lifetime { get; init; } = (800, 1400);

    /// <summary>Launch speed in units per ms, drawn per particle.</summary>
    public (double Min, double Max) Speed { get; init; } = (0.2, 0.6);

    /// <summary>Launch direction in degrees: 0 is right, -90 is up.</summary>
    public double Angle { get; init; } = -90;

    /// <summary>Degrees either side of <see cref="Angle"/> a launch direction is drawn from.</summary>
    public double Spread { get; init; } = 60;

    /// <summary>Acceleration in units per ms², positive down.</summary>
    public double Gravity { get; init; }

    /// <summary>Exponential velocity decay per second.</summary>
    public double Drag { get; init; }

    /// <summary>Rotation rate in degrees per ms, drawn per particle.</summary>
    public (double Min, double Max) Spin { get; init; }

    /// <summary>Size over life.</summary>
    public ParticleCurve Size { get; init; } = new(6, 6);

    /// <summary>Opacity over life.</summary>
    public ParticleCurve Opacity { get; init; } = new(1, 0);

    /// <summary>The palette each particle's color is drawn from.</summary>
    public IReadOnlyList<PrestoColor> Colors { get; init; } = [new(255, 255, 255)];

    /// <summary>How far each particle's color has moved toward <see cref="EndColor"/> over life; unset keeps it.</summary>
    public ParticleCurve? ColorMix { get; init; }

    /// <summary>The color <see cref="ColorMix"/> blends toward.</summary>
    public PrestoColor? EndColor { get; init; }

    /// <summary>Confetti flips per second, 0 for none.</summary>
    public double Flutter { get; init; }

    /// <summary>The most particles alive at once; further spawns are dropped.</summary>
    public int MaxParticles { get; init; } = 500;

    /// <summary>Seed for every random draw, so a replay matches; null draws a fresh seed.</summary>
    public int? Seed { get; init; }

    /// <summary>The spawn area centered on the origin; zero spawns at the origin itself.</summary>
    public (double Width, double Height) Area { get; init; }

    /// <summary>What each particle looks like, resolved by the renderer.</summary>
    public object? Shape { get; init; }
}
