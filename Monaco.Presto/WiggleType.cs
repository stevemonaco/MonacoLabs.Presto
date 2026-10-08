namespace Monaco.Presto;

/// <summary>The amplitude envelope of an <see cref="Eases.Wiggle"/> ease, after GSAP's CustomWiggle types.</summary>
public enum WiggleType
{
    /// <summary>Starts at full amplitude and decays to rest.</summary>
    EaseOut,

    /// <summary>Grows to full amplitude halfway, then decays to rest.</summary>
    EaseInOut,

    /// <summary>A strong first swing against the to-value, then a quick decay.</summary>
    Anticipate,

    /// <summary>Constant full amplitude throughout.</summary>
    Uniform,

    /// <summary>Random amplitude per swing, decaying to rest.</summary>
    Random,
}
