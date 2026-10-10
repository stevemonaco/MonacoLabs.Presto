using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto;

/// <summary>One live particle of a <see cref="ParticleSystem"/>; <see cref="ParticleSystem.Step"/> writes it and a renderer only reads it.</summary>
public struct ParticleState
{
    /// <summary>Horizontal position in units.</summary>
    public double X { get; internal set; }

    /// <summary>Vertical position in units, positive down.</summary>
    public double Y { get; internal set; }

    /// <summary>Horizontal velocity in units per ms.</summary>
    public double VelocityX { get; internal set; }

    /// <summary>Vertical velocity in units per ms, positive down.</summary>
    public double VelocityY { get; internal set; }

    /// <summary>Time since spawning, in ms.</summary>
    public double Age { get; internal set; }

    /// <summary>The age in ms at which the particle dies.</summary>
    public double Lifetime { get; internal set; }

    /// <summary>Rotation in degrees.</summary>
    public double Rotation { get; internal set; }

    /// <summary>Rotation rate in degrees per ms.</summary>
    public double Spin { get; internal set; }

    /// <summary>Flutter phase in radians; a renderer scales one axis by its cosine.</summary>
    public double FlutterPhase { get; internal set; }

    /// <summary>The color picked from <see cref="EmitterParams.Colors"/> at spawn.</summary>
    public PrestoColor BaseColor { get; internal set; }

    /// <summary>The current size, sampled from <see cref="EmitterParams.Size"/>.</summary>
    public double Size { get; internal set; }

    /// <summary>The current opacity, sampled from <see cref="EmitterParams.Opacity"/>.</summary>
    public double Opacity { get; internal set; }

    /// <summary>The current color: <see cref="BaseColor"/> mixed toward <see cref="EmitterParams.EndColor"/> by <see cref="EmitterParams.ColorMix"/>.</summary>
    public PrestoColor Color { get; internal set; }
}
